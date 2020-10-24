using BaseTelegramBot;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FeedbackBot
{
    public class FeedBackBot : BaseBot
    {
        private enum commands
        {
            ban,
            setgreeting,
            viewgroups,
            push
        }

        public FeedBackBot(string token, string DBConnectionString) : base(token, DBConnectionString)
        {
            PrivateChatGreeting = "Добрый день! Напишите боту сообщение и мы его прочтем.";
            SupportedCommands = new List<BotCommand>()
            {
                new BotCommand(@"/ban","^/ban$","Ответьте командой /ban на сообщение, присланное ботом, чтобы заблокировать передачу сообщений от пользователя"),
                new BotCommand(@"/setgreeting", @"^/setgreeting (.+)$", "Отправьте команду вида \"/setgreeting Привет!\"," +
                " чтобы установить приветствие, отправляемое новым пользователям."),
                new BotCommand("/setgroup", @"^/setgroup (.+)$", "Отправьте команду вида \"/setgroup политота\"," +
                " чтобы присвоить какую-либо группу написавшему пользователю."),
                new BotCommand("/viewgroups", @"^/viewgroups$", "Отправьте команду вида /viewgroups" +
                " чтобы посмотреть все созданные группы"),
                new BotCommand("/push", @"^/push$", "Отправьте команду вида /push или \"/push политота\" в ответе на любое сообщение, чтобы разослать его всем членам группы." +
                " чтобы посмотреть все созданные группы"),
                new BotCommand("/push", @"^/push (.+)$", "Отправьте команду вида /push или \"/push политота\" в ответе на любое сообщение, чтобы разослать его всем членам группы." +
                " чтобы посмотреть все созданные группы")
            };
        }

        public override void OnMessageReceivedAction(Task<bool> task, object? state)
        {
            if (task.Result)
            {
                Message message = state as Message;
                if (ChatIsGroup(message))
                {
                    Message inReplyOf = message.ReplyToMessage;

                    if (inReplyOf != null)
                    {
                        long? targer = dBWorker.get_pair_chat_id(inReplyOf.Chat.Id, inReplyOf.MessageId, token);
                        if (targer != null)
                        {
                            IMessageToSend MyMess = RecreateMessage(message, (long)targer);
                            sender_to_tg.Put(MyMess);
                        }
                            
                    }                    
                }
                else if (ChatIsPrivate(message))
                {
                    List<long> groups = dBWorker.get_active_groups();
                    string appendix = "\n\n#id{0}\n<a href =\"tg://user?id={0}\">{1}</a>";
                    foreach (long group_id in groups)
                    {
                        IMessageToSend MyMess = RecreateMessage(message, group_id, string.Format(appendix, message.From.Id, message.From.FirstName));
                        if (MyMess!=null)
                            MyMess.AddLinkedMessage(message);
                        sender_to_tg.Put(MyMess);
                    }
                       
                }
            }
        }

        public override void GroupChatProcessing(Message message, ref bool continuation)
        {
            bool? is_alive = null;
            if (message.NewChatMembers!=null&&message.NewChatMembers.Contains(me, new CommonFunctions.UserComparer()))
            {
                is_alive = true;
            }
            if (message.LeftChatMember!=null&&message.LeftChatMember.Id == me.Id)
            {
                is_alive = false;
            }
            add_chat(message.Chat, message.From, is_alive);

            if (message.Text == null) return;

            ParseHelpCommand(message, ref continuation);
            Message inReplyOf = message.ReplyToMessage;
            if (message.Text.ToLower().Equals("/ban"))
            {
                if (inReplyOf != null)
                {
                    long? chat_id = dBWorker.get_pair_chat_id(inReplyOf.Chat.Id, inReplyOf.MessageId, token);
                    long? user_id = null;
                    if (chat_id != null)
                    {
                        user_id = dBWorker.get_user_by_chat((long)chat_id);
                    }
                    if (user_id != null)
                    {
                        dBWorker.ban_user((long)user_id, (long)chat_id, token);
                    }
                    continuation = false;
                }
                else
                {
                    sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), "Некорректная комманда, см. /help", message.MessageId));
                }

            }

            Regex reg = new Regex(@"^/setgreeting (.+)$");
            Match match = reg.Match(message.Text);
            if (match.Success)
            {
                PrivateChatGreeting = match.Groups[1].Value;
                continuation = false;
            }

            reg = new Regex(@"^/setgroup (.+)$");
            match = reg.Match(message.Text);
            if (match.Success)
            {
                if (message.ReplyToMessage != null)
                {
                    string group = match.Groups[1].Value;
                    long? chat_id = dBWorker.get_pair_chat_id(message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId, token);
                    if (chat_id != null)
                    {
                        dBWorker.set_group((long)chat_id, group, token);
                    }
                }
                continuation = false;
            }

            reg = new Regex(@"^/viewgroups$");
            match = reg.Match(message.Text);
            if (match.Success)
            {
                List<string> groups = dBWorker.get_groupping(token);
                string text = "";
                foreach (string g in groups)
                {
                    text += g + "\n\n";
                }
                sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), text));
                continuation = false;
            }

            reg = new Regex(@"^/push$");
            match = reg.Match(message.Text);
            if (match.Success)
            {
                Message rep = message.ReplyToMessage;
                if (rep != null)
                {
                    List<long> groups = dBWorker.get_active_private_chats(token);
                    foreach (long g in groups)
                    {
                        if (!dBWorker.check_user_ban((int)g, g, token))
                            sender_to_tg.Put(factory.CreateMessage(new ChatId(g), CommonFunctions.TextFormatingRecovering(rep.Entities,rep.Text)));
                    }
                }
                else
                {
                    sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), "Не хватает прав администратора," +
                        " чтобы прочесть сообщение, которое нужно разослать. Рассылаемое сообщение должно быть отправлено " +
                        "в группу ПОСЛЕ назначения бота администратором группы." +
                        "Если прав у бота хватает, проверьте корректность команды см. /help", message.MessageId));
                }
                continuation = false;
            }

            reg = new Regex(@"^/push (.+)$");
            match = reg.Match(message.Text);
            if (match.Success)
            {
                Message rep = message.ReplyToMessage;
                if (rep != null)
                {
                    List<long> groups = dBWorker.get_active_private_chats(token, match.Groups[1].Value);
                    foreach (long g in groups)
                    {
                        if (!dBWorker.check_user_ban((int)g, g,token))
                            sender_to_tg.Put(factory.CreateMessage(new ChatId(g), CommonFunctions.TextFormatingRecovering(rep.Entities, rep.Text)));
                    }
                }
                else
                {
                    sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), "Не хватает прав администратора," +
                        " чтобы прочесть сообщение, которое нужно разослать. Рассылаемое сообщение должно быть отправлено " +
                        "в группу ПОСЛЕ назначения бота администратором группы." +
                        "Если прав у бота хватает, проверьте корректность команды см. /help", message.MessageId));
                }
                continuation = false;
            }
        }

        public override void PrivateChatProcessing(Message message, ref bool continuation)
        {
            base.PrivateChatProcessing(message, ref continuation);
            continuation = continuation && !dBWorker.check_user_ban(message.From.Id, message.Chat.Id, token);
        }
    }
}

