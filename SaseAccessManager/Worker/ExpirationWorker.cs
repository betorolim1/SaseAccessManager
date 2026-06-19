using SaseAccessManager.Models;
using SaseAccessManager.Services;

namespace SaseAccessManager.Worker
{
    public class ExpirationWorker : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly ILogger<ExpirationWorker> _logger;
        private readonly IConfiguration _config;

        public ExpirationWorker(IServiceProvider provider, ILogger<ExpirationWorker> logger, IConfiguration config)
        {
            _provider = provider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expiration Worker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await WaitUntilNextRun(stoppingToken);

                try
                {
                    using var scope = _provider.CreateScope();
                    var store = scope.ServiceProvider.GetRequiredService<PostgresUserStore>();
                    var userService = scope.ServiceProvider.GetRequiredService<UserService>();

                    var users = await store.GetAll();

                    var expired = users
                        .Where(x => x.ST_USUARIO == UserStatus.Active &&
                                    x.DH_EXPIRACAO <= DateTime.UtcNow)
                        .ToList();

                    if (expired.Count == 0)
                    {
                        _logger.LogInformation("Nenhum usuário expirado para remover da lista hoje.");
                    }
                    else
                    {
                        _logger.LogInformation($"Encontrados {expired.Count} usuários expirados.");

                        foreach (var user in expired)
                        {
                            _logger.LogInformation($"Removendo usuário expirado: {user.DS_EMAIL}");
                            await userService.Remove(user.ID_USUARIO_SASE, $"Removido automaticamente por expiração em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC.");
                        }
                    }

                    await CleanupOldRemovedUsers(store);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar expiração.");
                }
            }
        }

        private async Task CleanupOldRemovedUsers(PostgresUserStore store)
        {
            var retentionDays = _config.GetValue<int>("RetentionDays", 90);
            var limitDate = DateTime.UtcNow.AddDays(-retentionDays);

            var users = await store.GetAll();

            var toDelete = users
                .Where(u =>
                    u.ST_USUARIO == UserStatus.Removed &&
                    u.DH_TENTATIVA_REMOCAO.HasValue &&
                    u.DH_TENTATIVA_REMOCAO.Value < limitDate)
                .ToList();

            if (toDelete.Count == 0)
            {
                _logger.LogInformation("Nenhum usuário antigo para limpar.");
                return;
            }

            _logger.LogInformation("Removendo {Count} usuários antigos do banco.", toDelete.Count);

            foreach (var user in toDelete)
            {
                await store.Remove(user.ID_USUARIO_SASE);
            }
        }

        private async Task WaitUntilNextRun(CancellationToken token)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Today.AddHours(3);

            if (now >= nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;

            await Task.Delay(delay, token);
        }
    }
}
