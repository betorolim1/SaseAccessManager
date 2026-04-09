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

        public async Task<OperationResult<TemporarySaseUser>> Create(
            string email, string? name, string? lastName, int durationDays, List<string> accessGroups)
        {
            accessGroups = accessGroups
                .Where(g => !string.IsNullOrWhiteSpace(g) && g != "All Users")
                .Select(g => g.Trim())
                .Distinct()
                .ToList();

            var users = await _store.GetAll();

            email = email.Trim().ToLowerInvariant();

            var alreadyActive = users.Any(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.Status == UserStatus.Active);

            if (alreadyActive)
                return OperationResult<TemporarySaseUser>
                    .Fail("Já existe um usuário ativo com este e-mail.");

            var user = new TemporarySaseUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                Name = name,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(durationDays),
                Status = UserStatus.Active,
                AccessGroups = accessGroups
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

            var userId = result.UserId!;

            foreach (var groupId in accessGroups)
            {
                var add = await _sase.AddUserToGroup(groupId, userId);

                if (!add.Success)
                {
                    return OperationResult<TemporarySaseUser>
                        .Fail($"Erro ao adicionar usuário ao grupo no SASE: {add.Error}");
                }
            }

            user.SaseUserId = result.UserId!;

            await _store.Add(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<List<TemporarySaseUser>> List()
            => await _store.GetAll();

        public async Task<OperationResult> Remove(string id)
        {
            var users = await _store.GetAll();

            var user = users.FirstOrDefault(x => x.Id == id);
            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            if (user.Status == UserStatus.Removed)
                return OperationResult.Ok();

            var result = await _sase.DeleteUser(user.SaseUserId!);

            user.LastRemovalAttempt = DateTime.UtcNow;

            if (result.Success)
            {
                user.Status = UserStatus.Removed;
                user.ErrorMessage = null;
            }
            else
            {
                user.Status = UserStatus.Error;
                user.ErrorMessage = result.Error;
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
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.Status == UserStatus.Active);

            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            var current = user.AccessGroups ?? [];

            var toAdd = newGroups.Except(current).ToList();
            var toRemove = current.Except(newGroups).ToList();

            foreach (var g in toAdd)
                await _sase.AddUserToGroup(g, user.SaseUserId!);

            foreach (var g in toRemove)
                await _sase.RemoveUserFromGroup(g, user.SaseUserId!);

            user.AccessGroups = newGroups;

            await _store.Update(user);

            return OperationResult.Ok();
        }

        public async Task<OperationResult<TemporarySaseUser>> ImportExistingUser(
            string email, string? name, string? lastName, int durationDays,
            List<string> accessGroups, string saseUserId, List<string> alreadyInGroups)
        {
            email = email.Trim().ToLowerInvariant();

            var users = await _store.GetAll();

            var alreadyActive = users.Any(u =>
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.Status == UserStatus.Active);

            if (alreadyActive)
                return OperationResult<TemporarySaseUser>
                    .Fail("Já existe um usuário ativo com este e-mail no sistema.");

            var groupsToTrack = alreadyInGroups
                    .Where(g => !string.IsNullOrWhiteSpace(g) && g != "All Users")
                    .Distinct()
                    .ToList();

            var user = new TemporarySaseUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                Name = name,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(durationDays),
                Status = UserStatus.Active,
                SaseUserId = saseUserId,
                AccessGroups = groupsToTrack
            };

            await _store.Add(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<OperationResult> UpdateExpiration(string id, int durationDays)
        {
            var user = await _store.GetById(id);

            if (user == null)
                return OperationResult.Fail("Usuário não encontrado.");

            if (user.Status != UserStatus.Active)
                return OperationResult.Fail("Apenas usuários ativos podem ter o prazo alterado.");

            user.ExpiresAt = DateTime.UtcNow.AddDays(durationDays);
            await _store.Update(user);

            return OperationResult.Ok();
        }

        public async Task<OperationResult<TemporarySaseUser>> Reactivate(string id, int durationDays)
        {
            var user = await _store.GetById(id);

            if (user == null)
                return OperationResult<TemporarySaseUser>.Fail("Usuário não encontrado.");

            if (user.Status == UserStatus.Active)
                return OperationResult<TemporarySaseUser>.Fail("Usuário já está ativo.");

            // Garante que não existe outro registro ativo com o mesmo email
            var users = await _store.GetAll();
            var conflito = users.Any(u =>
                u.Id != id &&
                u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase) &&
                u.Status == UserStatus.Active);

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
                user.SaseUserId = result.UserId!;
            }

            // Readiciona nos grupos
            foreach (var groupId in user.AccessGroups ?? [])
            {
                var add = await _sase.AddUserToGroup(groupId, user.SaseUserId);
                if (!add.Success)
                    return OperationResult<TemporarySaseUser>.Fail($"Usuário recriado no SASE, mas erro ao adicionar ao grupo: {add.Error}");
            }

            user.Status = UserStatus.Active;
            user.ExpiresAt = DateTime.UtcNow.AddDays(durationDays);
            user.ErrorMessage = null;
            user.LastRemovalAttempt = null;
            user.CreatedAt = DateTime.UtcNow;

            await _store.Update(user);

            return OperationResult<TemporarySaseUser>.Ok(user);
        }

        public async Task<BatchOperationResult> CreateBatch(
            List<(string Email, string? Name, string? LastName)> users,
            int durationDays,
            List<string> accessGroups)
        {
            var results = new List<BatchUserResult>();

            foreach (var (email, name, lastName) in users)
            {
                var create = await Create(email, name, lastName, durationDays, accessGroups);

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

        private static SaseCreateUserRequest BuildSaseRequest(TemporarySaseUser user)
        {

            var isGov = IsGovEmail(user.Email);

            return new SaseCreateUserRequest
            {
                IdpType = isGov ? "azureAD" : "database",
                EmailVerified = isGov,
                Email = user.Email,
                ProfileData = new SaseProfileData
                {
                    FirstName = user.Name,
                    LastName = user.LastName ?? "",
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
