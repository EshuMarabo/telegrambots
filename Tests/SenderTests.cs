using BaseTelegramBot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace SenderTests
{
    [TestClass]
    public class WorkingTest
    {
        #region Общие настройки
        private static int SamplingTime = 10;//Время проведения теста


        #endregion

        [TestMethod]
        public void MessagePerSecondTest_MomentSending()
        {
            TestMessageToSend.RecipientNumber = 30000;
            TestMessageToSend.MessageNumber = 1;
            TestMessageToSend.GeneratorSleeping = 4000;
            Assert.IsTrue(MessagePerSecondTest(300, 100, 0, 1));
        }

        [TestMethod]
        public void MessagePerSecondTest_LongSending()
        {
            TestMessageToSend.RecipientNumber = 30000;
            TestMessageToSend.MessageNumber = 1;
            TestMessageToSend.GeneratorSleeping = 4000;
            Assert.IsTrue(MessagePerSecondTest(300, 100, 200, 0.66));
            Assert.IsTrue(MessagePerSecondTest(300, 100, 300, 2));
        }
        [TestMethod]
        public void PersonPerTickTest()
        {
            double SendingInterval = 100;
            int MessagesPerTick = 10;
            TestMessageToSend.RecipientNumber = 5;
            TestMessageToSend.MessageNumber = 3;
            TestMessageToSend.GeneratorSleeping = 300;
            TestMessageToSend.SamplingInterval = (int)SendingInterval * 20;
            double limit = TestMessageToSend.RecipientNumber * TestMessageToSend.SamplingInterval / SendingInterval;
            double res = TestCoreMethod(MessagesPerTick, SendingInterval, 0);
            Assert.IsTrue(res<= limit);

            TestMessageToSend.MessageNumber = 3000;
            res = TestCoreMethod(MessagesPerTick, SendingInterval, 0);
            Assert.IsTrue(res <= limit);


            TestMessageToSend.RecipientNumber = 15;
            TestMessageToSend.MessageNumber = 1;
            limit = MessagesPerTick * TestMessageToSend.SamplingInterval / SendingInterval;
            res = TestCoreMethod(MessagesPerTick, SendingInterval, 0);
            Assert.IsTrue(res <= limit);

        }

        [TestMethod]
        public void AllMessageSendedTest()
        {
            TestMessageToSend.RecipientNumber = 30;
            TestMessageToSend.MessageNumber = 300;
            SendSomeMessages(500, 10, 30);
            Assert.IsTrue(TestMessageToSend.TotalSendsCounter==TestMessageToSend.TotalCreationsCounter);
        }


        [TestMethod]
        public void MessageSequenceTest()
        {
            TestMessageToSend.created = new List<TestMessageToSend>();
            TestMessageToSend.TotalSendsCounter = 0;
            TestMessageToSend.TotalCreationsCounter = 0;
            TestMessageToSend.RecipientNumber = 30;
            TestMessageToSend.MessageNumber = 300;
            SendSomeMessages(500, 10, 30);
            Assert.IsTrue(TestMessageToSend.TotalSendsCounter == TestMessageToSend.TotalCreationsCounter);
            List<double> diffs_ms = new List<double>();
            List<int> diffs_count = new List<int>();
            for (long RecNumber=0; RecNumber < TestMessageToSend.RecipientNumber; RecNumber++)
            {
                List<TestMessageToSend> PersonMessages = TestMessageToSend.created.FindAll(item => item.GetChatId() == RecNumber);
                for (int i=1;i< PersonMessages.Count; i++)
                {
                    TestMessageToSend mes2 = PersonMessages[i];
                    TestMessageToSend mes1 = PersonMessages[i-1];
                    double diff_ms = mes2.SendingTime.Subtract(mes1.SendingTime).TotalMilliseconds;
                    int diff_count = mes2.GetMessageNumber() - mes1.GetMessageNumber();
                    diffs_ms.Add(diff_ms);
                    diffs_count.Add(diff_count);
                }
            }
            var q1 = diffs_ms.FindAll(item => item < 0);
            var q2 = diffs_count.FindAll(item => item != 1);
            Assert.IsTrue(q1.Count<0.01* TestMessageToSend.created.Count&& q2.Count==0); 


            
        }

        public static bool MessagePerSecondTest(int MessagesPerTick, double SendingInterval, 
            int SendingDelayAmplitude,double limitKoeff=1)
        {

            double NoZeroMean = TestCoreMethod(MessagesPerTick, SendingInterval, 
                SendingDelayAmplitude);

            double limit = MessagesPerTick * TestMessageToSend.SamplingInterval / SendingInterval;
            return (NoZeroMean <= (limit* limitKoeff)+ MessagesPerTick);
        }
        private static double TestCoreMethod(int MessagesPerTick, double SendingInterval, 
            int SendingDelayAmplitude)
        {
            TestMessageToSend.sender = new Sender(MessagesPerTick, SendingInterval);
            Thread MessageCreatorThread = new Thread(new ThreadStart(TestMessageToSend.InfinityMessageCreation));

            TestMessageToSend.SendingDelayAmplitude = SendingDelayAmplitude;


            TestMessageToSend.sender.Start();
            MessageCreatorThread.Start();
            

            DateTime dt = DateTime.UtcNow;
            List<int> scores = new List<int>();
            bool clear = true;
            while (DateTime.UtcNow.Subtract(dt).TotalSeconds < SamplingTime)
            {
                if (clear)
                {
                    TestMessageToSend.TotalSendsCounter = 0;
                    clear = false;
                    continue;
                }
                scores.Add(TestMessageToSend.TotalSendsCounter);
                TestMessageToSend.TotalSendsCounter = 0;
                System.Threading.Thread.Sleep(TestMessageToSend.SamplingInterval);
            }

            List<int> scoresTemp = scores.FindAll(item => item > 0);
            TestMessageToSend.sender.Stop();

            return scoresTemp.Sum() / scoresTemp.Count;
        }


        private static void SendSomeMessages(int MessagesPerTick, double SendingInterval,
            int SendingDelayAmplitude)
        {
            TestMessageToSend.TotalSendsCounter = 0;
            TestMessageToSend.sender = new Sender(MessagesPerTick, SendingInterval);
            TestMessageToSend.SendingDelayAmplitude = SendingDelayAmplitude;
            TestMessageToSend.MessageCreation(1);
            TestMessageToSend.sender.Start();
            
            DateTime dt = DateTime.UtcNow;
            List<int> scores = new List<int>();
            while ( TestMessageToSend.sender.GetMaxPriorQuenueSize() != 0 
                | TestMessageToSend.sender.GetMainQuenueSize()!=0)
            {
                System.Threading.Thread.Sleep(TestMessageToSend.SamplingInterval);
            }
        }
    }

    
    public class TestMessageToSend : IMessageToSend
    {

        #region статические настройки тестов
        public static int SamplingInterval = 2000;
        public static int TotalSendsCounter = 0;
        public static int TotalCreationsCounter = 0;
        public static DateTime StartDT;
        public static DateTime CurrentDT;
        public static Sender sender;
        public static long FirstRecipient=0;
        public static long RecipientNumber= 30000;
        public static int FirstMessageNumber=0;
        public static int MessageNumber=1;
        public static int SendingDelayAmplitude=0;
        public static int GeneratorSleeping= 4000;
        #endregion

        #region статические поля
        private object locker = new object();
        public static List<TestMessageToSend> sended = new List<TestMessageToSend>();
        public static List<TestMessageToSend> created = new List<TestMessageToSend>();
        internal static Random rnd = new Random();
        #endregion

        #region поля
        public DateTime SendingTime;
        private long ChatId;
        private int number;
        private Task SendingTask;
        private int SendingDelay;
        #endregion

        public static void InfinityMessageCreation()
        {

            while (true)
            {
                MessageCreation();
                System.Threading.Thread.Sleep(GeneratorSleeping);
            }
        }

        public static void MessageCreation()
        {
            for (long iter = FirstRecipient; iter < FirstRecipient + RecipientNumber; iter++)
            {
                for (int MessNumber = FirstMessageNumber; MessNumber < FirstMessageNumber + MessageNumber; MessNumber++)
                {
                    int ampl = (int)(rnd.NextDouble() * SendingDelayAmplitude);
                    TestMessageToSend mes = new TestMessageToSend(iter, ampl, MessNumber);
                    sender.Put(mes);
                    created.Add(mes);
                    TotalCreationsCounter++;

                }
            }
        }

        public static void MessageCreation(int Cycles)
        {
            int cycle = 0;
            while (cycle< Cycles)
            {
                MessageCreation();
                cycle++;
            }

        }
        public int GetMessageNumber()
        {
            return number;
        }

        public TestMessageToSend(long id, int SendingDelay = 50, int Number = 0)
        {

            ChatId = id;
            this.number = Number;
            this.SendingDelay = SendingDelay;
        }


        public long GetChatId()
        {
            return ChatId;
        }

        public bool isFinished()
        {
            if (SendingTask != null)
                return (SendingTask.Status == TaskStatus.RanToCompletion|| 
                    SendingTask.Status == TaskStatus.Canceled ||
                    SendingTask.Status == TaskStatus.WaitingForChildrenToComplete ||
                    SendingTask.Status == TaskStatus.Faulted);
            else
                return false;
        }


        public void Send()
        {
            SendingTask = Task.Delay(SendingDelay);
            SendingTask.ContinueWith(add, SendingTask);
            //await SendingTask.ConfigureAwait(false);
            TotalSendsCounter++;
        }

        private void add(Task arg1, object arg2)
        {
            SendingTime = DateTime.UtcNow;
            /*
            lock (locker)
            {
                sended.Add(this);
            }
                */
        }

        public void AddLinkedMessage(object message)
        {
            
        }
    }
}


    

