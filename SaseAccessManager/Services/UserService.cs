using SaseAccessManager.DTOs;
using SaseAccessManager.Models;

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

        public async Task<TemporarySaseUser> Create(string email, string? name, string? lastName, int durationDays)
        {
            var request = new SaseCreateUserRequest
            {
                Email = email,
                ProfileData = new SaseProfileData
                {
                    FirstName = name ?? "",
                    LastName = lastName ?? "",
                    RoleName = "Member"
                }
            };

            var result = await _sase.CreateUser(request);

            if (!result.Success)
                throw new Exception(result.Error);

            var user = new TemporarySaseUser
            {
                Email = email,
                Name = name,
                LastName = lastName,
                SaseUserId = result.UserId!,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(durationDays),
                Id = result.UserId
            };

            var users = await _store.GetAll();
            users.Add(user);
            await _store.SaveAll(users);

            return user;
        }

        public async Task<List<TemporarySaseUser>> List()
            => await _store.GetAll();

        public async Task<bool> Remove(string id)
        {
            var users = await _store.GetAll();

            var user = users.FirstOrDefault(x => x.Id == id);
            if (user == null)
                return false;

            if (user.Status == UserStatus.Removed)
                return true;

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
            return result.Success;
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
