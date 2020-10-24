using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BaseTelegramBot
{
    /// <summary>
    /// Обобщенный интерфейс класса - обертки сообщения
    /// </summary>
    public interface IMessageToSend
    {
        public void Send();

        public long GetChatId();

        public bool isFinished();

        public void AddLinkedMessage(object message);
    }

    public class Sender
    {
        #region поля
        public int MessagesBySecond { get; internal set; }
        public int MessagesByPerson { get; internal set; }
        public int QuenueMaxSize { get; internal set; }
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static object locker = new object();
        private Queue<IMessageToSend> MainQuenue;
        private Queue<IMessageToSend> MaxPriorityQuenue;
        private Queue<IMessageToSend> MinPriorityQuenue;
        private System.Timers.Timer timer;
        private IMessageToSend[] SendingBuffer;

        #endregion
        public Sender(int MessagesPerTick = 30, double SendingInterval = 1000,
            int MessagesByPerson = 1, int QuenueMaxSize = 0)
        {

            this.MessagesBySecond = MessagesPerTick;
            this.MessagesByPerson = MessagesByPerson;
            this.QuenueMaxSize = QuenueMaxSize;

            SendingBuffer = new IMessageToSend[this.MessagesBySecond];
            MainQuenue = new Queue<IMessageToSend>();
            MaxPriorityQuenue = new Queue<IMessageToSend>();
            MinPriorityQuenue = new Queue<IMessageToSend>();

            timer = new System.Timers.Timer();
            timer.Elapsed += action;
            timer.Interval = SendingInterval;
            timer.AutoReset = true;
            timer.Enabled = false;
        }

        public int GetMainQuenueSize()
        {
            return MainQuenue.Count;
        }
        public int GetMaxPriorQuenueSize()
        {
            return MaxPriorityQuenue.Count;
        }
        public int GetMinPriorQuenueSize()
        {
            return MinPriorityQuenue.Count;
        }

        #region методы
        public void Start()
        {
            logger.Info("Started!");
            timer.Enabled = true;
        }
        public void Stop()
        {
            logger.Info("Stopped!");
            timer.Enabled = false;
        }
        public void Put(IMessageToSend mess)
        {
            if (mess!=null)
                lock (locker)
                {
                    MainQuenue.Enqueue(mess);
                }
        }

        /// <summary>
        /// Основной метод-отправитель сообщений
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void action(object sender, ElapsedEventArgs e)
        {
            lock (locker)
            {
                try
                {
                    logger.Debug("Sending action started.");
                    List<long> ids = new List<long>();
                    List<IMessageToSend> maxPriorityBuffer = new List<IMessageToSend>();
                    List<IMessageToSend> middlePriorityBuffer = new List<IMessageToSend>();
                    List<IMessageToSend> minPriorityBuffer = new List<IMessageToSend>();
                    for (int iter = 0; iter < SendingBuffer.Length; iter++)
                    {
                        if (SendingBuffer[iter] == null || SendingBuffer[iter].isFinished())
                        {
                            bool BufferElementAdded = false;
                            IMessageToSend mess;
                            while (!BufferElementAdded && MaxPriorityQuenue.TryDequeue(out mess))
                            {
                                if (ids.FindAll(item => item == mess.GetChatId()).Count < MessagesByPerson)
                                {
                                    SendingBuffer[iter] = mess;
                                    SendingBuffer[iter].Send();
                                    ids.Add(SendingBuffer[iter].GetChatId());
                                    BufferElementAdded = true;
                                }
                                else
                                {
                                    maxPriorityBuffer.Add(mess);
                                }
                            }
                            while (!BufferElementAdded && MainQuenue.TryDequeue(out mess))
                            {
                                if (ids.FindAll(item => item == mess.GetChatId()).Count < MessagesByPerson)
                                {
                                    SendingBuffer[iter] = mess;
                                    
                                    SendingBuffer[iter].Send();
                                    ids.Add(SendingBuffer[iter].GetChatId());
                                    BufferElementAdded = true;
                                }
                                else
                                {
                                    middlePriorityBuffer.Add(mess);
                                }
                            }
                            while (!BufferElementAdded && MinPriorityQuenue.TryDequeue(out mess))
                            {
                                if (ids.FindAll(item => item == mess.GetChatId()).Count < MessagesByPerson)
                                {
                                    SendingBuffer[iter] = mess;
                                    SendingBuffer[iter].Send();
                                    ids.Add(SendingBuffer[iter].GetChatId());
                                    BufferElementAdded = true;
                                }
                                else
                                {
                                    minPriorityBuffer.Add(mess);
                                }
                            }
                        }
                        else
                        {
                            ids.Add(SendingBuffer[iter].GetChatId());
                        }
                    }
                    maxPriorityBuffer.AddRange(MaxPriorityQuenue.ToList());
                    maxPriorityBuffer.AddRange(middlePriorityBuffer);
                    MaxPriorityQuenue = new Queue<IMessageToSend>(maxPriorityBuffer);
                    minPriorityBuffer.AddRange(MinPriorityQuenue.ToList());
                    MinPriorityQuenue = new Queue<IMessageToSend>(minPriorityBuffer);
                    logger.Debug(string.Format("Sender queneues status: MaxPriorityQuenue size = {0}, " +
                       "MainQuenue size = {1}, MinPriorityQuenue size = {2}",
                       MaxPriorityQuenue.Count,MainQuenue.Count,MinPriorityQuenue.Count));
                }
                catch (Exception ex)
                {
                    logger.Error(ex,"Error while sending message by Sender");
                }
            }
        }
        #endregion
    }


}

