using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services;
using ServicesTheWeakestRival.Server.Services.Chat;
using ServerTheWeakestRival.Tests.Unit.Infrastructure;
using System;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;

namespace ServerTheWeakestRival.Tests.Integration.Services.Chat
{
    [TestClass]
    public sealed class ChatOperationsIntegrationTests
    {
        private const int INVALID_ID = 0;

        private const int TOKEN_TTL_MINUTES = 30;
        private const int MANY_MESSAGES_COUNT = 210;

        private const int SINCE_ID_ZERO = 0;
        private const int GET_MAX_COUNT_TEN = 10;
        private const int SINCE_ID_SAMPLE = 123;
        private const int MAXCOUNT_TOO_HIGH = int.MaxValue;

        private const string DEFAULT_DISPLAY_NAME = "Test User";
        private const string DEFAULT_MESSAGE_TEXT = "Hello world";
        private const string WHITESPACE_MESSAGE = "   ";

        private const int INVALID_USER_ID = 0;

        private const int SINCE_ID_NEGATIVE = -5;
        private const int MAXCOUNT_NULL = 0; 
        private const int MAXCOUNT_ZERO = 0;
        private const int MAXCOUNT_NEGATIVE = -10;

        private const int EXPECTED_ONE = 1;

        private const int PAGE_SIZE_SMALL = 2;
        private const int PAGE_SIZE_THREE = 3;

        private const int EXTRA_MESSAGES = 5;

        private const string TOKEN_INVALID_VALUE = "token_not_in_cache";


        private const string SQL_INSERT_ACCOUNT = @"
            INSERT INTO dbo.Accounts (email, password_hash, status, created_at, suspended_until_utc)
            VALUES (@email, @password_hash, @status, @created_at, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        private const string SQL_INSERT_USER = @"
            INSERT INTO dbo.Users (user_id, display_name, created_at)
            VALUES (@user_id, @display_name, @created_at);";

        private const string SQL_DELETE_CHAT_MESSAGES = @"DELETE FROM dbo.ChatMessages;";
        private const string SQL_DELETE_USERS = @"DELETE FROM dbo.Users;";
        private const string SQL_DELETE_ACCOUNTS = @"DELETE FROM dbo.Accounts;";

        private const string PARAM_USER_ID = "@user_id";
        private const string PARAM_DISPLAY_NAME = "@display_name";
        private const string PARAM_CREATED_AT = "@created_at";

        private const string PARAM_EMAIL = "@email";
        private const string PARAM_PASSWORD_HASH = "@password_hash";
        private const string PARAM_STATUS = "@status";

        private const int ACCOUNT_STATUS_ACTIVE = 1;
        private const string PASSWORD_HASH_DUMMY = "TEST_HASH";
        private const string EMAIL_PREFIX = "chat.test+";
        private const string EMAIL_DOMAIN = "@example.com";
        private const int EMAIL_MAX_LENGTH = 320;
        private const int PASSWORD_HASH_MAX_LENGTH = 128;

        private ChatOperations chatOperations;
        private string connectionString;

        [TestInitialize]
        public void TestInitialize()
        {
            connectionString = DbTestConfig.GetMainConnectionString();

            CleanDatabase();
            CleanTokenStore();

            var repository = new ChatRepository(() => connectionString);
            chatOperations = new ChatOperations(repository);
        }

        [TestMethod]
        public void SendChatMessage_RequestIsNull_ThrowsFaultInvalidRequest()
        {
            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(null));

            Assert.AreEqual(ChatServiceConstants.ERROR_INVALID_REQUEST, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_REQUEST_NULL, fault.Detail.Message);
        }

        [TestMethod]
        public void SendChatMessage_MessageTextWhitespace_ThrowsFaultValidation()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            var request = new SendChatMessageRequest
            {
                AuthToken = token,
                MessageText = WHITESPACE_MESSAGE
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_VALIDATION, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TEXT_EMPTY, fault.Detail.Message);
        }

        [TestMethod]
        public void SendChatMessage_MessageTooLong_ThrowsFaultValidation()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            string longMessage = new string('a', ChatServiceConstants.MAX_MESSAGE_LENGTH + 1);

