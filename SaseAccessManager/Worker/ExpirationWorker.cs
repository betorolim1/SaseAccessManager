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

            var before = users.Count;

            users = users.Where(u =>
                u.Status != UserStatus.Removed ||
                (u.LastRemovalAttempt != null && u.LastRemovalAttempt >= limitDate)
            ).ToList();

            var removed = before - users.Count;

            if (removed > 0)
            {
                _logger.LogInformation($"Limpando {removed} usuários antigos do JSON.");
                await store.SaveAll(users);
            }
        }

        private async Task WaitUntilNextRun(CancellationToken token)
        {
            var now = DateTime.Now;
            var nextRun = DateTime.Now.AddMinutes(1); //DateTime.Today.AddDays(1).AddHours(3); // 03:00

            var delay = nextRun - now;

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.FromHours(24);

            await Task.Delay(delay, token);
        }
    }
}
