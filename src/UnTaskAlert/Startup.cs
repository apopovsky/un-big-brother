using System;
using System.Net.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

[assembly: FunctionsStartup(typeof(UnTaskAlert.MyNamespace.Startup))]
namespace UnTaskAlert
{
    namespace MyNamespace
    {
        public class Startup : FunctionsStartup
        {
            public override void Configure(IFunctionsHostBuilder builder)
            {
                builder.Services.AddTransient<IMonitoringService, MonitoringService>();
                builder.Services.AddTransient<IReportingService, ReportingService>();
                builder.Services.AddTransient<INotifier, TelegramNotifier>();
                builder.Services.AddTransient<IMailSender, MailSender>();
                builder.Services.AddTransient<IBacklogAccessor, BacklogAccessor>();
                builder.Services.AddTransient<IQueryBuilder, QueryBuilder>();
                builder.Services.AddTransient<ICommandProcessor, CommandProcessor>();
                builder.Services.AddTransient<IDbAccessor, DbAccessor>();
                builder.Services.AddTransient<ITelegramBotProvider, TelegramBotProvider>();
                builder.Services.AddTransient<IPinGenerator, PinGenerator>();
                builder.Services.AddOptions<Config>()
                    .Configure<IConfiguration>((settings, configuration) =>
                    {
                        configuration.Bind(settings);
                    });
                var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                builder.Services.AddSingleton<ITelegramBotClient>(provider => new TelegramBotClient(provider.GetService<IOptions<Config>>().Value.TelegramBotKey, httpClient));
                builder.Services.AddSingleton<ITelegramBotListener, TelegramBotListener>();
            }
        }
	}
}
