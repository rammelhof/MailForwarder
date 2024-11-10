namespace MailForwarder.Service;

public class Worker : BackgroundService
{
    private IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Worker start at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                }

                var mailForwarder = _serviceProvider.GetService<MailForwarder.Lib.MailForwarder>();
                mailForwarder?.ProcessMails();

                await Task.Delay(30000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);
            Environment.Exit(1);
        }
    }
}
