using SaseAccessManager.DTOs;
using SaseAccessManager.Models;
using SaseAccessManager.Results;

namespace SaseAccessManager.Services
{
    public class UserService
    {
        private readonly PostgresUserStore _store;
        private readonly ISaseClient _sase;

        private static readonly HashSet<string> AzureDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "agro.gov.br",
            "inmet.gov.br",
            "mpa.gov.br",
            "mda.gov.br",
            "apoio.agro.gov.br"
        };

        public UserService(PostgresUserStore store, ISaseClient sase)
        {
            _store = store;
            _sase = sase;
        }

        private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute EmailValidator = new();

        /// <summary>
        /// Normaliza e valida as chefias: pelo menos 1 obrigatória, formato de e-mail,
        /// sem duplicidade e diferente do próprio usuário. Se só a 2ª vier preenchida, ela vira a 1ª.
        /// </summary>
        public static (bool Ok, string? Error, string? Chefia1, string? Chefia2) NormalizeChefias(
            string? chefia1, string? chefia2, string? userEmail = null)
        {
            static string? Clean(string? s) =>
                string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();

            var c1 = Clean(chefia1);
            var c2 = Clean(chefia2);

            if (c1 is null && c2 is not null)
            {
                c1 = c2;
                c2 = null;
            }

            if (c1 is null)
                return (false, "Informe o email de pelo menos uma chefia imediata.", null, null);

            foreach (var c in new[] { c1, c2 })
            {
                if (c is not null && (c.Length > 254 || !EmailValidator.IsValid(c) || !c.Contains('.')))
                    return (false, $"Email de chefia inválido: {c}", null, null);
            }

            if (c2 == c1)
                c2 = null;

            var self = Clean(userEmail);
            if (self is not null && (c1 == self || c2 == self))
                return (false, "O usuário não pode ser cadastrado como a própria chefia.", null, null);

            return (true, null, c1, c2);
        }