            var request = new SendChatMessageRequest
            {
                AuthToken = token,
                MessageText = longMessage
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_VALIDATION, fault.Detail.Code);
            StringAssert.Contains(fault.Detail.Message, ChatServiceConstants.MESSAGE_TEXT_TOO_LONG_PREFIX);
        }

        [TestMethod]
        public void SendChatMessage_TokenMissing_ThrowsFaultUnauthorized()
        {
            var request = new SendChatMessageRequest
            {
                AuthToken = null,
                MessageText = DEFAULT_MESSAGE_TEXT
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_UNAUTHORIZED, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TOKEN_REQUIRED, fault.Detail.Message);
        }

        [TestMethod]
        public void SendChatMessage_TokenExpired_ThrowsFaultUnauthorized()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreExpiredToken(userId);

            var request = new SendChatMessageRequest
            {
                AuthToken = token,
                MessageText = DEFAULT_MESSAGE_TEXT
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_UNAUTHORIZED, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TOKEN_EXPIRED, fault.Detail.Message);
        }

        [TestMethod]
        public void SendChatMessage_ValidRequest_InsertsRow_AndReturnsSuccess()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            var sendRequest = new SendChatMessageRequest
            {
                AuthToken = token,
                MessageText = DEFAULT_MESSAGE_TEXT
            };

            BasicResponse sendResponse = chatOperations.SendChatMessage(sendRequest);

            Assert.IsNotNull(sendResponse);
            Assert.IsTrue(sendResponse.IsSuccess);

            var getRequest = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = SINCE_ID_ZERO,
                MaxCount = GET_MAX_COUNT_TEN
            };

            GetChatMessagesResponse getResponse = chatOperations.GetChatMessages(getRequest);

            Assert.IsNotNull(getResponse);
            Assert.IsNotNull(getResponse.Messages);
            Assert.IsTrue(getResponse.Messages.Length >= 1);

            ChatMessageDto last = getResponse.Messages[getResponse.Messages.Length - 1];

            Assert.AreEqual(userId, last.UserId);
            Assert.AreEqual(DEFAULT_MESSAGE_TEXT, last.MessageText);
            Assert.IsTrue(last.ChatMessageId > 0);
        }

        [TestMethod]
        public void GetChatMessages_RequestIsNull_ThrowsFaultInvalidRequest()
        {
            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.GetChatMessages(null));

            Assert.AreEqual(ChatServiceConstants.ERROR_INVALID_REQUEST, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_REQUEST_NULL, fault.Detail.Message);
        }

        [TestMethod]
        public void GetChatMessages_NoMessages_ReturnsEmptyArray_AndLastIdEqualsSinceId()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = SINCE_ID_SAMPLE,
                MaxCount = GET_MAX_COUNT_TEN
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(0, response.Messages.Length);
            Assert.AreEqual(SINCE_ID_SAMPLE, response.LastChatMessageId);
        }

        [TestMethod]
        public void GetChatMessages_MaxCountTooHigh_IsClampedToMaxPageSize()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, MANY_MESSAGES_COUNT);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = SINCE_ID_ZERO,
                MaxCount = MAXCOUNT_TOO_HIGH
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.IsTrue(response.Messages.Length <= ChatServiceConstants.MAX_PAGE_SIZE);
        }

        [TestMethod]
        public void SendChatMessage_TokenInvalid_ThrowsFaultUnauthorized()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);

            var request = new SendChatMessageRequest
            {
                AuthToken = TOKEN_INVALID_VALUE,
                MessageText = DEFAULT_MESSAGE_TEXT
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_UNAUTHORIZED, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TOKEN_INVALID, fault.Detail.Message);
        }

        [TestMethod]
        public void GetChatMessages_TokenInvalid_ThrowsFaultUnauthorized()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);

            var request = new GetChatMessagesRequest
            {
                AuthToken = TOKEN_INVALID_VALUE,
                SinceChatMessageId = 0,
                MaxCount = PAGE_SIZE_SMALL
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.GetChatMessages(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_UNAUTHORIZED, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TOKEN_INVALID, fault.Detail.Message);
        }

        private static string AddRawTokenWithUserId(int userId, DateTime expiresAtUtc)
        {
            string tokenValue = Guid.NewGuid().ToString("N");

            TokenStore.Cache[tokenValue] = new AuthToken
            {
                Token = tokenValue,
                UserId = userId,
                ExpiresAtUtc = expiresAtUtc
            };

            TokenStore.ActiveTokenByUserId[userId] = tokenValue;

            return tokenValue;
        }

        [TestMethod]
        public void SendChatMessage_TokenWithInvalidUserId_ThrowsFaultUnauthorized()
        {
            string token = AddRawTokenWithUserId(INVALID_USER_ID, DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES));

            var request = new SendChatMessageRequest
            {
                AuthToken = token,
                MessageText = DEFAULT_MESSAGE_TEXT
            };

            FaultException<ServiceFault> fault = AssertThrowsServiceFault(
                () => chatOperations.SendChatMessage(request));

            Assert.AreEqual(ChatServiceConstants.ERROR_UNAUTHORIZED, fault.Detail.Code);
            Assert.AreEqual(ChatServiceConstants.MESSAGE_TOKEN_INVALID, fault.Detail.Message);
        }

        [TestMethod]
        public void GetChatMessages_SinceIdNull_TreatsAsZero()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, PAGE_SIZE_THREE);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = null,
                MaxCount = PAGE_SIZE_THREE
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(PAGE_SIZE_THREE, response.Messages.Length);
            Assert.IsTrue(response.LastChatMessageId > 0);
        }

        [TestMethod]
        public void GetChatMessages_SinceIdNegative_TreatsAsZero()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, PAGE_SIZE_THREE);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = SINCE_ID_NEGATIVE,
                MaxCount = PAGE_SIZE_THREE
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(PAGE_SIZE_THREE, response.Messages.Length);
            Assert.IsTrue(response.LastChatMessageId > 0);
        }

        [TestMethod]
        public void GetChatMessages_MaxCountNull_UsesDefaultPageSize()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            int totalToInsert = ChatServiceConstants.DEFAULT_PAGE_SIZE + EXTRA_MESSAGES;
            InsertManyMessages(token, totalToInsert);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = 0,
                MaxCount = null
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(ChatServiceConstants.DEFAULT_PAGE_SIZE, response.Messages.Length);
        }

        [TestMethod]
        public void GetChatMessages_MaxCountZero_UsesOne()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, PAGE_SIZE_THREE);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = 0,
                MaxCount = MAXCOUNT_ZERO
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(EXPECTED_ONE, response.Messages.Length);
        }

        [TestMethod]
        public void GetChatMessages_MaxCountNegative_UsesOne()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, PAGE_SIZE_THREE);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = 0,
                MaxCount = MAXCOUNT_NEGATIVE
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(EXPECTED_ONE, response.Messages.Length);
        }

        [TestMethod]
        public void GetChatMessages_Pagination_WalksForward_ByLastChatMessageId()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            InsertManyMessages(token, PAGE_SIZE_THREE);

            var firstRequest = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = 0,
                MaxCount = PAGE_SIZE_SMALL
            };

            GetChatMessagesResponse first = chatOperations.GetChatMessages(firstRequest);

            Assert.IsNotNull(first);
            Assert.IsNotNull(first.Messages);
            Assert.AreEqual(PAGE_SIZE_SMALL, first.Messages.Length);
            Assert.IsTrue(first.LastChatMessageId > 0);

            var secondRequest = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = first.LastChatMessageId,
                MaxCount = PAGE_SIZE_SMALL
            };

            GetChatMessagesResponse second = chatOperations.GetChatMessages(secondRequest);

            Assert.IsNotNull(second);
            Assert.IsNotNull(second.Messages);
            Assert.AreEqual(EXPECTED_ONE, second.Messages.Length);
            Assert.IsTrue(second.LastChatMessageId >= first.LastChatMessageId);
        }

        [TestMethod]
        public void GetChatMessages_ReturnsMessagesInAscendingOrder()
        {
            int userId = CreateAccountAndUser(DEFAULT_DISPLAY_NAME);
            string token = CreateAndStoreToken(userId);

            int totalToInsert = ChatServiceConstants.DEFAULT_PAGE_SIZE;
            InsertManyMessages(token, totalToInsert);

            var request = new GetChatMessagesRequest
            {
                AuthToken = token,
                SinceChatMessageId = 0,
                MaxCount = ChatServiceConstants.DEFAULT_PAGE_SIZE
            };

            GetChatMessagesResponse response = chatOperations.GetChatMessages(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Messages);
            Assert.AreEqual(ChatServiceConstants.DEFAULT_PAGE_SIZE, response.Messages.Length);

            for (int i = 1; i < response.Messages.Length; i++)
            {
                Assert.IsTrue(response.Messages[i].ChatMessageId > response.Messages[i - 1].ChatMessageId);
            }
        }


        private void InsertManyMessages(string token, int count)
        {
            if (count < 1)
            {
                Assert.Fail("count must be >= 1.");
            }

            for (int i = 0; i < count; i++)
            {
                var request = new SendChatMessageRequest
                {
                    AuthToken = token,
                    MessageText = DEFAULT_MESSAGE_TEXT
                };

                BasicResponse response = chatOperations.SendChatMessage(request);

                if (response == null || !response.IsSuccess)
                {
                    Assert.Fail("SendChatMessage failed while inserting many messages.");
                }
            }
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


        private static void CleanTokenStore()
        {
            TokenStore.Cache.Clear();
            TokenStore.ActiveTokenByUserId.Clear();
        }

        private int CreateAccountAndUser(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                Assert.Fail("displayName must be provided.");
            }

            string email = BuildUniqueEmail();

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var insertAccount = new SqlCommand(SQL_INSERT_ACCOUNT, sqlConnection))
            using (var insertUser = new SqlCommand(SQL_INSERT_USER, sqlConnection))
            {
                insertAccount.CommandType = CommandType.Text;
                insertUser.CommandType = CommandType.Text;

                insertAccount.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;
                insertUser.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;

                insertAccount.Parameters.Add(PARAM_EMAIL, SqlDbType.NVarChar, EMAIL_MAX_LENGTH).Value = email;
                insertAccount.Parameters.Add(PARAM_PASSWORD_HASH, SqlDbType.NVarChar, PASSWORD_HASH_MAX_LENGTH).Value = PASSWORD_HASH_DUMMY;
                insertAccount.Parameters.Add(PARAM_STATUS, SqlDbType.TinyInt).Value = ACCOUNT_STATUS_ACTIVE;
                insertAccount.Parameters.Add(PARAM_CREATED_AT, SqlDbType.DateTime2).Value = DateTime.UtcNow;

                sqlConnection.Open();

                object accountIdObj = insertAccount.ExecuteScalar();
                if (accountIdObj == null || accountIdObj == DBNull.Value)
                {
                    Assert.Fail("CreateAccount did not return an id.");
                }

                int accountId = Convert.ToInt32(accountIdObj);
                if (accountId <= INVALID_ID)
                {
                    Assert.Fail(string.Format("Invalid accountId='{0}'.", accountId));
                }

                insertUser.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = accountId;
                insertUser.Parameters.Add(PARAM_DISPLAY_NAME, SqlDbType.NVarChar, ChatServiceConstants.MAX_DISPLAYNAME_LENGTH).Value = displayName.Trim();
                insertUser.Parameters.Add(PARAM_CREATED_AT, SqlDbType.DateTime2).Value = DateTime.UtcNow;

                int affectedRows = insertUser.ExecuteNonQuery();
                if (affectedRows != 1)
                {
                    Assert.Fail(string.Format("CreateUser affectedRows='{0}'.", affectedRows));
                }

                return accountId;
            }
        }

        private static string BuildUniqueEmail()
        {
            return EMAIL_PREFIX + Guid.NewGuid().ToString("N") + EMAIL_DOMAIN;
        }


        private static string CreateAndStoreToken(int userId)
        {
            string tokenValue = Guid.NewGuid().ToString("N");

            TokenStore.StoreToken(new AuthToken
            {
                Token = tokenValue,
                UserId = userId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(TOKEN_TTL_MINUTES)
            });

            return tokenValue;
        }

        private static string CreateAndStoreExpiredToken(int userId)
        {
            string tokenValue = Guid.NewGuid().ToString("N");

            TokenStore.StoreToken(new AuthToken
            {
                Token = tokenValue,
                UserId = userId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

            return tokenValue;
        }

        private static FaultException<ServiceFault> AssertThrowsServiceFault(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected FaultException<ServiceFault> but no exception was thrown.");
                return default;
            }
            catch (FaultException<ServiceFault> ex)
            {
                Assert.IsNotNull(ex.Detail);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ex.Detail.Code));
                return ex;
            }
        }
    }
}
