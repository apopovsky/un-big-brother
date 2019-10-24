using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                builder.Services.AddTransient<INotifier, TelegramNotifier>();
                builder.Services.AddTransient<IMailSender, MailSender>();
                builder.Services.AddTransient<IBacklogAccessor, BacklogAccessor>();
                builder.Services.AddOptions<Config>()
                    .Configure<IConfiguration>((settings, configuration) =>
                    {
                        configuration.Bind(settings);
                    });
            }
        }
    }
}
