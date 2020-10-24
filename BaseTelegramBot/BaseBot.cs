using System;
using DataBaseWorker;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.Enums;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Collections.Concurrent;
using Telegram.Bot.Types.ReplyMarkups;

namespace BaseTelegramBot
{
	public abstract class BaseBot : BackgroundService
	{
		#region поля

		public class BotCommand
        {
			public string command;
			public Regex CommandReg;
			public string HelpText;

			public BotCommand(string command,string CommandReg, string HelpText)
            {
				this.command = command;
				this.CommandReg = new Regex(CommandReg);
				this.HelpText = HelpText;
			}
        }


		protected const string CancelCommand = "Отмена";
		protected object locker = new object();
		protected NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
		protected TelegramBotClient botClient;
		protected WrapperFactory factory;
		protected Sender sender_to_tg;
		protected DBWorker dBWorker;
		public string token = string.Empty;
		public string DBConnectionString = string.Empty;
		public List<string> StopCommands = new List<string>() { "/stop", "/ban", "/block" };
		public List<string> StartCommands = new List<string>() { "/start" };
		public List<string> HelpCommands = new List<string>() { "/help", "/h" };

		public List<BotCommand> SupportedCommands = new List<BotCommand>();
		protected User me;
		private Regex CommandRegex = new Regex(@"^/\w+.*");
		public string PrivateChatGreeting = "Hello!";
		#endregion
		/// <summary>
		/// Пришедший альбом порождает события, создающие сообщения по числу фотографий в альбоме. 
		/// Чтобы добиться одного пересоздания альбома, используется этот словарь. 
		/// </summary>
		public ConcurrentDictionary<string, string> AlbumSyncDictionary = new ConcurrentDictionary<string, string>();
		public bool ChatIsGroup(Message message)
        {
			return message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup;
		}
		public bool ChatIsPrivate(Message message)
		{
			return message.Chat.Type == ChatType.Private;
		}
		public bool ChatIsChannel(Message message)
		{
			return message.Chat.Type == ChatType.Channel;
		}
		#region конструкторы
		public BaseBot(string token,string DBConnectionString)
		{
            try
            {
				logger.Info("Creating new bot");
				this.token = token;
				this.DBConnectionString = DBConnectionString;
				botClient = new TelegramBotClient(token);

				sender_to_tg = new Sender();
				dBWorker = new DBWorker(DBConnectionString);
				factory = new WrapperFactory(botClient, dBWorker, token);
				EventsInit();
			}
            catch (Exception ex)
            {
				logger.Error(ex);
				throw ex;
            }

		}

		private void EventsInit()
        {
			botClient.OnMessage += BotOnMessageReceived;
			botClient.OnUpdate += BotOnUpdateRecieved;
			botClient.OnMessageEdited += BotOnMessageEdited;
        }
		#endregion


		#region Методы
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			this.Start();
			await Task.Delay(100);
		}
		public virtual void Start()
		{
            try
            {
				sender_to_tg.Start();
				dBWorker.Connect();
				Task<User> meWaiting = botClient.GetMeAsync();
				meWaiting.Wait();
				me = meWaiting.Result;
				dBWorker.add_user(DateTime.UtcNow, botClient.BotId, token, me.Username);
				botClient.StartReceiving();
			}
			catch (Exception ex)
            {
				logger.Error(ex);
            }

			
		}

		public void Stop()
		{
			botClient.StopReceiving();
		}


