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
            return await _db.S_USUARIO_SASE.ToListAsync();
        }

        public async Task SaveAll(List<TemporarySaseUser> users)
        {
            _db.S_USUARIO_SASE.RemoveRange(_db.S_USUARIO_SASE);
            _db.S_USUARIO_SASE.AddRange(users);
            await _db.SaveChangesAsync();
        }

        public async Task Add(TemporarySaseUser user)
        {
            _db.S_USUARIO_SASE.Add(user);
            await _db.SaveChangesAsync();
        }

        public async Task Update(TemporarySaseUser user)
        {
            _db.S_USUARIO_SASE.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<TemporarySaseUser?> GetById(Guid id)
        {
            return await _db.S_USUARIO_SASE.FindAsync(id);
        }

        public async Task Remove(Guid id)
        {
            var user = await _db.S_USUARIO_SASE.FindAsync(id);
            if (user != null)
            {
                _db.S_USUARIO_SASE.Remove(user);
                await _db.SaveChangesAsync();
            }
        }
    }
}
