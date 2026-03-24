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
                    var store = scope.ServiceProvider.GetRequiredService<FileUserStore>();
                    var userService = scope.ServiceProvider.GetRequiredService<UserService>();

                    var users = await store.GetAll();

                    var expired = users
                        .Where(x => x.Status == UserStatus.Active &&
                                    x.ExpiresAt <= DateTime.UtcNow)
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
                            _logger.LogInformation($"Removendo usuário expirado: {user.Email}");
                            await userService.Remove(user.Id);
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

        private async Task CleanupOldRemovedUsers(FileUserStore store)
        {
            var retentionDays = _config.GetValue<int>("RetentionDays", 90);
            var limitDate = DateTime.UtcNow.AddDays(-retentionDays);

            var users = await store.GetAll();

            var toDelete = users
                .Where(u =>
                    u.Status == UserStatus.Removed &&
                    u.LastRemovalAttempt.HasValue &&
                    u.LastRemovalAttempt.Value < limitDate)
                .ToList();

            if (toDelete.Count == 0)
            {
                _logger.LogInformation("Nenhum usuário antigo para limpar.");
                return;
            }

            _logger.LogInformation("Removendo {Count} usuários antigos do JSON.", toDelete.Count);

            var remaining = users.Except(toDelete).ToList();

            await store.SaveAll(remaining);
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
