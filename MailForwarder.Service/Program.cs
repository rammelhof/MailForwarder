using MailForwarder.Service;
using Serilog;

var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", true)
        .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddTransient<MailForwarder.Lib.MailForwarder>();
builder.Services.AddTransient<MailForwarder.Lib.SRS>();
builder.Services.Configure<MailForwarder.Lib.MailForwarderConfiguration>(configuration.GetSection("MailForwarderConfiguration"));
builder.Services.AddSerilog();
var host = builder.Build();
host.Run();
