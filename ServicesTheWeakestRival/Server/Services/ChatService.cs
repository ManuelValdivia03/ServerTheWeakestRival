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

namespace ServicesTheWeakestRival.Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class ChatService : IChatService
    {
        private const int MaxMessageLength = 500;
        private const int MaxDisplayNameLength = 80;
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;

        private const string SqlSelectDisplayName =
            "SELECT display_name FROM dbo.Users WHERE user_id = @id;";

        private const string SqlInsertChatMessage = @"
            INSERT INTO dbo.ChatMessages (user_id, display_name, message_text)
            VALUES (@user_id, @display_name, @message_text);";

        private const string SqlSelectMessagesPaged = @"
            SELECT TOP (@max)
                chat_message_id,
                user_id,
                display_name,
                message_text,
                sent_utc
            FROM dbo.ChatMessages
            WHERE chat_message_id > @since_id
            ORDER BY chat_message_id ASC;";

        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache;

        private static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    ThrowFault("CONFIG_ERROR", "Missing connection string 'TheWeakestRivalDb'.");
                return cs.ConnectionString;
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
                ThrowFault("UNAUTHORIZED", "Auth token is required.");

            if (!TokenCache.TryGetValue(authToken, out var token) || token == null)
                ThrowFault("UNAUTHORIZED", "Auth token is invalid.");

            if (token.ExpiresAtUtc <= DateTime.UtcNow)
                ThrowFault("UNAUTHORIZED", "Auth token has expired.");

            return token.UserId;
        }

        private static string GetUserDisplayName(int userId)
        {
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(SqlSelectDisplayName, conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId;
                conn.Open();

                var obj = cmd.ExecuteScalar();
                var name = (obj == null || obj == DBNull.Value) ? null : Convert.ToString(obj);

                return string.IsNullOrWhiteSpace(name) ? $"User{userId}" : name.Trim();
            }
        }

        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            if (request == null)
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");

            var messageText = (request.MessageText ?? string.Empty).Trim();
            if (messageText.Length == 0)
                ThrowFault("VALIDATION_ERROR", "MessageText cannot be empty.");
            if (messageText.Length > MaxMessageLength)
                ThrowFault("VALIDATION_ERROR", $"MessageText exceeds {MaxMessageLength} characters.");

            var userId = EnsureAuthorizedAndGetUserId(request.AuthToken);
            var displayName = GetUserDisplayName(userId);

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(SqlInsertChatMessage, conn))
                {
                    cmd.Parameters.Add("@user_id", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@display_name", SqlDbType.NVarChar, MaxDisplayNameLength).Value = displayName;
                    cmd.Parameters.Add("@message_text", SqlDbType.NVarChar, MaxMessageLength).Value = messageText;

                    conn.Open();
                    var affected = cmd.ExecuteNonQuery();
                    if (affected != 1)
                        ThrowFault("DB_ERROR", "Failed to insert chat message.");
                }

                return new BasicResponse { IsSuccess = true, Message = "Message sent." };
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"[SqlException] Number={ex.Number}, Message={ex.Message}\n{ex}");
                throw; // no enmascarar
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"[TimeoutException] {ex.Message}\n{ex}");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                Console.Error.WriteLine($"[OperationCanceled] {ex.Message}\n{ex}");
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
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");

            EnsureAuthorizedAndGetUserId(request.AuthToken);

            var sinceId = request.SinceChatMessageId ?? 0;
            var requestedMax = request.MaxCount.GetValueOrDefault(DefaultPageSize);
            var maxCount = Math.Min(Math.Max(requestedMax, 1), MaxPageSize);

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(SqlSelectMessagesPaged, conn))
                {
                    cmd.Parameters.Add("@max", SqlDbType.Int).Value = maxCount;
                    cmd.Parameters.Add("@since_id", SqlDbType.Int).Value = sinceId;

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new List<ChatMessageDto>();
                        while (reader.Read())
                        {
                            items.Add(new ChatMessageDto
                            {
                                ChatMessageId = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                DisplayName = reader.GetString(2),
                                MessageText = reader.GetString(3),
                                SentUtc = reader.GetDateTime(4)
                            });
                        }

                        var lastId = items.Count == 0 ? sinceId : items.Last().ChatMessageId;
                        return new GetChatMessagesResponse
                        {
                            Messages = items.ToArray(),
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
                Console.Error.WriteLine($"[OperationCanceled] {ex.Message}\n{ex}");
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
