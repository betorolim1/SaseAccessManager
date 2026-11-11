using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaseAccessManager.Models
{
    public enum UserStatus
    {
        Active = 1,
        Removed = 2,
        Error = 3
    }

    public static class SaseRoles
    {
        public const string Admin = "Admin";

        public static bool IsAdmin(string? role) =>
            string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);
    }

    public class TemporarySaseUser
    {
        public Guid ID_USUARIO_SASE { get; set; }

        public string DS_EMAIL { get; set; } = default!;

        public string NM_USUARIO { get; set; } = default!;
        public string? NM_SOBRENOME { get; set; }

        public DateTime DH_CRIACAO { get; set; }
        public DateTime DH_EXPIRACAO { get; set; }

        public UserStatus ST_USUARIO { get; set; }

        public string? ID_USUARIO_PERIMETER { get; set; }

        public DateTime? DH_TENTATIVA_REMOCAO { get; set; }

        public string? DS_ERRO { get; set; }

        public List<string> DS_GRUPO_ACESSO { get; set; } = [];

        public string? DS_CHAMADO { get; set; }

        public string? DS_MOTIVO_REMOCAO { get; set; }

        public string? DS_OBSERVACAO_REMOCAO { get; set; }

        /// <summary>Perfil do usuário no SASE (ex: "Admin", "User"). Atualizado pelo SyncWorker.</summary>
        public string? DS_PERFIL_SASE { get; set; }

        /// <summary>Admins não podem ser excluídos pela API do SASE — o sistema não tenta removê-los.</summary>
        [NotMapped]
        public bool IsAdmin => SaseRoles.IsAdmin(DS_PERFIL_SASE);
    }
}