		protected void add_message(Message message,bool isOutput)
        {
			try
            {
				dBWorker.add_message(message.Date, DateTime.UtcNow, message.MessageId, message.Chat.Id,
					message.From.Id, 
					message.ReplyToMessage!=null?message.ReplyToMessage.MessageId:0, 
					token, CommonFunctions.TextFormatingRecovering(message.Entities, message.Text), 
					CommonFunctions.TextFormatingRecovering(message.CaptionEntities, message.Caption),
					message.Photo!=null? message.Photo.Last().FileId:null,message.MediaGroupId, isOutput,
					ButtonsData: CommonFunctions.CteateReturningValuesFromKeyboard(message.ReplyMarkup));
			}
			catch(Exception ex)
            {
				logger.Error(ex);
            }
		}
		protected void add_user(User user)
		{
			dBWorker.add_user(DateTime.UtcNow, user.Id, this.token, user.Username, user.FirstName, user.LastName);
		}
		protected void add_chat(Chat chat,User user,bool? is_active=null)
		{
			bool is_group = chat.Type == ChatType.Group || chat.Type == ChatType.Supergroup;
			bool is_channel = chat.Type == ChatType.Channel;
			long? user_id=null;
			if (chat.Type == ChatType.Private) user_id = user.Id;
			dBWorker.add_chat(DateTime.UtcNow, chat.Id, this.token, user_id,chat.Title,chat.Username,is_group,is_channel, is_active);
		}
		
		private void LogUserAndMessageToDB(object? mess)
        {
			Message message = mess as Message;
			if (message != null)
            {
				add_user(message.From);
				add_message(message, false);
				logger.Trace(string.Format("New message recieved! sender: {0}, text: {1}",
					message.From.Id, message.Text ?? message.Caption ?? "null"));
			}
            else
            {
				logger.Warn("Uncorrect type given by LogUserAndMessageToDB");
            }
		}

		public virtual void OnMessageReceivedAction(Task<bool> task,object? state)
        {

        }
		public async virtual void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
		{
			try
			{
				Message message = messageEventArgs.Message;
				Task.Factory.StartNew(LogUserAndMessageToDB, message);
				Task.Factory.StartNew(ManageChat, message).ContinueWith(OnMessageReceivedAction, message);
			}
			catch (Exception ex)
			{
				logger.Error(ex);
			}
		}


        protected async virtual void BotOnMessageEdited(object sender, MessageEventArgs messageEventArgs)
		{
			try
			{
				var message = messageEventArgs.Message;
				logger.Trace(string.Format("Message editted! sender: {0}, text: {1}",
					message.From.Id, message.Text ?? message.Caption ?? "null"));
				add_message(message, false);
			}
			catch (Exception ex)
			{
				logger.Error(ex);
			}
		}
		public virtual void BotOnUpdateRecieved(object sender, UpdateEventArgs updateEventArgs)
		{
			try
			{
				logger.Trace("Update!");
				switch (updateEventArgs.Update.Type)
				{
					case UpdateType.CallbackQuery:
						{
							long chatId = updateEventArgs.Update.CallbackQuery.Message.Chat.Id;
							logger.Trace("chatId is "+ chatId.ToString());
							break;
						}
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex);
			}
		}
		private bool CheckCommand(string Text)
        {
			try
			{
				Match match = CommandRegex.Match(Text.ToLower());
				return match.Success;
			}
			catch { return false; }
			
		}
		private bool ManageChat(object? mess)
		{
			Message message = mess as Message;
			bool result = true;
			if (message != null)
            {
				if (message.Chat.Type == ChatType.Private)
				{
					PrivateChatProcessing(message, ref result);
				}
				else if (message.Chat.Type==ChatType.Group|| message.Chat.Type == ChatType.Supergroup)
                {
					GroupChatProcessing(message, ref result);
				}
				return result;
			}
            else
            {
				return result;
            }
		}

