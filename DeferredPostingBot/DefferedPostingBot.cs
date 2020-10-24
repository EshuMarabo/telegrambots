using BaseTelegramBot;
using DataBaseWorker;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static BaseTelegramBot.BaseBot;

namespace DefferedPosting
{
    public class DefferedPostingBot : BaseBot
    {
        private System.Timers.Timer timer = new System.Timers.Timer(5000) { AutoReset = true};
        
        private const string deffer = "deffer";
        private const string publicNow = "public";
        private const string like1 = "👍👎";
        private const string like2 = "🔥💩";
        private const string reactionsYes = "reactions_yes";
        private const string reactionsNo = "reactions_no";
        private const string CreatePostCommand = "Создать пост";
        private const string CreateRePostCommand = "Создать репост";
        private const string DefferedPostsCommand = "Отмена отложенных";
        private const string EditPostsCommand = "Редактировать";
        private const string AddChannelCommand = "Добавить канал";
        private Regex ReactionReg = new Regex("^like_(.+)$");
        private Regex TaskReg = new Regex("^task_(.+)$");
        private ConcurrentDictionary<long, Mode> WorkModes = new ConcurrentDictionary<long, Mode>();
        private ConcurrentDictionary<long, int> Stages = new ConcurrentDictionary<long, int>();
        private enum Mode
        {
            NoMode,
            PostCreation,
            RePostCreation,
            PostEditing,
            DefferedManaging,
            ChannelAdding
        }

        public override List<List<string>> MainMenuDescription => new List<List<string>>()
        {
            new List<string>() {CreatePostCommand},
            new List<string>() {CreateRePostCommand},
            new List<string>() {DefferedPostsCommand},
            //new List<string>() { EditPostsCommand},
            new List<string>() { AddChannelCommand }
        };

        private enum commands
        {
            ban,
            setgreeting,
            viewgroups,
            push
        }

        public DefferedPostingBot(string token = "", string DBConnectionString = "") : base(token, DBConnectionString)
        {
            this.PrivateChatGreeting = "Здесь вы можете создавать и откладывать посты, опросы и репосты";
            timer.Elapsed += OnTimerAction;
            timer.Start();
        }

