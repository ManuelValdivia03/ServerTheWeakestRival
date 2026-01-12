using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Server.Services.Chat;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ServerTheWeakestRival.Tests.Integration.Services.Chat
{
    [TestClass]
    public sealed class ChatRepositoryIntegrationTests
    {
        private const int USER_ID_ZERO = 0;
        private const int NON_EXISTING_USER_ID = 999999;

        private const int SINCE_ID_SAMPLE = 25;
        private const int SINCE_ID_NEGATIVE = -5;

        private const int MAX_COUNT_ONE = 1;
        private const int MAX_COUNT_TEN = 10;
        private const string SQL_DELETE_CHAT_MESSAGES = @"DELETE FROM dbo.ChatMessages;";
        private const string SQL_DELETE_USERS = @"DELETE FROM dbo.Users;";
        private const string SQL_DELETE_ACCOUNTS = @"DELETE FROM dbo.Accounts;";

        private string connectionString;
        private ChatRepository chatRepository;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();

            CleanDatabase();

            chatRepository = new ChatRepository(() => connectionString);
        }

        [TestMethod]
        public void GetUserDisplayName_WhenUserDoesNotExist_ReturnsDefaultPrefixPlusUserId()
        {
            string displayName = chatRepository.GetUserDisplayName(NON_EXISTING_USER_ID);

            Assert.AreEqual(ChatServiceConstants.DEFAULT_USER_PREFIX + NON_EXISTING_USER_ID, displayName);
        }

        [TestMethod]
        public void GetUserDisplayName_WhenUserIdIsZero_ReturnsDefaultPrefixPlusUserId()
        {
            string displayName = chatRepository.GetUserDisplayName(USER_ID_ZERO);

            Assert.AreEqual(ChatServiceConstants.DEFAULT_USER_PREFIX + USER_ID_ZERO, displayName);
        }

        [TestMethod]
        public void GetMessagesPaged_WhenNoRows_ReturnsEmptyAndLastIdEqualsSinceId()
        {
            ChatPageResult page = chatRepository.GetMessagesPaged(MAX_COUNT_TEN, SINCE_ID_SAMPLE);

            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Messages);
            Assert.AreEqual(0, page.Messages.Count);
            Assert.AreEqual(SINCE_ID_SAMPLE, page.LastChatMessageId);
        }

        [TestMethod]
        public void GetMessagesPaged_WhenSinceIdIsNegative_ReturnsEmptyAndLastIdEqualsSinceId()
        {
            ChatPageResult page = chatRepository.GetMessagesPaged(MAX_COUNT_TEN, SINCE_ID_NEGATIVE);

            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Messages);
            Assert.AreEqual(0, page.Messages.Count);
            Assert.AreEqual(SINCE_ID_NEGATIVE, page.LastChatMessageId);
        }

        [TestMethod]
        public void GetMessagesPaged_WhenMaxCountIsOne_ReturnsEmptyAndLastIdEqualsSinceId()
        {
            ChatPageResult page = chatRepository.GetMessagesPaged(MAX_COUNT_ONE, SINCE_ID_SAMPLE);

            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Messages);
            Assert.AreEqual(0, page.Messages.Count);
            Assert.AreEqual(SINCE_ID_SAMPLE, page.LastChatMessageId);
        }

        [TestMethod]
        public void GetMessagesPaged_ReturnsNonNullMessagesList_Always()
        {
            ChatPageResult page = chatRepository.GetMessagesPaged(MAX_COUNT_TEN, USER_ID_ZERO);

            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Messages);
        }

        private void CleanDatabase()
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var deleteChat = new SqlCommand(SQL_DELETE_CHAT_MESSAGES, sqlConnection))
            using (var deleteUsers = new SqlCommand(SQL_DELETE_USERS, sqlConnection))
            using (var deleteAccounts = new SqlCommand(SQL_DELETE_ACCOUNTS, sqlConnection))
            {
                deleteChat.CommandType = CommandType.Text;
                deleteUsers.CommandType = CommandType.Text;
                deleteAccounts.CommandType = CommandType.Text;

                deleteChat.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;
                deleteUsers.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;
                deleteAccounts.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;

                sqlConnection.Open();

                deleteChat.ExecuteNonQuery();
                deleteUsers.ExecuteNonQuery();
                deleteAccounts.ExecuteNonQuery();
            }
        }
    }
}
