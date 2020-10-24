using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FeedbackBot
{
    public class Program
    {

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    string BotToken = hostContext.Configuration.GetSection("BotSettings:TelegramBotToken").Value;
                    string ConnectionString = hostContext.Configuration.GetSection("BotSettings:DBConnectionString").Value;
                    services.AddHostedService(sh=>new FeedBackBot(BotToken,ConnectionString));

                });
    }
}
