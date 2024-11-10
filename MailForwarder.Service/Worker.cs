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
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
            }

            var mailForwarder = _serviceProvider.GetService<MailForwarder.Lib.MailForwarder>();
            mailForwarder?.ProcessMails();

            await Task.Delay(60000, stoppingToken);
        }
    }
}
