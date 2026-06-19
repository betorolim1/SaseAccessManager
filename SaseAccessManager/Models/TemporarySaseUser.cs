namespace SaseAccessManager.Models
{
    public enum UserStatus
    {
        Active = 1,
        Removed = 2,
        Error = 3
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
    }
}
