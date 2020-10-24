using BaseTelegramBot;
using DefferedPosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static BaseTelegramBot.BaseBot;

namespace BotManager
{
    class BotManager : BaseBot
    {
        private Regex TokenParsing = new Regex(@"(\d+:.+)");
        private List<BaseBot> bots = new List<BaseBot>();
        private const string DefferedPosting = "Отложенный постинг";
        private const string FeedBack = "Обратная связь";
        private ConcurrentDictionary<long, Mode> mods = new ConcurrentDictionary<long, Mode>();

        public override List<List<string>> MainMenuDescription => new List<List<string>>()
        {
            new List<string>() {DefferedPosting},
            new List<string>() {FeedBack}

        };

        private enum Mode
        {
            Deffered,
            FeedBack
        }

        private enum commands
        {
            ban,
            setgreeting,
            viewgroups,
            push
        }
        public BotManager(string token="", string DBConnectionString="") : base(token, DBConnectionString)
        {
            PrivateChatGreeting = "Добрый день! Это бот-менеджер ботов. Здесь вы можете создать себе копию одного из доступных ботов и безвозмездно пользоваться ей." +
                "\n\n Выберете тип бота:";
            SupportedCommands = new List<BotCommand>()
            {
                new BotCommand(@"/addbot","^/addbot (.+)$","Введите команду /addbot чтобы добавить бота"),
            };
        }
        public override void Start()
        {
            base.Start();
            RestoreBotsFromDB();
        }
        
        private void RestoreBotsFromDB()
        {
            foreach (var bot_info in dBWorker.get_all_bots())
            {
                try
                {
                    if (Enum.TryParse(bot_info.Item2,out Mode mode))
                    {
                        if (!bot_info.Item1.Equals(this.token))
                            AddBot(bot_info.Item1, mode);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }
            
        }

        private void AddBot(string token, Mode mode)
        {
            logger.Info("Creating new bot. token: " + token);
            dBWorker.get_bot_id(token);
            if (this.bots.FindIndex(bot => bot.token == token)<0)
            {
                BaseBot bot=null;
                switch (mode)
                {
                    case Mode.FeedBack:
                        {
                            logger.Info("Creating new bot. token.");
                            bot = new FeedbackBot.FeedBackBot(token, this.DBConnectionString);
                            dBWorker.add_bot(token, Mode.FeedBack.ToString());
                            break;
                        }
                    case Mode.Deffered:
                        {
                            bot = new DefferedPostingBot(token, this.DBConnectionString);
                            dBWorker.add_bot(token, Mode.Deffered.ToString());
                            break;
                        }
                }
                if (bot != null)
                {
                    bot.Start();
                    this.bots.Add(bot);
                }
            }
        }

        public override void BotOnUpdateRecieved(object sender, UpdateEventArgs updateEventArgs)
        {
            try
            {
                logger.Trace("Update!");
                switch (updateEventArgs.Update.Type)
                {
                    case UpdateType.CallbackQuery:
                        {
                            long chatId = updateEventArgs.Update.CallbackQuery.Message.Chat.Id;
                            switch (updateEventArgs.Update.CallbackQuery.Data)
                            {
                                case DefferedPosting:
                                    {
                                        mods.AddOrUpdate(chatId, Mode.Deffered, (oldKey, oldValue) => Mode.Deffered);
                                        break;
                                    }
                                case FeedBack:
                                    {
                                        mods.AddOrUpdate(chatId, Mode.FeedBack, (oldKey, oldValue) => Mode.FeedBack);
                                        break;
                                    }
                            }
                            CreateUnderChatMenu(chatId, "Для создания бота скопируйте и пришлите токен, выдаваемый @BotFather при создании нового бота. " +
                                "В течении минуты после получения токена, бот запущен и готов к использованию.");
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public override void GroupChatProcessing(Message message, ref bool continuation)
        { 
        
        }

        public override void PrivateChatProcessing(Message message, ref bool continuation)
        {
            try
            {
                base.PrivateChatProcessing(message, ref continuation);
                long chatId = message.Chat.Id;
                if (message.Text != null && message.Text.ToLower().Equals(CancelCommand.ToLower()))
                {
                    ClearUnderChatMenu(chatId, "Принято!");
                    SendDefaultMenu(chatId);
                    mods.TryRemove(chatId, out Mode m);
                    return;
                }
                if (mods.TryGetValue(chatId, out Mode mode))
                {
                    Match TokenChecking = TokenParsing.Match(message.Text);
                    if (TokenChecking.Success)
                    {
                        logger.Info("Добавляем бота с токеном " + TokenChecking.Groups[1].Value);
                        AddBot(TokenChecking.Groups[1].Value, mode);
                        ClearUnderChatMenu(message.Chat.Id, "Бот успешно создан!");
                        SendDefaultMenu(message.Chat.Id);
                        return;
                    }
                    else
                    {
                        sender_to_tg.Put(factory.CreateMessage(message.Chat.Id, "Пришлите пожалуйста корректный токен в формате\n\n 1234567:AAAAAAAdsdd"));
                        return;
                    }
                }
                SendDefaultMenu(chatId);
            }
            catch (Exception ex) { logger.Error(ex); }

        }

        public override bool? ParseStartStopCommands(Message message, ref bool continuation)
        {
            bool? is_alive = null;
            if (message.Text != null)
            {
                string text = message.Text.ToLower();
                if (StartCommands.Contains(text))
                {
                    is_alive = true;
                    continuation = false;
                    if (PrivateChatGreeting != null && !PrivateChatGreeting.Equals(string.Empty))
                        SendDefaultMenu(message.Chat.Id);
                }
                else if (StopCommands.Contains(text))
                {
                    is_alive = false;
                    continuation = false;
                }
            }
            return is_alive;
        }
    }
}

