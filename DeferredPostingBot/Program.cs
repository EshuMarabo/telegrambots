using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DefferedPosting
{
    public class Program
    {

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void Main(string[] args)
        {
            IHost host = CreateHostBuilder(args).Build();
            host.Run();
        }
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    string BotToken = hostContext.Configuration.GetSection("BotSettings:TelegramBotToken").Value;
                    string ConnectionString = hostContext.Configuration.GetSection("BotSettings:DBConnectionString").Value;
                    services.AddHostedService(sh=>new BotService(BotToken,ConnectionString));
                    //services.AddHostedService<BotService>();

                });
    }
}
