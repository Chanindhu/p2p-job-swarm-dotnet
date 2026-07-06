using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Swarm.Server.Data;

namespace Swarm.Server.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<CleanupService> _logger;

        public CleanupService(IServiceProvider sp, ILogger<CleanupService> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var cutoff = DateTime.UtcNow.AddMinutes(-5);
                    var zombies = await db.Clients
                        .Where(c => c.LastSeenUtc < cutoff)
                        .ToListAsync(stoppingToken);

                    if (zombies.Count > 0)
                    {
                        db.Clients.RemoveRange(zombies);
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Pruned {Count} stale clients.", zombies.Count);
                    }
                }
                catch (Exception ex)
                {
                    // Happens if the schema isn't ready yet; just skip this cycle.
                    _logger.LogDebug(ex, "Cleanup skipped (likely DB not created yet).");
                }

                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (TaskCanceledException) { }
            }
        }
    }
}