		public virtual void PrivateChatProcessing(Message message, ref bool continuation)
        {
			continuation = true;
			bool? is_alive = ParseStartStopCommands(message, ref continuation);
			ParseHelpCommand(message, ref continuation);
			add_chat(message.Chat, message.From, is_alive);
		}
		public virtual bool? ParseStartStopCommands(Message message, ref bool continuation)
        {
			bool? is_alive = null;
			if (message.Text != null)
			{
				string text = message.Text.ToLower();
				if (StartCommands.Contains(text))
				{
					is_alive = true;
					continuation = false;
					if (PrivateChatGreeting!=null&&!PrivateChatGreeting.Equals(string.Empty))
						sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), PrivateChatGreeting));
				}
				else if (StopCommands.Contains(text))
				{
					is_alive = false;
					continuation = false;
				}
			}
			return is_alive;
		}
		public void ParseHelpCommand(Message message, ref bool continuation)
        {
			if (message.Text!=null&&HelpCommands.Contains(message.Text))
			{
				string replyText = "";
				foreach (BotCommand command in SupportedCommands)
				{
					replyText += command.HelpText + "\n\n";
				}
				if (replyText == string.Empty) replyText = "Бот не поддерживает никаких комманд";
				continuation = false;
				sender_to_tg.Put(factory.CreateMessage(new ChatId(message.Chat.Id), replyText, message.MessageId));
			}
		}
		public virtual void GroupChatProcessing(Message message, ref bool continuation)
		{
			bool? is_alive = null;
			ParseHelpCommand(message, ref continuation);
			add_chat(message.Chat, message.From, is_alive);
			
		}
		public static string CaptionComparer(string t1,string t2)
        {
			t1 = t1 == null ? string.Empty : t1;
			t2 = t2 == null ? string.Empty : t2;
			return t1.Length > t2.Length ? t1 : t2;
        }

		public void CreateUnderChatMenu(long chatid, string text)
		{
			var rmu = new ReplyKeyboardMarkup();
			rmu.Keyboard = new List<List<KeyboardButton>>() { new List<KeyboardButton>() { new KeyboardButton(CancelCommand) } };
			rmu.ResizeKeyboard = true;
			sender_to_tg.Put(factory.CreateMessage(chatid, text, keyboardMarkup: rmu));
		}

		public void ClearUnderChatMenu(long chatid, string text)
		{
			var rmr = new ReplyKeyboardRemove();
			sender_to_tg.Put(factory.CreateMessage(chatid, text, keyboardMarkup: rmr));
		}
					
		public virtual List<List<string>> MainMenuDescription => new List<List<string>>();
		public void SendDefaultMenu(long chatId)
		{
			sender_to_tg.Put(factory.CreateMessage(chatId,
				PrivateChatGreeting, keyboardMarkup: CommonFunctions.CreateInlineKeyboard(this.MainMenuDescription)));
		}

		public IMessageToSend RecreateMessage(Message message, long TargetChatId, string Appendix = "")
		{
			IMessageToSend result;
			if (message.Photo != null || message.Video != null)
			{
				if (message.MediaGroupId == null)
				{
					result = factory.CreatePhoto(new ChatId(TargetChatId), message.Photo.Last().FileId, CommonFunctions.TextFormatingRecovering(message.CaptionEntities, message.Caption) + Appendix);
				}
				else
				{
					if (AlbumSyncDictionary.TryAdd(message.MediaGroupId, message.Caption))
					{
						System.Threading.Thread.Sleep(500);
						List<string> media = dBWorker.get_media_ids(message.MediaGroupId);
						string caption = dBWorker.get_caption(message.MediaGroupId);
						result = factory.CreateAlbum(new ChatId(TargetChatId), media, caption + Appendix);
						AlbumSyncDictionary.TryRemove(message.MediaGroupId, out string cap);
					}
					else result = null;
					
					
				}
			}
			else if (message.Sticker != null)
			{
				result = factory.CreateStiker(new ChatId(TargetChatId), message.Sticker.FileId);
			}
			else if (message.Animation != null)
			{
				result = factory.CreateAnimation(new ChatId(TargetChatId), message.Animation.FileId, CommonFunctions.TextFormatingRecovering(message.CaptionEntities, message.Caption) + Appendix);
			}
            else
            {
				result = factory.CreateMessage(new ChatId(TargetChatId), CommonFunctions.TextFormatingRecovering(message.Entities, message.Text) + Appendix);
			}
			return result;

		}


		#endregion
	}
}
