using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

using UnTaskAlert.MyNamespace;

using UnTaskAlert;
using UnTaskAlert.Functions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(Configure)
    .Build();

host.Run();

void Configure(HostBuilderContext context, IServiceCollection services)
{
    services.AddLogging(builder => builder.AddDebug().AddConsole());
    services.AddTransient<IMonitoringService, MonitoringService>();
    services.AddTransient<IReportingService, ReportingService>();
    services.AddTransient<INotifier, TelegramNotifier>();
    services.AddTransient<IMailSender, MailSender>();
    services.AddTransient<IBacklogAccessor, BacklogAccessor>();
    services.AddTransient<IQueryBuilder, QueryBuilder>();
    services.AddTransient<ICommandProcessor, CommandProcessor>();
    services.AddTransient<IDbAccessor, DbAccessor>();
    services.AddTransient<ITelegramBotProvider, TelegramBotProvider>();
    services.AddTransient<IPinGenerator, PinGenerator>();
    services.AddOptions<Config>()
        .Configure<IConfiguration>((settings, configuration) =>
        {
            configuration.Bind(settings);
        });
    services.AddHttpClient();
    services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient(provider.GetService<IOptions<Config>>().Value.TelegramBotKey, provider.GetService<HttpClient>()));
    services.AddSingleton<ITelegramBotListener, TelegramBotListenerFunction>();
}
