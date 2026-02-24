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

        public async Task<TemporarySaseUser> Create(string email, string? name, int durationDays)
        {
            var result = await _sase.CreateUser(email, name);

            if (!result.Success)
                throw new Exception(result.Error);

            var user = new TemporarySaseUser
            {
                Email = email,
                Name = name,
                SaseUserId = result.UserId!,
                ExpiresAt = DateTime.UtcNow.AddDays(durationDays)
            };

            var users = await _store.GetAll();
            users.Add(user);
            await _store.SaveAll(users);

            return user;
        }

        public async Task<List<TemporarySaseUser>> List()
            => await _store.GetAll();

        public async Task<bool> Remove(Guid id)
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
    }
}
