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
            _logger.LogInformation("Worker interval: {interval}s", _configuration.CheckInterval / 1000);
            

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
                    var result = mailForwarder?.ProcessMails();

                    if (!(result?.IsSuccess ?? false))
                    {
                        throw new Exception("ProcessMails failed!?");
                    }

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

                await Task.Delay(_configuration.CheckInterval, stoppingToken);
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
