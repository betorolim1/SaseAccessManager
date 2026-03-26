using Microsoft.EntityFrameworkCore;
using SaseAccessManager.Data;
using SaseAccessManager.Models;

namespace SaseAccessManager.Services
{
    public class PostgresUserStore
    {
        private readonly AppDbContext _db;

        public PostgresUserStore(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<TemporarySaseUser>> GetAll()
        {
            return await _db.Users.ToListAsync();
        }

        public async Task SaveAll(List<TemporarySaseUser> users)
        {
            _db.Users.RemoveRange(_db.Users);
            _db.Users.AddRange(users);
            await _db.SaveChangesAsync();
        }

        public async Task Add(TemporarySaseUser user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        public async Task Update(TemporarySaseUser user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<TemporarySaseUser?> GetById(string id)
        {
            return await _db.Users.FindAsync(id);
        }

        public async Task Remove(string id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
            }
        }
    }
}
