using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class ChatService : IChatService
    {
        private const int MAX_MESSAGE_LENGTH = 500;
        private const int MAX_DISPLAYNAME_LENGTH = 80;
        private const int DEFAULT_PAGE_SIZE = 50;
        private const int MAX_PAGE_SIZE = 200;
        private const int DEFAULT_COMMAND_TIMEOUT_SECONDS = 30;

        private const string COL_CHAT_MESSAGE_ID = "chat_message_id";
        private const string COL_USER_ID = "user_id";
        private const string COL_DISPLAY_NAME = "display_name";
        private const string COL_MESSAGE_TEXT = "message_text";
        private const string COL_SENT_UTC = "sent_utc";

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string ConnectionString
        {
            get
            {
                var connectionString = ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"];
                if (connectionString == null || string.IsNullOrWhiteSpace(connectionString.ConnectionString))
                {
                    ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
                }

                return connectionString.ConnectionString;
            }
        }

        private static void ThrowFault(string code, string message)
        {
            var fault = new ServiceFault { Code = code, Message = message };
            throw new FaultException<ServiceFault>(fault, message);
        }

        private static int EnsureAuthorizedAndGetUserId(string authToken)
        {
            if (string.IsNullOrWhiteSpace(authToken))
            {
                ThrowFault("UNAUTHORIZED", "Auth token is required.");
            }

            if (!TokenCache.TryGetValue(authToken, out var token) || token == null)
            {
                ThrowFault("UNAUTHORIZED", "Auth token is invalid.");
            }

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
            {
                ThrowFault("UNAUTHORIZED", "Auth token has expired.");
            }

            return token.UserId;
        }

        private static string GetUserDisplayName(int userId)
        {
            using (var sqlConnection = new SqlConnection(ConnectionString))
            using (var getDisplayNameCommand = new SqlCommand(ChatSql.Text.SELECT_DISPLAY_NAME, sqlConnection))
            {
                getDisplayNameCommand.CommandType = CommandType.Text;
                getDisplayNameCommand.CommandTimeout = DEFAULT_COMMAND_TIMEOUT_SECONDS;

                getDisplayNameCommand.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = userId });

                sqlConnection.Open();
                var result = getDisplayNameCommand.ExecuteScalar();

                var name = (result == null || result == DBNull.Value) ? null : Convert.ToString(result);
                return string.IsNullOrWhiteSpace(name) ? $"User{userId}" : name.Trim();
            }
        }

        private static int GetInt32OrDefault(SqlDataReader reader, int ordinal, int defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
        }

        private static string GetStringOrDefault(SqlDataReader reader, int ordinal, string defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
        }

        private static DateTime GetDateTimeOrDefault(SqlDataReader reader, int ordinal, DateTime defaultValue)
        {
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetDateTime(ordinal);
        }

        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");
            }

            var messageText = (request.MessageText ?? string.Empty).Trim();
            if (messageText.Length == 0)
            {
                ThrowFault("VALIDATION_ERROR", "MessageText cannot be empty.");
            }

            if (messageText.Length > MAX_MESSAGE_LENGTH)
            {
                ThrowFault("VALIDATION_ERROR", $"MessageText exceeds {MAX_MESSAGE_LENGTH} characters.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.AuthToken);
            var displayName = GetUserDisplayName(userId);

            try
            {
                using (var sqlConnection = new SqlConnection(ConnectionString))
                using (var insertMessageCommand = new SqlCommand(ChatSql.Text.INSERT_CHAT_MESSAGE, sqlConnection))
                {
                    insertMessageCommand.CommandType = CommandType.Text;
                    insertMessageCommand.CommandTimeout = DEFAULT_COMMAND_TIMEOUT_SECONDS;

                    insertMessageCommand.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = userId });
                    insertMessageCommand.Parameters.Add(new SqlParameter("@display_name", SqlDbType.NVarChar, MAX_DISPLAYNAME_LENGTH) { Value = displayName });
                    insertMessageCommand.Parameters.Add(new SqlParameter("@message_text", SqlDbType.NVarChar, MAX_MESSAGE_LENGTH) { Value = messageText });

                    sqlConnection.Open();
                    var affectedRows = insertMessageCommand.ExecuteNonQuery();
                    if (affectedRows != 1)
                    {
                        ThrowFault("DB_ERROR", "Failed to insert chat message.");
                    }
                }

                return new BasicResponse { IsSuccess = true, Message = "Message sent." };
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"[SqlException] Number={ex.Number}, Message={ex.Message}\n{ex}");
                throw;
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"[TimeoutException] {ex.Message}\n{ex}");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                Console.Error.WriteLine($"[OperationCanceledException] {ex.Message}\n{ex}");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"[InvalidOperationException] {ex.Message}\n{ex}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Unexpected] {ex.GetType().Name}: {ex.Message}\n{ex}");
                throw;
            }
        }

        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");
            }

            EnsureAuthorizedAndGetUserId(request.AuthToken);

            var sinceId = request.SinceChatMessageId ?? 0;
            var requestedMax = request.MaxCount.GetValueOrDefault(DEFAULT_PAGE_SIZE);
            var maxCount = Math.Min(Math.Max(requestedMax, 1), MAX_PAGE_SIZE);

            try
            {
                using (var sqlConnection = new SqlConnection(ConnectionString))
                using (var getMessagesCommand = new SqlCommand(ChatSql.Text.SELECT_MESSAGES_PAGED, sqlConnection))
                {
                    getMessagesCommand.CommandType = CommandType.Text;
                    getMessagesCommand.CommandTimeout = DEFAULT_COMMAND_TIMEOUT_SECONDS;

                    getMessagesCommand.Parameters.Add(new SqlParameter("@max_count", SqlDbType.Int) { Value = maxCount });
                    getMessagesCommand.Parameters.Add(new SqlParameter("@since_id", SqlDbType.Int) { Value = sinceId });

                    sqlConnection.Open();

                    using (var reader = getMessagesCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        var ordId = reader.GetOrdinal(COL_CHAT_MESSAGE_ID);
                        var ordUserId = reader.GetOrdinal(COL_USER_ID);
                        var ordDisplayName = reader.GetOrdinal(COL_DISPLAY_NAME);
                        var ordText = reader.GetOrdinal(COL_MESSAGE_TEXT);
                        var ordSentUtc = reader.GetOrdinal(COL_SENT_UTC);

                        var messages = new List<ChatMessageDto>();

                        while (reader.Read())
                        {
                            var message = new ChatMessageDto
                            {
                                ChatMessageId = GetInt32OrDefault(reader, ordId, 0),
                                UserId = GetInt32OrDefault(reader, ordUserId, 0),
                                DisplayName = GetStringOrDefault(reader, ordDisplayName, string.Empty),
                                MessageText = GetStringOrDefault(reader, ordText, string.Empty),
                                SentUtc = GetDateTimeOrDefault(reader, ordSentUtc, DateTime.MinValue)
                            };

                            messages.Add(message);
                        }

                        var lastId = messages.Count == 0 ? sinceId : messages.Last().ChatMessageId;

                        return new GetChatMessagesResponse
                        {
                            Messages = messages.ToArray(),
                            LastChatMessageId = lastId
                        };
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"[SqlException] Number={ex.Number}, Message={ex.Message}\n{ex}");
                throw;
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"[TimeoutException] {ex.Message}\n{ex}");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                Console.Error.WriteLine($"[OperationCanceledException] {ex.Message}\n{ex}");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"[InvalidOperationException] {ex.Message}\n{ex}");
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Unexpected] {ex.GetType().Name}: {ex.Message}\n{ex}");
                throw;
            }
        }
    }
}
