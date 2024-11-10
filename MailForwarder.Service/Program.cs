using System.Reflection;
using MailForwarder.Service;
using Serilog;
using Serilog.Events;

string exeDir = System.AppContext.BaseDirectory;
Directory.SetCurrentDirectory(exeDir);


var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(Path.Combine(exeDir, "appsettings.json"))
        .AddJsonFile(Path.Combine(exeDir, $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json"), true)
        .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MailForwarder";
});
builder.Services.AddHostedService<Worker>();
builder.Services.AddTransient<MailForwarder.Lib.MailForwarder>();
builder.Services.AddTransient<MailForwarder.Lib.SRS>();
builder.Services.Configure<MailForwarder.Lib.MailForwarderConfiguration>(configuration.GetSection("MailForwarderConfiguration"));
builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
