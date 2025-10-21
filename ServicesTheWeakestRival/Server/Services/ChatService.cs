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
    /// <summary>
    /// WCF service that persists and retrieves global lobby chat messages (pre-match).
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public sealed class ChatService : IChatService
    {
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

        /// <summary>
        /// Validates the auth token and returns the associated user id.
        /// </summary>
        private static int EnsureAuthorizedAndGetUserId(string authToken)
        {
            if (string.IsNullOrWhiteSpace(authToken))
                ThrowFault("UNAUTHORIZED", "Auth token is required.");

            if (!TokenCache.TryGetValue(authToken, out var token) || token == null)
                ThrowFault("UNAUTHORIZED", "Auth token is invalid.");

            // Fix: usamos ExpiresAtUtc en vez de IsExpired
            if (token.ExpiresAtUtc <= DateTime.UtcNow)
                ThrowFault("UNAUTHORIZED", "Auth token has expired.");

            return token.UserId;
        }

        /// <summary>
        /// Retrieves the user's display name; falls back to "User{userId}" on missing/DB error.
        /// </summary>
        private static string GetUserDisplayName(int userId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand("SELECT display_name FROM dbo.Users WHERE user_id = @id;", conn))
                {
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = userId;
                    conn.Open();
                    var obj = cmd.ExecuteScalar();
                    var name = (obj == null || obj == DBNull.Value) ? null : Convert.ToString(obj);
                    return string.IsNullOrWhiteSpace(name) ? $"User{userId}" : name.Trim();
                }
            }
            catch
            {
                return $"User{userId}";
            }
        }

        // === Operations ===

        /// <inheritdoc />
        public BasicResponse SendChatMessage(SendChatMessageRequest request)
        {
            if (request == null)
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");

            var messageText = (request.MessageText ?? string.Empty).Trim();
            if (messageText.Length == 0)
                ThrowFault("VALIDATION_ERROR", "MessageText cannot be empty.");
            if (messageText.Length > 500)
                ThrowFault("VALIDATION_ERROR", "MessageText exceeds 500 characters.");

            var userId = EnsureAuthorizedAndGetUserId(request.AuthToken);
            var displayName = GetUserDisplayName(userId);

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(@"
INSERT INTO dbo.ChatMessages (user_id, display_name, message_text)
VALUES (@user_id, @display_name, @message_text);", conn))
                {
                    cmd.Parameters.Add("@user_id", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@display_name", SqlDbType.NVarChar, 80).Value = displayName;
                    cmd.Parameters.Add("@message_text", SqlDbType.NVarChar, 500).Value = messageText;

                    conn.Open();
                    var affected = cmd.ExecuteNonQuery();
                    if (affected != 1)
                        ThrowFault("DB_ERROR", "Failed to insert chat message.");
                }

                return new BasicResponse { IsSuccess = true, Message = "Message sent." };
            }
            catch (SqlException ex)
            {
                ThrowFault("DB_ERROR", $"SQL error: {ex.Number}");
                throw; // unreachable
            }
            catch (Exception)
            {
                ThrowFault("UNEXPECTED", "Unexpected server error.");
                throw; // unreachable
            }
        }

        /// <inheritdoc />
        public GetChatMessagesResponse GetChatMessages(GetChatMessagesRequest request)
        {
            if (request == null)
                ThrowFault("INVALID_REQUEST", "Request cannot be null.");

            // Validar token solamente
            EnsureAuthorizedAndGetUserId(request.AuthToken);

            var sinceId = request.SinceChatMessageId ?? 0;
            var maxCount = request.MaxCount.HasValue && request.MaxCount.Value > 0
                ? Math.Min(request.MaxCount.Value, 200)
                : 50;

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(@"
SELECT TOP (@max)
       chat_message_id,
       user_id,
       display_name,
       message_text,
       sent_utc
FROM dbo.ChatMessages
WHERE chat_message_id > @since_id
ORDER BY chat_message_id ASC;", conn))
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
                ThrowFault("DB_ERROR", $"SQL error: {ex.Number}");
                throw; 
            }
            catch (Exception)
            {
                ThrowFault("UNEXPECTED", "Unexpected server error.");
                throw; 
            }
        }
    }
}