        private List<List<string>> ReactionsParser(string reactions)
        {
            List<List<string>> result = new List<List<string>>();
            Regex reg = new Regex("([.+])");
            Match match = reg.Match(reactions);
            List<string> row = new List<string>();
            if (match.Success)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    row.Add(match.Groups[i].Value);
                }
            }
            else
            {
                for (int i = 0; i < reactions.Length;i+=2)
                {
                    row.Add(new string (new[] { reactions[i], reactions[i + 1] }));
                }
                result.Add(row);
            }
            return result;
        }

        private List<List<string>> GetReactionsTexts(List<DataBaseWorker.Reaction> reactions)
        {
            List<List<string>> result = new List<List<string>>();
            List<string> row = new List<string>();
            foreach (Reaction reaction in reactions)
            {
                row.Add(reaction.reaction_counter!=0?reaction.reaction_text + reaction.reaction_counter.ToString():reaction.reaction_text);
            }
            result.Add(row);
            return result;
        }

        private List<List<string>> GetReactionsData(List<DataBaseWorker.Reaction> reactions)
        {
            List<List<string>> result = new List<List<string>>();
            List<string> row = new List<string>();
            foreach (Reaction reaction in reactions)
            {
                row.Add("like_"+reaction.reaction_id.ToString());
            }
            result.Add(row);
            return result;
        }

        private void OnTimerAction(object sender, ElapsedEventArgs e)
        {
            List<DataBaseWorker.PostingTask> postingTasks  = dBWorker.get_active_tasks(token);
            logger.Debug(string.Format("Finded {0} tasks for posting!", postingTasks.Count));
            foreach (var task  in postingTasks)
            {
                List<DataBaseWorker.Reaction> reactions = dBWorker.get_reactions_by_task(task.id);
                InlineKeyboardMarkup keybpard=CommonFunctions.CreateInlineKeyboard(GetReactionsTexts(reactions), GetReactionsData(reactions));
                string t = Mode.PostCreation.ToString();
                if (Enum.TryParse<Mode>(task.TaskType, out Mode mode))
                {
                    if (Mode.PostCreation.Equals(mode))
                    {
                        if (task.media_id != null)
                        {
                            if (task.media_group_id == null)
                            {
                                sender_to_tg.Put(factory.CreatePhoto(new ChatId(task.TargetChannel), task.media_id, task.caption, keyboardMarkup: keybpard));
                                dBWorker.task_complited(task.SourceChat, task.TargetChannel, token);
                                SetMode(task.SourceChat);
                            }
                            else
                            {
                                List<string> media = dBWorker.get_media_ids(task.media_group_id);
                                string caption = dBWorker.get_caption(task.media_group_id);
                                sender_to_tg.Put(factory.CreateAlbum(new ChatId(task.TargetChannel), media, caption, keyboardMarkup: keybpard));
                                dBWorker.task_complited(task.SourceChat, task.TargetChannel, token);
                                SetMode(task.SourceChat);
                            }
                        }
                        else
                        {
                            sender_to_tg.Put(factory.CreateMessage(task.TargetChannel, task.text, keyboardMarkup: keybpard));
                            dBWorker.task_complited(task.SourceChat, task.TargetChannel, token);
                            SetMode(task.SourceChat);
                        }
                    }
                    else if (Mode.RePostCreation.Equals(mode))
                    {
                        sender_to_tg.Put(factory.CreateMessageForwarding(new ChatId(task.TargetChannel),new ChatId(task.SourceChat),(int)task.SourceMessageId));
                        dBWorker.task_complited(task.SourceChat, task.TargetChannel, token);
                        SetMode(task.SourceChat);
                    }

                }

            }
        }

        public override void BotOnUpdateRecieved(object sender, UpdateEventArgs updateEventArgs)
        {
            try
            {
                switch (updateEventArgs.Update.Type)
                {
                    case UpdateType.CallbackQuery:
                        {
                            long chatId = updateEventArgs.Update.CallbackQuery.Message.Chat.Id;
                            switch (updateEventArgs.Update.CallbackQuery.Data)
                            {
                                case deffer:
                                    {
                                        if (WorkModes.TryGetValue(chatId, out Mode mode))
                                        {
                                            sender_to_tg.Put(factory.CreateMessage(chatId, "Отправьте время публикации в формате, аналогичном текущему времени:" +
                                                "\n\n " + DateTime.Now.ToString()));
                                            if (mode == Mode.PostCreation)
                                            {
                                                Stages.AddOrUpdate(chatId, 5, (oldkey, oldvalue) => 5);
                                            }
                                            else if (mode == Mode.RePostCreation)
                                            {
                                                Stages.AddOrUpdate(chatId, 2, (oldkey, oldvalue) => 2);
                                            }
                                        }
                                        break;
                                    }
                                case publicNow:
                                    {
                                        ClearUnderChatMenu(chatId, "Успешно опубликовано!");
                                        dBWorker.update_task_time(chatId, token, DateTime.UtcNow);
                                        SetMode(chatId);
                                        Stages.TryRemove(chatId, out int v);
                                        SendDefaultMenu(chatId);
                                        break;
                                    }
                                case reactionsNo: 
                                    {
                                        Stages.AddOrUpdate(chatId, 4, (oldkey, oldvalue) => 4);
                                        sender_to_tg.Put(factory.CreateMessage(chatId, "Опубликовать пост сейчас или отложить?",
                                            keyboardMarkup: CreateShedulerQuestion()));
                                        break;
                                    }
                                case reactionsYes:
                                    {
                                        CreateUnderChatReactionsMenu(chatId, "Выберите или введите реакции. " +
                                            "Один символ - один лайк. Если нужно добавить текст введите их в виде [реакция1][реакция2]");
                                        Stages.AddOrUpdate(chatId, 3, (oldkey, oldvalue) => 3);
                                        break;
                                    }
                                case CreatePostCommand:
                                    {
                                        Task.Factory.StartNew(CreatePostCommandButtonReaction, chatId);
                                        break;
                                    }
                                case CreateRePostCommand:
                                    {
                                        Task.Factory.StartNew(CreateRePostCommandButtonReaction, chatId);
                                        break;
                                    }
                                case DefferedPostsCommand:
                                    {
                                        Mode mode = Mode.DefferedManaging;
                                        WorkModes.AddOrUpdate(chatId, mode, (oldkey, oldvalue) => mode);
                                        List<DataBaseWorker.PostingTask> postingTasks = dBWorker.get_future_tasks(token);
                                        if (postingTasks.Count > 0)
                                        {
                                            List<List<string>> texts = new List<List<string>>();
                                            List<List<string>> values = new List<List<string>>();
                                            foreach (var tsk in postingTasks)
                                            {
                                                string ButtonText = string.Format("{0} {1}", tsk.channel_name, tsk.PublishTime);
                                                string ReturnedValue = "task_" + tsk.id.ToString();
                                                texts.Add(new List<string>() { ButtonText });
                                                values.Add(new List<string>() { ReturnedValue });
                                            }
                                            InlineKeyboardMarkup keyboard = CommonFunctions.CreateInlineKeyboard(texts, values);
                                            sender_to_tg.Put(factory.CreateMessage(chatId, "Выберете отложенное сообщение:", keyboardMarkup: keyboard));
                                            CreateUnderChatMenu(chatId, "Или отмените действие");
                                        }
                                        else
                                        {
                                            ClearUnderChatMenu(chatId, "Нет ни одного отложенного сообщения!");
                                            SendDefaultMenu(chatId);
                                            SetMode(chatId);
                                        }

                                        break;
                                    }
                                case EditPostsCommand:
                                    {
                                        Mode mode = Mode.PostEditing;
                                        WorkModes.AddOrUpdate(chatId, mode, (oldkey, oldvalue) => mode);
                                        break;
                                    }
                                case AddChannelCommand:
                                    {
                                        Task.Factory.StartNew(AddChannelButtonReaction, chatId);
                                        break;
                                    }
                                default:
                                    {
                                        if (long.TryParse(updateEventArgs.Update.CallbackQuery.Data, out long channel_id) &&
                                            WorkModes.TryGetValue(chatId, out Mode currentMode))
                                        {
                                            if (currentMode == Mode.PostCreation)
                                            {
                                                if (!Stages.TryGetValue((long)chatId, out int st))
                                                {
                                                    Stages.AddOrUpdate((long)chatId, 1, (oldkey, oldvalue) => 1);
                                                    dBWorker.add_task((long)chatId, channel_id, token, Mode.PostCreation.ToString());
                                                    CreateUnderChatMenu((long)chatId, "Канал выбран! Отправьте боту то, что хотите опубликовать. " +
                                                        "Это может быть всё, что угодно – текст, фото, альбом, видео, даже стикеры.");
                                                }
                                            }
                                            else if (currentMode == Mode.RePostCreation)
                                            {
                                                Stages.AddOrUpdate((long)chatId, 1, (oldkey, oldvalue) => 1);
                                                dBWorker.add_task((long)chatId, channel_id, token, Mode.RePostCreation.ToString());
                                                CreateUnderChatMenu((long)chatId, "Канал выбран! Перешлите боту сообщение, которое хотите репостнуть. " +
                                                    "Это может быть текст, фото, видео, даже стикеры, но не альбом.");
                                            }
                                            break;
                                        }
                                        Match ReactionMatch = ReactionReg.Match(updateEventArgs.Update.CallbackQuery.Data);
                                        if (ReactionMatch.Success && int.TryParse(ReactionMatch.Groups[1].Value, out int ReactionId))
                                        {
                                            dBWorker.count_reaction(ReactionId, updateEventArgs.Update.CallbackQuery.From.Id);
                                            var reactions = dBWorker.get_counted_reactions(ReactionId);
                                            if (reactions != null && reactions.Count > 0)
                                            {
                                                InlineKeyboardMarkup keybpard = CommonFunctions.CreateInlineKeyboard(GetReactionsTexts(reactions),
                                                    GetReactionsData(reactions));
                                                sender_to_tg.Put(factory.CreateKeyboardEditingRequest(updateEventArgs.Update.CallbackQuery.Message.Chat.Id,
                                                    updateEventArgs.Update.CallbackQuery.Message.MessageId, keybpard));
                                            }

                                            break;
                                        }

                                        Match TaskMatch = TaskReg.Match(updateEventArgs.Update.CallbackQuery.Data);
                                        if (TaskMatch.Success && int.TryParse(TaskMatch.Groups[1].Value, out int TaskId))
                                        {
                                            dBWorker.task_rejected(TaskId);
                                            ClearUnderChatMenu(chatId, "Сообщение успешно отменено!");
                                            SendDefaultMenu(chatId);
                                            SetMode(chatId);
                                        }

                                        break;
                                    }

                            }
                            break;
                        }
                }
            }
            catch(Exception ex) { logger.Error(ex); }

        }

        private void AddChannelButtonReaction(object? ChatId)
        {
            long? chatId = ChatId as long?;
            if (chatId == null) return;
            Mode mode = Mode.ChannelAdding;
            WorkModes.AddOrUpdate((long)chatId, mode, (oldkey, oldvalue) => mode);
            CreateUnderChatMenu((long)chatId, "Перешлите сообщение из канала, который хотите добавить, а также добавьте бота в администраторы канала");
        }

        private void SetMode(long chatId, Mode mode = Mode.NoMode)
        {
            WorkModes.AddOrUpdate(chatId, mode, (oldkey, oldvalue) => mode);
        }



        private void CreateUnderChatReactionsMenu(long chatid, string text)
        {
            var rmu = new ReplyKeyboardMarkup();
            rmu.Keyboard = new List<List<KeyboardButton>>() { new List<KeyboardButton>() { new KeyboardButton(like1), new KeyboardButton(like2), new KeyboardButton(CancelCommand) } };
            rmu.ResizeKeyboard = true;
            sender_to_tg.Put(factory.CreateMessage(chatid, text, keyboardMarkup: rmu));
        }


        private void CreatePostCommandButtonReaction(object? ChatId)
        {
            long? chatId = ChatId as long?;
            if (chatId == null) return;
            Mode mode = Mode.PostCreation;
            WorkModes.AddOrUpdate((long)chatId, mode, (oldkey, oldvalue) => mode);
            CreatesReaction((long)chatId);
        }

        private void CreateRePostCommandButtonReaction(object? ChatId)
        {
            long? chatId = ChatId as long?;
            if (chatId == null) return;
            Mode mode = Mode.RePostCreation;
            WorkModes.AddOrUpdate((long)chatId, mode, (oldkey, oldvalue) => mode);
            CreatesReaction((long)chatId);
        }

        private void CreatesReaction(long chatId)
        {
            List<Tuple<long, string>> channels = dBWorker.get_active_channels(token);
            if (channels.Count > 0)
                sender_to_tg.Put(factory.CreateMessage(chatId, "Выберете канал для публикации из списка:", keyboardMarkup: CreateCoseChannelMenu(channels)));
            else
            {
                sender_to_tg.Put(factory.CreateMessage(chatId, "Не добавлено ни одного канала! Для возврата в основное меню нажмите \"Отмена\""));
            }
        }

        public InlineKeyboardMarkup CreateCoseChannelMenu(List<Tuple<long, string>> channels)
        {
            List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();
            foreach (var channel in channels)
            {
                buttons.Add(new List<InlineKeyboardButton>() {InlineKeyboardButton.WithCallbackData(channel.Item2,channel.Item1.ToString()) });
            }
            return new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons);
        }

        public override void OnMessageReceivedAction(Task<bool> task, object? state)
        {
            Message message = state as Message;
            if (task.Result)
            {
                bool IsChatExists = WorkModes.TryGetValue(message.Chat.Id, out Mode mode);
                mode = IsChatExists ? mode : Mode.NoMode;
                switch (mode)
                {
                    case Mode.PostCreation:
                        {
                            if (Stages.TryGetValue(message.Chat.Id, out int stage))
                            {
                                switch (stage)
                                {
                                    case 1://Добавление поста. В ответ отправляем вопрос про реакции.
                                        {
                                            sender_to_tg.Put(factory.CreateMessage(message.Chat.Id,"Добавить реакции?", 
                                                keyboardMarkup: CreateReactionsQuestion()));
                                            Stages.AddOrUpdate(message.Chat.Id, 2, (oldkey, oldvalue) => 2);
                                            break;
                                        }
                                    case 3:
                                        {
                                            List<List<string>> reactions = ReactionsParser(message.Text);
                                            foreach (var row in reactions)
                                                foreach (var r in row)
                                                    dBWorker.get_reaction_id(r,message.Chat.Id,token);
                                            CreateUnderChatMenu(message.Chat.Id, "Реакции установлены!");
                                            Stages.AddOrUpdate(message.Chat.Id, 4, (oldkey, oldvalue) => 4);
                                            sender_to_tg.Put(factory.CreateMessage(message.Chat.Id, "Опубликовать пост сейчас или отложить?",
                                                keyboardMarkup: CreateShedulerQuestion()));
                                            break;
                                        }
                                    case 5:
                                        {
                                            logger.Debug("Parsing DateTime: "+ message.Text);
                                            if (DateTime.TryParse(message.Text, out DateTime dt))
                                            {
                                                dBWorker.update_task_time(message.Chat.Id, token, dt);
                                                ClearUnderChatMenu(message.Chat.Id, string.Format("Создан отложенный пост {0}!",dt));
                                                SetMode(message.Chat.Id);
                                                Stages.TryRemove(message.Chat.Id, out int v);
                                                SendDefaultMenu(message.Chat.Id);
                                            }
                                            else
                                            {
                                                sender_to_tg.Put(factory.CreateMessage(message.Chat.Id, "Не получилось распознать время.\n\nОтправьте время публикации в формате ЧЧ:ММ:СС ДД.ММ.ГГГГ." +
                                                "\n\n 00:00:00 01.01.2020 - ноль часов ноль минут 1 января 2020 года"));
                                            }
                                            break;
                                        }
                                }
                            }
                            break;
                        }
                    case Mode.RePostCreation:
                        {
                            if (Stages.TryGetValue(message.Chat.Id, out int stage))
                            {
                                switch (stage)
                                {
                                    case 1:
                                        {
                                            sender_to_tg.Put(factory.CreateMessage(message.Chat.Id, "Опубликовать репост сейчас или отложить?",
                                                keyboardMarkup: CreateShedulerQuestion()));
                                            break;
                                        }
                                    default:
                                        break;
                                }
                            }
                            break;
                        }
                    case Mode.NoMode:
                        {
                            SendDefaultMenu(message.Chat.Id);
                            break;
                        }
                    case Mode.ChannelAdding:
                        {
                            if (message.ForwardFromChat != null&&
                                message.ForwardFromChat.Type == ChatType.Channel)
                            {
                                dBWorker.add_chat(DateTime.UtcNow, message.ForwardFromChat.Id, token,title: message.ForwardFromChat.Title,
                                    is_channel: true, is_active: true);
                                ClearUnderChatMenu(message.Chat.Id, string.Format("Канал {0} добавлен! \nНе забудьте назначить бота" +
                                    " администратором канала с правами на редактирование и публикацию постов", message.ForwardFromChat.Title));
                                SetMode(message.Chat.Id);
                                SendDefaultMenu(message.Chat.Id);
                            }
                            else
                            {
                                sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id),
                                    "Перешлите сообщение из канала, к которому хотите подключить бота!"));
                            }
                            break;
                        }
                }
            }

        }

        private InlineKeyboardMarkup CreateReactionsQuestion()
        {
            return new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[] 
            { 
                new[] 
                { 
                    InlineKeyboardButton.WithCallbackData("Да", reactionsYes), 
                    InlineKeyboardButton.WithCallbackData("Нет", reactionsNo) 
                } 
            
            });
        }

        private InlineKeyboardMarkup CreateShedulerQuestion()
        {
            return new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Отложить", deffer),
                    InlineKeyboardButton.WithCallbackData("Опубликовать", publicNow)
                }

            });
        }

        public override void GroupChatProcessing(Message message, ref bool continuation)
        {
            continuation = false;
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
                        sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id),
                            PrivateChatGreeting,keyboardMarkup:CommonFunctions.CreateInlineKeyboard(this.MainMenuDescription)));
                }
                else if (StopCommands.Contains(text))
                {
                    is_alive = false;
                    continuation = false;
                }
            }
            return is_alive;
        }

        public override void PrivateChatProcessing(Message message, ref bool continuation)
        {
            if (!WorkModes.TryGetValue(message.Chat.Id, out Mode mode))
            {
                WorkModes.AddOrUpdate(message.Chat.Id, Mode.NoMode, (oldkey, oldvalue) => Mode.NoMode);
            }
            base.PrivateChatProcessing(message, ref continuation);
            if (message.Text!=null&&message.Text.ToLower().Equals(CancelCommand.ToLower()))
            {
                continuation = false;
                dBWorker.task_rejected(message.Chat.Id, token);
                ClearUnderChatMenu(message.Chat.Id, "Принято!");
                SetMode(message.Chat.Id);
                Stages.TryRemove(message.Chat.Id, out int v);
                SendDefaultMenu(message.Chat.Id);
            }
        }
    }
}

