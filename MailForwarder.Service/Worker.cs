using MailForwarder.Lib;
using Microsoft.Extensions.Options;

namespace MailForwarder.Service;

public class Worker : BackgroundService
{
    private IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private MailForwarderConfiguration _configuration;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger, IOptions<MailForwarderConfiguration> configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Worker start at: {time}", DateTimeOffset.Now);

            int failCounter = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                }

                try
                {
                    var mailForwarder = _serviceProvider.GetService<MailForwarder.Lib.MailForwarder>();
                    mailForwarder?.ProcessMails();

                    if (_configuration.PushUrlOk != null)
                        await new HttpClient().GetAsync(_configuration.PushUrlOk.Replace("{msg}", "ProcessMails success"));
                }
                catch (Exception ex)
                {
                    failCounter++;
                    _logger.LogError(ex, "ProcessMails failed!");
                    if (failCounter % 3 == 0 && _configuration.PushUrlError != null)
                    {
                        await new HttpClient().GetAsync(_configuration.PushUrlError.Replace("{msg}", $"ProcessMails failed! ({failCounter})"));
                    }

                }

                await Task.Delay(60000, stoppingToken);
            }

            _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker cancled at: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker failed!");
            Environment.Exit(1);
        }
    }
}
