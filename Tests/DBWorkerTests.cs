using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataBaseWorker;
using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using System.IO;
using System.Threading;

namespace DataBaseWorker.Tests
{
    public static class Simulations
    {
        private static Random rnd = new Random();
        /// <summary>
        /// Имитация потока сообщений от разных пользователей
        /// </summary>
        /// <param name="dBWorker"></param>
        /// <param name="MessagesNumber"></param>
        public static void TestMessageCreator(DBWorker dBWorker, int MessagesNumber = 3)
        {
            long pair_chat_id = 10000;
            long pair_message_id = 1;
            Dictionary<long, int> users_and_messages = new Dictionary<long, int>();
            string token1 = "token1";
            dBWorker.add_user(DateTime.UtcNow, 1, token1);
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, pair_message_id, pair_chat_id, 1, 0, token1, "test text " + rnd.Next().ToString());
            int count = 0;
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, 10000 + 1, 1, 0,
                token1, "test text " + rnd.Next().ToString(), pair_chat_id: pair_chat_id, pair_message_id: pair_message_id);
            while (count < MessagesNumber)
            {
                long user = 1;// rnd.Next(0, 1000);
                int message_id = 2;// users_and_messages.TryGetValue(user, out int val) ? val++ : 1;
                if (users_and_messages.ContainsKey(user))
                    users_and_messages[user] = message_id;
                else
                    users_and_messages.Add(user, message_id);
                dBWorker.add_user(DateTime.UtcNow, user, token1);
                dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, message_id, 10000 + user, user, 0, token1, "test text " + rnd.Next().ToString());
                count++;
                Thread.Sleep(100);
            }
        }
    }
    public static class WorkWithDB
    {
        public static void ExecuteNonQueryScript(string ConnectionString, string Script)
        {
            using (NpgsqlConnection Connention = new NpgsqlConnection(ConnectionString))
            {
                Connention.Open();
                NpgsqlCommand command = Connention.CreateCommand();
                command.CommandText = Script;
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                Connention.Close();
             
            }
        }

        public static List<string> ExecuteReaderScript(string ConnectionString, string Script,char sep = ';')
        {
            List<string> result = new List<string>();
            using (NpgsqlConnection Connention = new NpgsqlConnection(ConnectionString))
            {
                Connention.Open();
                NpgsqlCommand command = Connention.CreateCommand();
                command.CommandText = Script;
                command.CommandType = System.Data.CommandType.Text;
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string line = string.Empty;
                        for (int iter=0;iter< reader.FieldCount; iter++)
                        {
                            line += reader[0].ToString() + sep;
                        }
                        result.Add(line);
                    }
                }
                Connention.Close();

            }
            return result;
        }

        public static void DataBaseСustomization(string ConnectionString)
        {
            string Script = File.ReadAllText(@"C:\Users\User\AppData\Roaming\JetBrains\DataGrip2020.1\consoles\db\1a5876b1-33e8-48cc-8377-6af46c6c0513\DBCustomisation.sql");
            ExecuteNonQueryScript(ConnectionString, Script);
        }

        public static void DropTestDB(string ConnectionString, string DBName)
        {
            using (NpgsqlConnection Connention = new NpgsqlConnection(ConnectionString))
            {
                Connention.Open();
                NpgsqlCommand command = Connention.CreateCommand();
                command.CommandText = string.Format("drop database {0};", DBName);
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                Connention.Close();
            }
        }

        public static void CreateTestDB(string ConnectionString, string DBName)
        {
            using (NpgsqlConnection Connention = new NpgsqlConnection(ConnectionString))
            {
                Connention.Open();
                NpgsqlCommand command = Connention.CreateCommand();
                command.CommandText = string.Format("create database {0} template template0;", DBName);
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                Connention.Close();
            }
        }
    }
    [TestClass]
    public class DBWorkerTests
    {
        ~DBWorkerTests()
        {
            zDropAllTestDataBases();
        }
        private static Random rnd = new Random();
        public static int testDataBaseCounter = 0;
        public static string testDBName = "test";
        public static TestContext testContext;
        private static string postgresUser;
        private static string postgresPassword;
        private static string postgresHost;
        private static string ConnectionStringTemplate;
        private static string ConnectionStringToPostgres;
        private static string ConnectionStringToTestDB;


        [ClassInitialize]
        public static void SetupTests(TestContext context)
        {
            testContext = context;
            postgresUser = testContext.Properties["postgresUser"].ToString();
            postgresPassword = testContext.Properties["postgresPassword"].ToString();
            postgresHost = testContext.Properties["postgresHost"].ToString();
            ConnectionStringTemplate = testContext.Properties["ConnectionStringTemplate"].ToString();
            ConnectionStringToPostgres = string.Format(ConnectionStringTemplate, postgresUser, postgresPassword, postgresHost, "postgres");

        }

        private static string GetConnectionStringToTestDB()
        {
            testDataBaseCounter++;
            return ConnectionStringToTestDB = string.Format(ConnectionStringTemplate, postgresUser, postgresPassword, postgresHost, testDBName + testDataBaseCounter);
        }
        private static string GetConnectionStringToTestDB(int testDataBaseCounter)
        {
            return ConnectionStringToTestDB = string.Format(ConnectionStringTemplate, postgresUser, postgresPassword, postgresHost, testDBName + testDataBaseCounter);
        }
        [TestMethod]
        public void SetupTestDB()
        {
            WorkWithDB.CreateTestDB(ConnectionStringToPostgres, testDBName + (testDataBaseCounter));
            WorkWithDB.DataBaseСustomization(GetConnectionStringToTestDB(testDataBaseCounter));
            testDataBaseCounter++;
            Assert.IsTrue(true);
        }

        private DBWorker DBWorkerInit()
        {
            DBWorker dBWorker = new DBWorker(GetConnectionStringToTestDB());
            SetupTestDB();
            dBWorker.Connect();
            return dBWorker;
        }
        [TestMethod]
        public void WritingTest()
        {

            DBWorker dBWorker = DBWorkerInit();

            Simulations.TestMessageCreator(dBWorker, 4);
            System.Threading.Thread.Sleep(1000);
            dBWorker.Disconnect();
            System.Threading.Thread.Sleep(1000);
            //DropTestDB();
            //System.Threading.Thread.Sleep(1000);
            Assert.IsTrue(true);
        }


        [TestMethod()]
        public void get_bot_idTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            int id_1 = dBWorker.get_bot_id("token1");
            int id_1_doubled = dBWorker.get_bot_id("token1");
            int id_2 = dBWorker.get_bot_id("token2");
            int id_2_doubled = dBWorker.get_bot_id("token2");
            Assert.AreEqual(id_1, id_1_doubled);
            Assert.AreEqual(id_2, id_2_doubled);
            Assert.AreNotEqual(id_1, id_2);
        }


        [TestMethod]
        public void zDropAllTestDataBases()
        {
            for (int iter = 0; iter <= 30; iter++)
            {
                try
                {
                    WorkWithDB.DropTestDB(ConnectionStringToPostgres, testDBName+iter);
                }
                catch { }

            }
        }

        [TestMethod()]
        public void UserBansTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            string bot_token_for_ban = "token1";
            string bot_token2 = "token2";
            string bot_token3 = "token3";
            dBWorker.add_user(DateTime.UtcNow, 1, bot_token_for_ban);
            dBWorker.add_user(DateTime.UtcNow, 2, bot_token_for_ban);
            dBWorker.add_user(DateTime.UtcNow, 1, bot_token2);
            dBWorker.add_user(DateTime.UtcNow, 1, bot_token3);
            System.Threading.Thread.Sleep(500);
            dBWorker.ban_user(1, 1, bot_token_for_ban);
            System.Threading.Thread.Sleep(500);
            bool res = dBWorker.check_user_ban(1, 1, bot_token_for_ban);
            Assert.IsTrue(res);
            Assert.IsFalse(dBWorker.check_user_ban(2, 1, bot_token_for_ban));
            Assert.IsFalse(dBWorker.check_user_ban(2, 1, bot_token2));
            Assert.IsFalse(dBWorker.check_user_ban(1, 1, bot_token3));
        }

        [TestMethod()]
        public void add_userTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            int MaxAddUsersIters = rnd.Next(3, 100);
            int MaxAddUsersNumber = rnd.Next(10, 100);
            for (int iter = 0; iter < MaxAddUsersIters; iter++)
            {
                for (int iter1 = 0; iter1 < MaxAddUsersNumber; iter1++)
                {
                    dBWorker.add_user(DateTime.UtcNow, iter1, "tttt");
                }
            }
            List<string> users = WorkWithDB.ExecuteReaderScript(dBWorker.ConnectionString, "select * from public.users");
            Assert.AreEqual(users.Count, MaxAddUsersNumber);

        }

        [TestMethod()]
        public void add_chat_NumberTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            int MaxAddChatsIters = rnd.Next(3, 100);
            int MaxAddChatsNumber = rnd.Next(10, 100);
            for (int iter = 0; iter < MaxAddChatsIters; iter++)
            {
                for (int iter1 = 0; iter1 < MaxAddChatsNumber; iter1++)
                {
                    dBWorker.add_chat(DateTime.UtcNow, iter1, "tttt",1);
                }
            }
            List<string> chats = WorkWithDB.ExecuteReaderScript(dBWorker.ConnectionString, "select * from public.chats");
            Assert.AreEqual(chats.Count, MaxAddChatsNumber);
        }

        [TestMethod()]
        public void get_user_by_chatTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            dBWorker.add_chat(DateTime.UtcNow, 1, "token1", 1);
            dBWorker.add_chat(DateTime.UtcNow, 2, "token1", 2);
            dBWorker.add_chat(DateTime.UtcNow, 3, "token1", 3);
            dBWorker.add_chat(DateTime.UtcNow, 4, "token1", 4, is_group: true);
            System.Threading.Thread.Sleep(500);
            Assert.IsTrue(1 == dBWorker.get_user_by_chat(1));
            Assert.IsTrue(2 == dBWorker.get_user_by_chat(2));
            Assert.IsTrue(3 == dBWorker.get_user_by_chat(3));
            Assert.IsNull(dBWorker.get_user_by_chat(4));

        }

        [TestMethod()]
        public void get_active_groupsTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            dBWorker.add_chat(DateTime.UtcNow, 1, "token1", 1);
            dBWorker.add_chat(DateTime.UtcNow, 2, "token1", 2);
            dBWorker.add_chat(DateTime.UtcNow, 3, "token1", 3);
            dBWorker.add_chat(DateTime.UtcNow, 4, "token1", 4, is_group: true);
            dBWorker.add_chat(DateTime.UtcNow, 5, "token1", 5, is_group: true);
            dBWorker.add_chat(DateTime.UtcNow, 6, "token1", 6, is_group: true, is_active: false);
            System.Threading.Thread.Sleep(500);
            List<long> active_groups = dBWorker.get_active_groups();
            Assert.IsTrue(active_groups.Count == 2);
            Assert.IsTrue(active_groups.Contains(4) && active_groups.Contains(5));
        }

        [TestMethod()]
        public void get_pair_chat_idTest()
        {
            string bot_token = "token1";
            DBWorker dBWorker = DBWorkerInit();
            dBWorker.add_user(DateTime.UtcNow, 1, bot_token);
            dBWorker.add_user(DateTime.UtcNow, 2, bot_token);
            dBWorker.add_user(DateTime.UtcNow, 3, bot_token);
            long user1_id = 1;
            long user2_id = 2;
            long chat1_id = 1;
            long chat2_id = 2;


            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 1, chat1_id, user1_id, 0, bot_token, text: "some text1");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, chat1_id, user1_id, 0, bot_token, text: "some text2");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 3, chat1_id, user1_id, 0, bot_token, text: "some text3");

            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 1, chat2_id, user2_id, 0, bot_token, text: "some text1");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, chat2_id, user2_id, 0, bot_token, text: "some text2", pair_chat_id: 1, pair_message_id: 3);
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 3, chat2_id, user2_id, 0, bot_token, text: "some text3");
            System.Threading.Thread.Sleep(500);
            Assert.AreEqual(1, dBWorker.get_pair_chat_id(2, 2, bot_token));
            Assert.AreEqual(2, dBWorker.get_pair_chat_id(1, 3, bot_token));
            Assert.IsNull(dBWorker.get_pair_chat_id(1, 1, bot_token));
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, 2, 2, 0, bot_token, text: "some text2");
            System.Threading.Thread.Sleep(500);
            Assert.AreEqual(1, dBWorker.get_pair_chat_id(2, 2, bot_token));
            Assert.AreEqual(2, dBWorker.get_pair_chat_id(1, 3, bot_token));
            Assert.IsNull(dBWorker.get_pair_chat_id(1, 1, bot_token));
        }

        [TestMethod()]
        public void get_pair_message_idTest()
        {
            string bot_token = "token1";
            DBWorker dBWorker = DBWorkerInit();
            dBWorker.add_user(DateTime.UtcNow, 1, bot_token);
            dBWorker.add_user(DateTime.UtcNow, 2, bot_token);
            dBWorker.add_user(DateTime.UtcNow, 3, bot_token);
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 1, 1, 1, 0, bot_token, text: "some text1");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, 1, 1, 0, bot_token, text: "some text2");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 3, 1, 1, 0, bot_token, text: "some text3");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 1, 2, 2, 0, bot_token, text: "some text1");
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, 2, 2, 0, bot_token, text: "some text2", pair_chat_id: 1, pair_message_id: 3);
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 3, 2, 2, 0, bot_token, text: "some text3");
            System.Threading.Thread.Sleep(500);
            Assert.AreEqual(3, dBWorker.get_pair_message_id(2, 2, bot_token));
            Assert.AreEqual(2, dBWorker.get_pair_message_id(1, 3, bot_token));
            Assert.IsNull(dBWorker.get_pair_message_id(1, 1, bot_token));
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, 2, 2, 2, 0, bot_token, text: "some text4");
            Assert.AreEqual(1, dBWorker.get_pair_chat_id(2, 2, bot_token));
            Assert.AreEqual(2, dBWorker.get_pair_chat_id(1, 3, bot_token));
            Assert.IsNull(dBWorker.get_pair_chat_id(1, 1, bot_token));
        }

        [TestMethod()]
        public void get_media_idsTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            string bot_token = "token1";
            string media_group_id = "mgid";
            long user_id = 1;
            dBWorker.add_user(DateTime.UtcNow, user_id, bot_token);
            int iter=0;
            List<string> mediaIds = new List<string>();
            for (iter = 1; iter < 4; iter++)
            {
                string mediaId = "medID" + iter;
                dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, iter, 1, 1, 0, bot_token,caption:"",media_tg_id: mediaId, media_group_id: media_group_id);
                mediaIds.Add(mediaId);
            }
            for (; iter < 7; iter++)
            {
                dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, iter, 1, 1, 0, bot_token, caption: "", media_tg_id: "medID" + iter);

            }
            Thread.Sleep(500);
            List<string> mediaIdsFromDB = dBWorker.get_media_ids(media_group_id);
            foreach (string id in mediaIdsFromDB)
            {
                Assert.IsTrue(mediaIds.Contains(id));
            }
            foreach (string id in mediaIds)
            {
                Assert.IsTrue(mediaIdsFromDB.Contains(id));
            }
        }

            [TestMethod()]
        public void get_captionTest()
        {
            DBWorker dBWorker = DBWorkerInit();
            string bot_token = "token1";
            string caption = "some caption";
            string media_group_id = "mgid";
            long user_id = 1;
            dBWorker.add_user(DateTime.UtcNow, user_id, bot_token);
            int iter = 0;
            List<string> mediaIds = new List<string>();
            for (iter = 1; iter < 4; iter++)
            {
                string mediaId = "medID" + iter;
                dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, iter, 1, 1, 0, bot_token, media_tg_id: mediaId, media_group_id: media_group_id);
                mediaIds.Add(mediaId);
            }
            dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, iter, 1, 1, 0, bot_token, caption: caption, media_tg_id: "medID" + iter, media_group_id: media_group_id);
            for (iter++; iter < 8; iter++)
            {
                dBWorker.add_message(DateTime.UtcNow, DateTime.UtcNow, iter, 1, 1, 0, bot_token, caption: caption, media_tg_id: "medID" + iter);
            }
            System.Threading.Thread.Sleep(500);
            string captionFromDB = dBWorker.get_caption(media_group_id);
            Assert.AreEqual(captionFromDB, caption);
        }
    }
}

