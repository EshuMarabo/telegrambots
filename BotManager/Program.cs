using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BotManager
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
                    var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
                    services.AddHostedService(sh=>new BotService(cfg.GetSection("BotSettings:TelegramBotToken").Value,
                        cfg.GetSection("BotSettings:DBConnectionString").Value));

                });
    }
}
