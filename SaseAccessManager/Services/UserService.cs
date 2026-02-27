using SaseAccessManager.DTOs;
using SaseAccessManager.Models;
using SaseAccessManager.Results;

namespace SaseAccessManager.Services
{
    public class UserService
    {
        private readonly FileUserStore _store;
        private readonly ISaseClient _sase;

        public UserService(FileUserStore store, ISaseClient sase)
        {
            _store = store;
            _sase = sase;
        }

        public async Task<OperationResult<TemporarySaseUser>> Create(
            string email, string? name, string? lastName, int durationDays)
        {
            var users = await _store.GetAll();

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
                Status = UserStatus.Active
            };

            var request = BuildSaseRequest(user);
            var result = await _sase.CreateUser(request);

            if (!result.Success)
                return OperationResult<TemporarySaseUser>
                    .Fail($"Erro ao criar usuário no SASE: {result.Error}");

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

        private static SaseCreateUserRequest BuildSaseRequest(TemporarySaseUser user)
        {
            return new SaseCreateUserRequest
            {
                Email = user.Email,
                ProfileData = new SaseProfileData
                {
                    FirstName = user.Name,
                    LastName = user.LastName ?? "",
                    RoleName = "Member"
                }
            };
        }
    }
}
