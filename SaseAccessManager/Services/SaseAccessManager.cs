using SaseAccessManager.DTOs;
using SaseAccessManager.Models;

namespace SaseAccessManager.Services
{
    /// <summary>Resultado da busca de órfãos (usuários que estão no SASE mas não estão ativos no SAM).</summary>
    public record OrphanReport(
        List<SaseUserDto> ToRemove,
        List<SaseUserDto> Admins,
        List<SaseUserDto> Whitelisted,
        int SaseCount);

    /// <summary>
    /// Regra única de "usuário órfão", usada pelo SyncWorker (automático) e pelo botão de sincronização manual.
    /// </summary>
    public class SaseSyncService
    {
        private readonly PostgresUserStore _store;
        private readonly ISaseClient _sase;
        private readonly ILogger<SaseSyncService> _logger;

        /// <summary>Usuários que nunca são removidos como órfãos.</summary>
        public static readonly HashSet<string> WhitelistEmails = new(StringComparer.OrdinalIgnoreCase)
        {
            "marcelo.zaranza@hepta.com.br",
            "hadson.fonseca@hepta.com.br",
            "daniel.dsantos@agro.gov.br",
            "adalberto.nogueira@agro.gov.br",
            "guilherme.bernardes@ntsec.com.br"
        };

        public SaseSyncService(PostgresUserStore store, ISaseClient sase, ILogger<SaseSyncService> logger)
        {
            _store = store;
            _sase = sase;
            _logger = logger;
        }

        /// <summary>Busca SASE e banco e calcula os órfãos.</summary>
        public async Task<OrphanReport> FindOrphansAsync(CancellationToken ct = default)
        {
            var saseUsers = await _sase.GetAllUsersAsync(ct);
            var localUsers = await _store.GetAll();
            return FindOrphans(saseUsers, localUsers);
        }

        /// <summary>
        /// Calcula os órfãos a partir de listas já carregadas (o SyncWorker usa esta versão
        /// depois de atualizar os perfis, para não buscar tudo de novo).
        /// </summary>
        public static OrphanReport FindOrphans(List<SaseUserDto> saseUsers, List<TemporarySaseUser> localUsers)
        {
            var localPerimeterIds = localUsers
                .Where(u => u.ST_USUARIO == UserStatus.Active &&
                            !string.IsNullOrWhiteSpace(u.ID_USUARIO_PERIMETER))
                .Select(u => u.ID_USUARIO_PERIMETER!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Proteção: banco sem usuários ativos faria TODO o SASE parecer órfão
            if (localPerimeterIds.Count == 0)
                throw new InvalidOperationException(
                    "O banco do SAM não tem usuários ativos com ID do SASE. Busca de órfãos cancelada por segurança.");

            var orphans = saseUsers
                .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !localPerimeterIds.Contains(s.Id))
                .ToList();

            // Admins não podem ser excluídos pela API — nunca entram na remoção
            var admins = orphans.Where(o => o.IsAdmin).ToList();
            var candidates = orphans.Where(o => !o.IsAdmin).ToList();

            var whitelisted = candidates.Where(o => WhitelistEmails.Contains(o.Email ?? "")).ToList();
            var toRemove = candidates.Where(o => !WhitelistEmails.Contains(o.Email ?? "")).ToList();

            return new OrphanReport(toRemove, admins, whitelisted, saseUsers.Count);
        }

        /// <summary>Remove do SASE os usuários informados.</summary>
        public async Task<(int Removed, int Errors, List<string> Failures)> RemoveOrphansAsync(
            IEnumerable<SaseUserDto> orphans, string origem)
        {
            var removed = 0;
            var failures = new List<string>();

            foreach (var orphan in orphans)
            {
                var result = await _sase.DeleteUser(orphan.Id);

                if (result.Success)
                {
                    removed++;
                    _logger.LogInformation("Órfão removido ({Origem}): {Email} (ID: {Id})", origem, orphan.Email, orphan.Id);
                }
                else
                {
                    failures.Add($"{orphan.Email}: {result.Error}");
                    _logger.LogError("Falha ao remover órfão ({Origem}) {Email}: {Error}", origem, orphan.Email, result.Error);
                }
            }

            return (removed, failures.Count, failures);
        }
    }
}