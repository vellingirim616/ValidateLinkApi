using LinksApi.Models;

namespace LinksApi.Services;

public class BackgroundValidationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundValidationService> _logger;

    public BackgroundValidationService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundValidationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Validation Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        _logger.LogInformation("Background Validation Service stopped");
    }
}
