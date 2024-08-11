using DualDrill.Engine.Services;

namespace DualDrill.Server;

sealed class SingletonHostedService<T>(T Service) : BackgroundService
    where T : IHostableBackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Service.ExecuteAsync(stoppingToken);
}
