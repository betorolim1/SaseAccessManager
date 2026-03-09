using SaseAccessManager.DTOs;
using SaseAccessManager.Models;
using SaseAccessManager.Results;

namespace SaseAccessManager.Services
{
    public class UserService
    {
        private readonly FileUserStore _store;
        private readonly ISaseClient _sase;

        private static readonly HashSet<string> AzureDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "agro.gov.br",
            "inmet.gov.br",
            "mpa.gov.br",
            "mda.gov.br",
            "apoio.agro.gov.br"
        };

        public UserService(FileUserStore store, ISaseClient sase)
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

            users.Add(user);
            await _store.SaveAll(users);

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

            await _store.SaveAll(users);

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

            await _store.SaveAll(users);

            return OperationResult.Ok();
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