        public async Task<OperationResult<TemporarySaseUser>> Create(
            string email, string? name, string? lastName, int durationDays, List<string> accessGroups,
            string? chamado = null, string? chefia1 = null, string? chefia2 = null)
        {
            var chefias = NormalizeChefias(chefia1, chefia2, email);
            if (!chefias.Ok)
                return OperationResult<TemporarySaseUser>.Fail(chefias.Error!);

            accessGroups = accessGroups
                .Where(g => !string.IsNullOrWhiteSpace(g) && g != "All Users")
                .Select(g => g.Trim())
                .Distinct()
                .ToList();

            var users = await _store.GetAll();

            email = email.Trim().ToLowerInvariant();

            var alreadyActive = users.Any(u =>
                u.DS_EMAIL.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.ST_USUARIO == UserStatus.Active);

            if (alreadyActive)
                return OperationResult<TemporarySaseUser>
                    .Fail("Já existe um usuário ativo com este e-mail.");

            if (email.Length > 254)
                return OperationResult<TemporarySaseUser>.Fail("Email excede o limite.");

            if (name?.Length > 100)
                return OperationResult<TemporarySaseUser>.Fail("Nome excede o limite.");

            if (lastName?.Length > 100)
                return OperationResult<TemporarySaseUser>.Fail("Sobrenome excede o limite.");

            var user = new TemporarySaseUser
            {
                ID_USUARIO_SASE = Guid.NewGuid(),
                DS_EMAIL = email,
                NM_USUARIO = name,
                NM_SOBRENOME = lastName,
                DH_CRIACAO = DateTime.UtcNow,
                DH_EXPIRACAO = DateTime.UtcNow.AddDays(durationDays),
                ST_USUARIO = UserStatus.Active,
                DS_GRUPO_ACESSO = accessGroups,
                DS_CHAMADO = chamado,
                DS_EMAIL_CHEFIA_1 = chefias.Chefia1,
                DS_EMAIL_CHEFIA_2 = chefias.Chefia2
            };

            var request = BuildSaseRequest(user);
            var result = await _sase.CreateUser(request);

            if (result.AlreadyExists)
            {
                var saseUser = await _sase.GetUserByEmailAsync(email);
                if (saseUser == null)
                    return OperationResult<TemporarySaseUser>.Fail("Usuário já existe no SASE mas não foi possível localizá-lo.");

                // Busca grupos diretamente da API (sem cache) para garantir dados atualizados
                var allGroups = await _sase.GetGroupsAsync(CancellationToken.None);
                var existingGroupIds = allGroups
                    .Where(g => g.Users.Contains(saseUser.Id) &&
                                !string.Equals(g.Name, "All Users", StringComparison.OrdinalIgnoreCase))
                    .Select(g => g.Id)
                    .ToList();

                return OperationResult<TemporarySaseUser>.ExistsInSase(saseUser.Id, existingGroupIds);
            }

            if (!result.Success)
                return OperationResult<TemporarySaseUser>
                    .Fail($"Erro ao criar usuário no SASE: {result.Error}");

            if (result.UserId is null)
            {
                return OperationResult<TemporarySaseUser>
                    .Fail("UserId não retornado pelo SASE.");
            }

            var userId = result.UserId;

            foreach (var groupId in accessGroups)
            {
                var add = await _sase.AddUserToGroup(groupId, userId);

                if (!add.Success)
                {
                    return OperationResult<TemporarySaseUser>
                        .Fail($"Erro ao adicionar usuário ao grupo no SASE: {add.Error}");
                }
            }

            user.ID_USUARIO_PERIMETER = result.UserId!;
            user.DS_PERFIL_SASE = "User"; // criado pelo SAM sempre como Member/User

            await _store.Add(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<List<TemporarySaseUser>> List()
            => await _store.GetAll();

        public const int ObservacaoMaxLength = 500;

        public async Task<OperationResult> Remove(Guid id, string? motivo = null, string? observacao = null)
        {
            observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();

            if (observacao?.Length > ObservacaoMaxLength)
                return OperationResult.Fail($"Observação excede o limite de {ObservacaoMaxLength} caracteres.");

            var users = await _store.GetAll();

            var user = users.FirstOrDefault(x => x.ID_USUARIO_SASE == id);
            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            if (user.ST_USUARIO == UserStatus.Removed)
                return OperationResult.Ok();

            // A API do SASE recusa exclusão de Admin — não tenta, não marca Erro
            if (user.IsAdmin)
                return OperationResult.Fail("Usuários Admin não podem ser removidos pelo sistema.");

            var result = await _sase.DeleteUser(user.ID_USUARIO_PERIMETER!);

            user.DH_TENTATIVA_REMOCAO = DateTime.UtcNow;

            if (result.Success)
            {
                user.ST_USUARIO = UserStatus.Removed;
                user.DS_ERRO = null;
                user.DS_MOTIVO_REMOCAO = motivo;
                user.DS_OBSERVACAO_REMOCAO = observacao;
            }
            else
            {
                user.ST_USUARIO = UserStatus.Error;
                user.DS_ERRO = result.Error;
            }

            await _store.Update(user);

            return result.Success
                ? OperationResult.Ok()
                : OperationResult.Fail(result.Error ?? "Erro ao remover usuário.");
        }

        public async Task<OperationResult> UpdateGroups(string email, List<string> newGroups)
        {
            var users = await _store.GetAll();

            var user = users.FirstOrDefault(u =>
                u.DS_EMAIL.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.ST_USUARIO == UserStatus.Active);

            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            var current = user.DS_GRUPO_ACESSO ?? [];

            var toAdd = newGroups.Except(current).ToList();
            var toRemove = current.Except(newGroups).ToList();

            foreach (var g in toAdd)
                await _sase.AddUserToGroup(g, user.ID_USUARIO_PERIMETER!);

            foreach (var g in toRemove)
                await _sase.RemoveUserFromGroup(g, user.ID_USUARIO_PERIMETER!);

            user.DS_GRUPO_ACESSO = newGroups;

            await _store.Update(user);

            return OperationResult.Ok();
        }

        public async Task<OperationResult<TemporarySaseUser>> ImportExistingUser(
            string email, string? name, string? lastName, int durationDays,
            List<string> accessGroups, string saseUserId, List<string> alreadyInGroups, string? chamado = null,
            string? chefia1 = null, string? chefia2 = null)
        {
            email = email.Trim().ToLowerInvariant();

            var chefias = NormalizeChefias(chefia1, chefia2, email);
            if (!chefias.Ok)
                return OperationResult<TemporarySaseUser>.Fail(chefias.Error!);

            var users = await _store.GetAll();

            var alreadyActive = users.Any(u =>
                u.DS_EMAIL.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.ST_USUARIO == UserStatus.Active);

            if (alreadyActive)
                return OperationResult<TemporarySaseUser>
                    .Fail("Já existe um usuário ativo com este e-mail no sistema.");

            var saseUser = await _sase.GetUserByEmailAsync(email);

            var groupsToTrack = alreadyInGroups
                    .Where(g => !string.IsNullOrWhiteSpace(g) && g != "All Users")
                    .Distinct()
                    .ToList();

            var user = new TemporarySaseUser
            {
                ID_USUARIO_SASE = Guid.NewGuid(),
                DS_EMAIL = email,
                NM_USUARIO = name,
                NM_SOBRENOME = lastName,
                DH_CRIACAO = DateTime.UtcNow,
                DH_EXPIRACAO = DateTime.UtcNow.AddDays(durationDays),
                ST_USUARIO = UserStatus.Active,
                ID_USUARIO_PERIMETER = saseUserId,
                DS_GRUPO_ACESSO = groupsToTrack,
                DS_CHAMADO = chamado,
                DS_PERFIL_SASE = saseUser?.Perfil,
                DS_EMAIL_CHEFIA_1 = chefias.Chefia1,
                DS_EMAIL_CHEFIA_2 = chefias.Chefia2
            };

            await _store.Add(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<OperationResult> UpdateExpiration(
            Guid id, int durationDays, string? chamado = null, string? chefia1 = null, string? chefia2 = null)
        {
            var user = await _store.GetById(id);

            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            if (user.ST_USUARIO != UserStatus.Active)
                return OperationResult.Fail("Apenas usuários ativos podem ter o prazo alterado.");

            var chefias = NormalizeChefias(chefia1, chefia2, user.DS_EMAIL);
            if (!chefias.Ok)
                return OperationResult.Fail(chefias.Error!);

            user.DH_EXPIRACAO = DateTime.UtcNow.AddDays(durationDays);
            user.DS_CHAMADO = chamado;
            user.DS_EMAIL_CHEFIA_1 = chefias.Chefia1;
            user.DS_EMAIL_CHEFIA_2 = chefias.Chefia2;

            await _store.Update(user);

            return OperationResult.Ok();
        }

        public async Task<OperationResult<TemporarySaseUser>> Reactivate(Guid id, int durationDays)
        {
            var user = await _store.GetById(id);

            if (user == null)
                return OperationResult<TemporarySaseUser>.Fail("Usuário não encontrado.");

            if (user.ST_USUARIO == UserStatus.Active)
                return OperationResult<TemporarySaseUser>.Fail("Usuário já está ativo.");

            // Garante que não existe outro registro ativo com o mesmo email
            var users = await _store.GetAll();
            var conflito = users.Any(u =>
                u.ID_USUARIO_SASE != id &&
                u.DS_EMAIL.Equals(user.DS_EMAIL, StringComparison.OrdinalIgnoreCase) &&
                u.ST_USUARIO == UserStatus.Active);

            if (conflito)
                return OperationResult<TemporarySaseUser>.Fail(
                    "Já existe um usuário ativo com este e-mail. Remova-o antes de reativar este registro.");

            var request = BuildSaseRequest(user);
            var result = await _sase.CreateUser(request);

            if (result.AlreadyExists)
            {
                return OperationResult<TemporarySaseUser>.Fail(
                    "O usuário já existe no SASE. Para reativá-lo no sistema, acesse 'Novo usuário', informe o e-mail dele e confirme a importação quando solicitado.");
            }
            else if (!result.Success)
            {
                return OperationResult<TemporarySaseUser>.Fail($"Erro ao recriar usuário no SASE: {result.Error}");
            }
            else
            {
                user.ID_USUARIO_PERIMETER = result.UserId!;
            }

            // Readiciona nos grupos
            foreach (var groupId in user.DS_GRUPO_ACESSO ?? [])
            {
                var add = await _sase.AddUserToGroup(groupId, user.ID_USUARIO_PERIMETER);
                if (!add.Success)
                    return OperationResult<TemporarySaseUser>.Fail($"Usuário recriado no SASE, mas erro ao adicionar ao grupo: {add.Error}");
            }

            user.ST_USUARIO = UserStatus.Active;
            user.DH_EXPIRACAO = DateTime.UtcNow.AddDays(durationDays);
            user.DS_ERRO = null;
            user.DH_TENTATIVA_REMOCAO = null;
            user.DH_CRIACAO = DateTime.UtcNow;
            user.DS_MOTIVO_REMOCAO = null;
            user.DS_OBSERVACAO_REMOCAO = null;

            await _store.Update(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<BatchOperationResult> CreateBatch(
            List<(string Email, string? Name, string? LastName)> users,
            int durationDays, List<string> accessGroups, string? chamado = null,
            string? chefia1 = null, string? chefia2 = null)
        {
            var results = new List<BatchUserResult>();

            foreach (var (email, name, lastName) in users)
            {
                var create = await Create(email, name, lastName, durationDays, accessGroups, chamado, chefia1, chefia2);

                if (create.Success)
                {
                    results.Add(new BatchUserResult { Email = email, Success = true });
                }
                else if (create.UserAlreadyExistsInSase)
                {
                    results.Add(new BatchUserResult
                    {
                        Email = email,
                        Success = false,
                        Error = "Usuário já existe no SASE. Use a criação individual para importá-lo."
                    });
                }
                else
                {
                    results.Add(new BatchUserResult
                    {
                        Email = email,
                        Success = false,
                        Error = create.Error
                    });
                }
            }

            return new BatchOperationResult { Results = results };
        }

        public static SaseCreateUserRequest BuildSaseRequest(TemporarySaseUser user)
        {

            var isGov = IsGovEmail(user.DS_EMAIL);

            return new SaseCreateUserRequest
            {
                IdpType = isGov ? "azureAD" : "database",
                EmailVerified = isGov,
                Email = user.DS_EMAIL,
                ProfileData = new SaseProfileData
                {
                    FirstName = user.NM_USUARIO,
                    LastName = user.NM_SOBRENOME ?? "",
                    RoleName = "Member"
                }
            };
        }

        private static bool IsGovEmail(string email)
        {
            var at = email.LastIndexOf('@');
            if (at < 0)
                return false;

            var domain = email[(at + 1)..].Trim();

            return AzureDomains.Contains(domain);
        }
    }
}