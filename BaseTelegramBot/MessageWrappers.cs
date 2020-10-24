using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using DataBaseWorker;
using Telegram.Bot.Types.ReplyMarkups;

namespace BaseTelegramBot
{
    public class WrapperFactory
    {

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private TelegramBotClient client;
        private DBWorker dBWorker;
        private string bot_token;
        public WrapperFactory(TelegramBotClient client, DBWorker dBWorker,string bot_token)
        {
            this.client = client;
            this.dBWorker = dBWorker;
            this.bot_token = bot_token;
        }
        private BaseWrapper ApplySettings(BaseWrapper wrapper)
        {
            wrapper.client = this.client;
            wrapper.logger = this.logger;
            wrapper.bot_token = this.bot_token;
            wrapper.DBWorker = this.dBWorker;
            return wrapper;
        }
        public IMessageToSend CreateAlbum(ChatId RecipientId, List<string> media_ids, string text, int inReplyOf = 0,
            InlineKeyboardMarkup keyboardMarkup = null)
        {
            return ApplySettings(new MegiaGroupWrapper(RecipientId, media_ids, text, inReplyOf: inReplyOf));
        }

        public IMessageToSend CreatePhoto(ChatId RecipientId, string media_id, string text, int inReplyOf = 0, 
            InlineKeyboardMarkup keyboardMarkup = null)
        {
            return ApplySettings(new PhotoWrapper(RecipientId, media_id, text, inReplyOf: inReplyOf));
        }
        public IMessageToSend CreateAnimation(ChatId RecipientId, string media_id, string text, int inReplyOf = 0)
        {
            return ApplySettings(new AnimationWrapper(RecipientId, media_id, text, inReplyOf: inReplyOf));
        }
        public IMessageToSend CreateStiker(ChatId RecipientId, string media_id, int inReplyOf = 0)
        {
            return ApplySettings(new StickerWrapper(RecipientId, media_id, inReplyOf: inReplyOf));
        }
        public IMessageToSend CreateMessage(Message message, string text, InlineKeyboardMarkup keyboardMarkup = null)
        {
            return ApplySettings(new BaseWrapper(message, text, keyboardMarkup:keyboardMarkup));
        }
        public IMessageToSend CreateMessage(long RecipientId, string text, int inReplyOf = 0, InlineKeyboardMarkup keyboardMarkup = null)
        {
            return ApplySettings(new BaseWrapper(RecipientId, text, inReplyOf, keyboardMarkup));
        }
        public IMessageToSend CreateMessage(ChatId RecipientId, string text, int inReplyOf = 0, IReplyMarkup keyboardMarkup = null)
        {
            return ApplySettings(new BaseWrapper(RecipientId, text, inReplyOf, keyboardMarkup));
        }
        public IMessageToSend CreateMessageForwarding(ChatId To, ChatId From, int Id)
        {
            return ApplySettings(new ForwardingWrapper(From, To, Id));
        }
        public IMessageToSend CreateKeyboardEditingRequest(long RecipientId, int MessageId, InlineKeyboardMarkup keyboardMarkup)
        {
            return ApplySettings(new KeyboardEditingWrapper(RecipientId, MessageId, keyboardMarkup));
        }
        
    }
    class BaseWrapper : IMessageToSend
    {
        public int delay = 0;
        internal object locker = new object();
        public Message linkedMessage;
        public Message result;
        public IReplyMarkup keyboardMarkup;
        public string bot_token;
        public DBWorker DBWorker;
        public bool finished = false;
        public NLog.Logger logger;
        public Task<Message> SendingTask;
        public Task FinishingTask;
        public long _ChatID;
        internal ChatId ChatID;
        internal string text;
        internal int inReplyOf;
        public TelegramBotClient client;
        internal CancellationToken cancellation = default;
        internal bool disableNotification = false;
        public BaseWrapper(Message message, string text, bool reply = false, IReplyMarkup keyboardMarkup =null)
        {
            try
            {
                this.keyboardMarkup = keyboardMarkup;
                if (reply)
                    this.inReplyOf = message.MessageId;
                else
                    this.inReplyOf = 0;
                this.text = text;
                this._ChatID = message.Chat.Id;
                this.ChatID = new ChatId(_ChatID);
            }
            catch
            {

            }

        }
        public BaseWrapper(long recip, string text, int inReplyOf = 0, IReplyMarkup keyboardMarkup = null)
        {
            try
            {
                this.keyboardMarkup = keyboardMarkup;
                this.inReplyOf = inReplyOf;
                this.text = text;
                this._ChatID = recip;
                this.ChatID = new ChatId(recip); ;
            }
            catch
            {

            }
        }
        public BaseWrapper(ChatId recip, string text, int inReplyOf = 0, IReplyMarkup keyboardMarkup = null)
        {
            try
            {
                this.delay = 0;
                this.keyboardMarkup = keyboardMarkup;
                this.inReplyOf = inReplyOf;
                this.text = text;
                this.ChatID = recip;
                this._ChatID = recip.Identifier;
            }
            catch
            {

            }
        }
        public bool isFinished()
        {
            lock (locker)
                return finished;
        }
        public virtual void Send()
        {
            SendingTask = client.SendTextMessageAsync(ChatID, text,
                replyToMessageId: inReplyOf,
                cancellationToken: cancellation,
                disableNotification: disableNotification, parseMode: ParseMode.Html,replyMarkup:keyboardMarkup);
            SendingTask.ContinueWith(LogResult);
        }

