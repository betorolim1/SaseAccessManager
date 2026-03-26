using Microsoft.EntityFrameworkCore;
using SaseAccessManager.Models;

namespace SaseAccessManager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TemporarySaseUser> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TemporarySaseUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Status).HasConversion<string>();

                entity.Property(e => e.AccessGroups)
                      .HasColumnType("jsonb");
            });
        }
    }
}
