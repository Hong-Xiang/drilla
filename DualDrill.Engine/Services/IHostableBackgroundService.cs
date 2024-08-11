namespace DualDrill.Engine.Services;

public interface IHostableBackgroundService
{
    Task ExecuteAsync(CancellationToken stoppingToken);
}