        public void SetFinished()
        {
            FinishingTask = Task.Delay(delay);
            FinishingTask.ContinueWith(setFinished);
        }
        public void setFinished(Task t)
        {
            lock (locker)
                finished = true;
        }
        public long GetChatId()
        {
            return _ChatID;
        }

        public void AddLinkedMessage(object? message)
        {
            linkedMessage = message as Message;
        }
        public virtual void LogResult(Task<Message> t)
        {
            lock (locker)
            {
                SetFinished();
                if (t.IsCompletedSuccessfully)
                {
                    this.result = t.Result;
                    logToDB();
                }    
            }

        }
        public virtual void logToDB()
        {
            long? pairChatId = null;
            long? pairMessageId = null;
            if (linkedMessage != null)
            {
                pairChatId = linkedMessage.Chat.Id;
                pairMessageId = linkedMessage.MessageId;
            }
            if (result != null)
            {
                DBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, result.MessageId,
                    result.Chat.Id, result.From!=null? ((long?)result.From.Id) : null, result.ReplyToMessage == null ? 0 : result.ReplyToMessage.MessageId,
                    bot_token, result.Text, result.Caption, result.Photo != null ? result.Photo.Last().FileId : null,
                    result.MediaGroupId, true, pairChatId, pairMessageId, ButtonsData: CommonFunctions.CteateReturningValuesFromKeyboard(result.ReplyMarkup));
            }

        }
    }

    class KeyboardEditingWrapper : BaseWrapper
    {
        public int message_id;
        public KeyboardEditingWrapper(long recipient, int message_id,InlineKeyboardMarkup keyboardMarkup) 
            :base(recipient,string.Empty,keyboardMarkup: keyboardMarkup)
        {
            delay = 1000;
            this.message_id = message_id;
        }

        public override void Send()
        {
            InlineKeyboardMarkup mark = this.keyboardMarkup as InlineKeyboardMarkup;
            if (mark != null)
            {
                SendingTask = client.EditMessageReplyMarkupAsync(this.ChatID, this.message_id, mark);
                SendingTask.ContinueWith(LogResult);
            }

        }
    }
    class ForwardingWrapper : BaseWrapper
    {

        private ChatId From;
        private ChatId To;
        private int MessageID;
        public ForwardingWrapper(ChatId From, ChatId To, int MessageId) :
            base(To, "")
        {
            this.From = From;
            this.To = To;
            this.MessageID = MessageId;
            this.ChatID = To.Identifier;
        }
        public override void Send()
        {
            SendingTask = this.client.ForwardMessageAsync(To, From, MessageID);
            SendingTask.ContinueWith(LogResult);
        }
    }
    class MegiaGroupWrapper : BaseWrapper
    {
        public new Task<Message[]> SendingTask;
        public new Message[] result;
        List<IAlbumInputMedia> media = new List<IAlbumInputMedia>();
        public MegiaGroupWrapper(ChatId To, List<string> InputMediaIds, string text, int inReplyOf,
            InlineKeyboardMarkup keyboardMarkup = null) :
            base(To, "",keyboardMarkup:keyboardMarkup)
        {
            this.ChatID = To;
            this._ChatID = To.Identifier;
            this.inReplyOf = inReplyOf;
            this.text = text;
            int i = 0;
            foreach (string InputMediaId in InputMediaIds)
            {
                InputMediaPhoto inputMediaPhoto = new InputMediaPhoto(new InputMedia(InputMediaId));
                if (i == 0)
                {
                    inputMediaPhoto.Caption = text;
                    inputMediaPhoto.ParseMode = ParseMode.Html;
                }
                media.Add(inputMediaPhoto);
                i++;
            }
        }
        public virtual void FinishSending(Task<Message[]> t)
        {
            lock (locker)
            {

                SetFinished();
                if (t.IsCompletedSuccessfully)
                {
                    this.result = t.Result;
                    logToDB();
                }
            }
        }
        public override void logToDB()
        {
            long? pairChatId = null;
            long? pairMessageId = null;
            if (linkedMessage != null)
            {
                pairChatId = linkedMessage.Chat.Id;
                pairMessageId = linkedMessage.MessageId;
            }
            if (result != null)
            {
                foreach (Message mess in result)
                {
                    DBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, mess.MessageId,
                        mess.Chat.Id, mess.From!=null?((long?)mess.From.Id):null, mess.ReplyToMessage == null ? 0 : mess.ReplyToMessage.MessageId,
                        bot_token, mess.Text, mess.Caption, mess.Photo != null ? mess.Photo.Last().FileId : null,
                        mess.MediaGroupId, true, pairChatId, pairMessageId,ButtonsData: CommonFunctions.CteateReturningValuesFromKeyboard(mess.ReplyMarkup));
                }
            }

        }
        public override void Send()
        {
            SendingTask = client.SendMediaGroupAsync(media, ChatID, replyToMessageId: inReplyOf);
            SendingTask.ContinueWith(FinishSending);
            //SendingTask.Wait();
        }
    }
    class PhotoWrapper : BaseWrapper
    {
        InputOnlineFile inputOnlineFile;
        public PhotoWrapper(ChatId To, string InputMediaId, string text, int inReplyOf,
            InlineKeyboardMarkup keyboardMarkup = null) :
            base(To, "", keyboardMarkup: keyboardMarkup)
        {
            this.ChatID = To;
            this._ChatID = To.Identifier;
            this.inReplyOf = inReplyOf;
            this.text = text;
            inputOnlineFile = new InputOnlineFile(InputMediaId);
        }

        public override void Send()
        {
            SendingTask = client.SendPhotoAsync(ChatID, inputOnlineFile, caption: text,
                replyToMessageId: inReplyOf, parseMode: ParseMode.Html,replyMarkup:keyboardMarkup);
            SendingTask.ContinueWith(LogResult);


        }
    }
    class StickerWrapper : BaseWrapper
    {
        InputOnlineFile inputOnlineFile;
        public StickerWrapper(ChatId To, string InputMediaId, int inReplyOf) :
            base(To, "")
        {
            this.ChatID = To;
            this._ChatID = To.Identifier;
            this.inReplyOf = inReplyOf;
            inputOnlineFile = new InputOnlineFile(InputMediaId);
        }

        public override void Send()
        {
            SendingTask = client.SendStickerAsync(ChatID, inputOnlineFile,
                replyToMessageId: inReplyOf);
            SendingTask.ContinueWith(LogResult);
        }
    }

    class AnimationWrapper : BaseWrapper
    {
        InputOnlineFile inputOnlineFile;
        public AnimationWrapper(ChatId To, string InputMediaId,string text, int inReplyOf) :
            base(To, "")
        {
            this.ChatID = To;
            this._ChatID = To.Identifier;
            this.inReplyOf = inReplyOf;
            this.text = text;
            inputOnlineFile = new InputOnlineFile(InputMediaId);
        }

        public override void Send()
        {
            SendingTask = client.SendAnimationAsync(ChatID, inputOnlineFile,
                replyToMessageId: inReplyOf,caption:text,parseMode: ParseMode.Html);
            SendingTask.ContinueWith(LogResult);
        }
    }
}
