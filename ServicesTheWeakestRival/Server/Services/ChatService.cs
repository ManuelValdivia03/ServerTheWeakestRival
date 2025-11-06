using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using log4net;

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

        private const string MAIN_CONNECTION_STRING_NAME = "TheWeakestRivalDb";

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatService));

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string GetConnectionString()
        {
            var connectionString = ConfigurationManager.ConnectionStrings[MAIN_CONNECTION_STRING_NAME];

            if (connectionString == null || string.IsNullOrWhiteSpace(connectionString.ConnectionString))
            {
                Logger.Error("Missing connection string 'TheWeakestRivalDb'.");
                throw ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
            }

            return connectionString.ConnectionString;
        }

        private static FaultException<ServiceFault> ThrowFault(string code, string message)
        {
            Logger.WarnFormat("ChatService fault. Code='{0}', Message='{1}'", code, message);

            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };

            return new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static void ThrowTechnicalFault(string code, string userMessage, string context, Exception ex)
        {
            Logger.Error(context, ex);

            var fault = new ServiceFault
            {
                Code = code,
                Message = userMessage
            };

            throw new FaultException<ServiceFault>(fault, new FaultReason(userMessage));
        }

        private static int EnsureAuthorizedAndGetUserId(string authToken)
        {
            if (string.IsNullOrWhiteSpace(authToken))
            {
                Logger.Warn("EnsureAuthorizedAndGetUserId: missing auth token.");
                throw ThrowFault("UNAUTHORIZED", "Auth token is required.");
            }

            if (!TokenCache.TryGetValue(authToken, out var token) || token == null)
            {
                Logger.Warn("EnsureAuthorizedAndGetUserId: invalid auth token.");
                throw ThrowFault("UNAUTHORIZED", "Auth token is invalid.");
            }

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
            {
                Logger.WarnFormat("EnsureAuthorizedAndGetUserId: expired token for UserId={0}.", token.UserId);
                throw ThrowFault("UNAUTHORIZED", "Auth token has expired.");
            }

            return token.UserId;
        }

        private static string GetUserDisplayName(int userId)
        {
            using (var sqlConnection = new SqlConnection(GetConnectionString()))
            using (var getDisplayNameCommand = new SqlCommand(ChatSql.Text.SELECT_DISPLAY_NAME, sqlConnection))
            {
                getDisplayNameCommand.CommandType = CommandType.Text;
                getDisplayNameCommand.CommandTimeout = DEFAULT_COMMAND_TIMEOUT_SECONDS;

                getDisplayNameCommand.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = userId });

                sqlConnection.Open();
                var result = getDisplayNameCommand.ExecuteScalar();

                var name = (result == null || result == DBNull.Value) ? null : Convert.ToString(result);
                var finalName = string.IsNullOrWhiteSpace(name) ? "User" + userId : name.Trim();

                Logger.DebugFormat("GetUserDisplayName: UserId={0}, DisplayName={1}", userId, finalName);

                return finalName;
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
                throw ThrowFault("INVALID_REQUEST", "Request cannot be null.");
            }

            var messageText = (request.MessageText ?? string.Empty).Trim();
            if (messageText.Length == 0)
            {
                throw ThrowFault("VALIDATION_ERROR", "MessageText cannot be empty.");
            }

            if (messageText.Length > MAX_MESSAGE_LENGTH)
            {
                throw ThrowFault("VALIDATION_ERROR", "MessageText exceeds " + MAX_MESSAGE_LENGTH + " characters.");
            }

            var userId = EnsureAuthorizedAndGetUserId(request.AuthToken);
            var displayName = GetUserDisplayName(userId);

            Logger.InfoFormat(
                "SendChatMessage: UserId={0}, DisplayName={1}, Length={2}",
                userId,
                displayName,
                messageText.Length);

            try
            {
                using (var sqlConnection = new SqlConnection(GetConnectionString()))
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
                        Logger.ErrorFormat(
                            "SendChatMessage: insert returned unexpected affectedRows={0} for UserId={1}.",
                            affectedRows,
                            userId);

                        throw ThrowFault("DB_ERROR", "Failed to insert chat message.");
                    }
                }

                return new BasicResponse { IsSuccess = true, Message = "Message sent." };
            }
            catch (SqlException ex)
            {
                ThrowTechnicalFault(
                    "DB_ERROR",
                    "A database error occurred while sending the chat message.",
                    "Database error at SendChatMessage.",
                    ex);
            }
            catch (TimeoutException ex)
            {
                ThrowTechnicalFault(
                    "TIMEOUT",
                    "The operation timed out while sending the chat message.",
                    "Timeout at SendChatMessage.",
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                ThrowTechnicalFault(
                    "CANCELLED",
                    "The operation was cancelled while sending the chat message.",
                    "OperationCanceled at SendChatMessage.",
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                ThrowTechnicalFault(
                    "INVALID_OPERATION",
                    "An invalid operation occurred while sending the chat message.",
                    "InvalidOperation at SendChatMessage.",
                    ex);
            }
            catch (Exception ex)
            {
                ThrowTechnicalFault(
                    "UNEXPECTED_ERROR",
                    "An unexpected error occurred while sending the chat message.",
                    "Unexpected error at SendChatMessage.",
                    ex);
            }

            // Inalcanzable, pero requerido por el compilador.
            return new BasicResponse { IsSuccess = false, Message = "Unreachable." };
        }

        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            if (request == null)
            {
                throw ThrowFault("INVALID_REQUEST", "Request cannot be null.");
            }

            EnsureAuthorizedAndGetUserId(request.AuthToken);

            var sinceId = request.SinceChatMessageId ?? 0;
            var requestedMax = request.MaxCount.GetValueOrDefault(DEFAULT_PAGE_SIZE);
            var maxCount = Math.Min(Math.Max(requestedMax, 1), MAX_PAGE_SIZE);

            Logger.DebugFormat(
                "GetChatMessages: SinceId={0}, RequestedMax={1}, EffectiveMax={2}",
                sinceId,
                requestedMax,
                maxCount);

            try
            {
                using (var sqlConnection = new SqlConnection(GetConnectionString()))
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

                        var messageCount = messages.Count;
                        var lastId = messageCount == 0
                            ? sinceId
                            : messages[messageCount - 1].ChatMessageId;

                        Logger.InfoFormat(
                            "GetChatMessages: Returned {0} messages. SinceId={1}, LastId={2}",
                            messageCount,
                            sinceId,
                            lastId);

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
                ThrowTechnicalFault(
                    "DB_ERROR",
                    "A database error occurred while retrieving chat messages.",
                    "Database error at GetChatMessages.",
                    ex);
            }
            catch (TimeoutException ex)
            {
                ThrowTechnicalFault(
                    "TIMEOUT",
                    "The operation timed out while retrieving chat messages.",
                    "Timeout at GetChatMessages.",
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                ThrowTechnicalFault(
                    "CANCELLED",
                    "The operation was cancelled while retrieving chat messages.",
                    "OperationCanceled at GetChatMessages.",
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                ThrowTechnicalFault(
                    "INVALID_OPERATION",
                    "An invalid operation occurred while retrieving chat messages.",
                    "InvalidOperation at GetChatMessages.",
                    ex);
            }
            catch (Exception ex)
            {
                ThrowTechnicalFault(
                    "UNEXPECTED_ERROR",
                    "An unexpected error occurred while retrieving chat messages.",
                    "Unexpected error at GetChatMessages.",
                    ex);
            }

            return new GetChatMessagesResponse
            {
                Messages = Array.Empty<ChatMessageDto>(),
                LastChatMessageId = sinceId
            };
        }
    }
}
