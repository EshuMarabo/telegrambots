using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace DefferedPosting
{
    public class BotService : BackgroundService
    {
        private DefferedPostingBot bot;

        public BotService(string BotToken, string DBConnectionString)
        {
            bot = new DefferedPostingBot(BotToken, DBConnectionString);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
           
            bot.Start();
            await Task.Delay(100);
        }


    }
}
