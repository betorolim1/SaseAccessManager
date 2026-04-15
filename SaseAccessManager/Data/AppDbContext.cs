using Microsoft.EntityFrameworkCore;
using SaseAccessManager.Models;

namespace SaseAccessManager.Data
{
public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
 
        public DbSet<TemporarySaseUser> S_USUARIO_SASE { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TemporarySaseUser>(entity =>
            {
                entity.ToTable("s_usuario_sase");

                entity.HasKey(e => e.ID_USUARIO_SASE);

                entity.Property(e => e.ID_USUARIO_SASE).HasColumnName("id_usuario_sase");
                entity.Property(e => e.DS_EMAIL).HasColumnName("ds_email").IsRequired();
                entity.Property(e => e.NM_USUARIO).HasColumnName("nm_usuario").IsRequired();
                entity.Property(e => e.NM_SOBRENOME).HasColumnName("nm_sobrenome");
                entity.Property(e => e.DH_CRIACAO).HasColumnName("dh_criacao");
                entity.Property(e => e.DH_EXPIRACAO).HasColumnName("dh_expiracao");
                entity.Property(e => e.ST_USUARIO).HasColumnName("st_usuario").HasConversion<string>();
                entity.Property(e => e.ID_USUARIO_PERIMETER).HasColumnName("id_usuario_perimeter");
                entity.Property(e => e.DH_TENTATIVA_REMOCAO).HasColumnName("dh_tentativa_remocao");
                entity.Property(e => e.DS_ERRO).HasColumnName("ds_erro");
                entity.Property(e => e.DS_GRUPO_ACESSO).HasColumnName("ds_grupo_acesso").HasColumnType("jsonb");
            });
        }
    }
}
