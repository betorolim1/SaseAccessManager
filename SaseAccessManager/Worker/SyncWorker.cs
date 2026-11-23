using SaseAccessManager.DTOs;
using SaseAccessManager.Models;
using SaseAccessManager.Services;

namespace SaseAccessManager.Worker
{
    public class SyncWorker : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly ILogger<SyncWorker> _logger;

        public SyncWorker(IServiceProvider provider, ILogger<SyncWorker> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        private const bool DryRun = false; // Se False, as mudanças serão aplicadas de fato. Se True, as mudanças serão logadas mas não aplicadas.

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Sync Worker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await WaitUntilNextRun(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await ExecuteSync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar sincronização SASE.");
                }
            }
        }

        private async Task ExecuteSync(CancellationToken ct)
        {
            using var scope = _provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<PostgresUserStore>();
            var sase = scope.ServiceProvider.GetRequiredService<ISaseClient>();
            var sync = scope.ServiceProvider.GetRequiredService<SaseSyncService>();

            var dryRun = DryRun;

            if (dryRun)
                _logger.LogWarning("Sync em modo DRY-RUN. Nenhum usuário será removido de fato.");

            // 1. Buscar todos os usuários do SASE
            var saseUsers = await sase.GetAllUsersAsync(ct);
            _logger.LogInformation("SASE retornou {Count} usuários ativos.", saseUsers.Count);

            // 2. Buscar todos os usuários ativos no banco local
            var localUsers = await store.GetAll();

            // 2.1 Atualizar perfil (Admin/User) dos usuários locais a partir do SASE
            await SyncRoles(store, localUsers, saseUsers, dryRun);

            // 3. Identificar órfãos (mesma regra do botão de sincronização manual)
            var report = SaseSyncService.FindOrphans(saseUsers, localUsers);
            var admins = report.Admins;
            var whitelisted = report.Whitelisted;
            var toRemove = new List<SaseUserDto>();

#if !DEBUG
            toRemove = report.ToRemove;
#endif

            if (admins.Count > 0)
            {
                _logger.LogInformation(
                    "Ignorados {Count} usuários Admin: {Emails}",
                    admins.Count,
                    string.Join(", ", admins.Select(a => a.Email)));
            }

            if (whitelisted.Count > 0)
            {
                _logger.LogInformation(
                    "Ignorados {Count} usuários da whitelist: {Emails}",
                    whitelisted.Count,
                    string.Join(", ", whitelisted.Select(w => w.Email)));
            }

            if (toRemove.Count == 0)
            {
                _logger.LogInformation("Nenhum usuário órfão para remover.");
                return;
            }

            // Trava: muitos órfãos de uma vez indica problema (banco vazio, IDs desalinhados etc.), não limpeza normal
            const int MaxRemocoesPorExecucao = 15;

            if (toRemove.Count > MaxRemocoesPorExecucao)
            {
                _logger.LogCritical(
                    "Sync ABORTADO: {Count} órfãos encontrados (limite {Max}). Nenhum usuário foi removido. " +
                    "Verifique se o banco está alinhado com o SASE. Primeiros: {Emails}",
                    toRemove.Count, MaxRemocoesPorExecucao,
                    string.Join(", ", toRemove.Take(10).Select(o => o.Email)));
                return;
            }

            _logger.LogWarning(
                "{Mode}: {Count} usuários órfãos identificados.",
                dryRun ? "DRY-RUN" : "EXECUTANDO",
                toRemove.Count);

            var removed = 0;
            var errors = 0;

            if (dryRun)
            {
                foreach (var orphan in toRemove)
                    _logger.LogWarning("[DRY-RUN] Seria removido: {Email} (ID: {Id})", orphan.Email, orphan.Id);
            }
            else
            {
                (removed, errors, _) = await sync.RemoveOrphansAsync(toRemove, "sincronização automática");
            }

            if (dryRun)
            {
                _logger.LogWarning(
                    "[DRY-RUN] Sincronização concluída. {Count} usuários seriam removidos.",
                    toRemove.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Sincronização concluída. Removidos: {Removed}, Erros: {Errors}",
                    removed, errors);
            }
        }

        private async Task SyncRoles(
            PostgresUserStore store,
            List<TemporarySaseUser> localUsers,
            List<SaseUserDto> saseUsers,
            bool dryRun)
        {
            var saseById = saseUsers
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var updated = 0;

            foreach (var local in localUsers.Where(u =>
                         u.ST_USUARIO != UserStatus.Removed &&
                         !string.IsNullOrWhiteSpace(u.ID_USUARIO_PERIMETER)))
            {
                if (!saseById.TryGetValue(local.ID_USUARIO_PERIMETER!, out var sase))
                    continue;

                var perfil = sase.Perfil;
                var changed = !string.Equals(local.DS_PERFIL_SASE, perfil, StringComparison.OrdinalIgnoreCase);

                // Admin que ficou em Erro por tentativa de remoção recusada pelo SASE: volta a Ativo
                var fixError = sase.IsAdmin && local.ST_USUARIO == UserStatus.Error;

                if (!changed && !fixError)
                    continue;

                if (dryRun)
                {
                    _logger.LogWarning(
                        "[DRY-RUN] Perfil de {Email} seria atualizado: {Old} -> {New}{Fix}",
                        local.DS_EMAIL, local.DS_PERFIL_SASE ?? "(vazio)", perfil,
                        fixError ? " (e status Erro -> Ativo)" : "");
                    continue;
                }

                local.DS_PERFIL_SASE = perfil;

                if (fixError)
                {
                    local.ST_USUARIO = UserStatus.Active;
                    local.DS_ERRO = null;
                    local.DH_TENTATIVA_REMOCAO = null;
                }

                await store.Update(local);
                updated++;
            }

            if (updated > 0)
                _logger.LogInformation("Perfil SASE atualizado para {Count} usuário(s).", updated);
        }

        private static readonly int[] RunHours = { 0, 6, 12, 18 };

        private async Task WaitUntilNextRun(CancellationToken token)
        {
            var now = DateTime.Now;

            var nextRun = RunHours
                .Select(h => DateTime.Today.AddHours(h))
                .Where(t => t > now)
                .DefaultIfEmpty(DateTime.Today.AddDays(1).AddHours(RunHours.Min()))
                .First();

            var delay = nextRun - now;

            _logger.LogInformation(
                "Próxima sincronização em {Delay} ({NextRun:dd/MM/yyyy HH:mm}).",
                delay, nextRun);

            await Task.Delay(delay, token);
        }
    }
}