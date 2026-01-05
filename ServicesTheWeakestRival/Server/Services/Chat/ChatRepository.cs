using log4net;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Infrastructure;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Chat
{
    public sealed class ChatRepository
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ChatRepository));

        private readonly Func<string> connectionStringProvider;

        public ChatRepository(Func<string> connectionStringProvider)
        {
            this.connectionStringProvider = connectionStringProvider ?? throw new ArgumentNullException(nameof(connectionStringProvider));
        }

        public string GetUserDisplayName(int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionStringProvider()))
            using (var command = new SqlCommand(ChatSql.Text.SELECT_DISPLAY_NAME, sqlConnection))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;

                command.Parameters.Add(ChatServiceConstants.PARAM_USER_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();
                var result = command.ExecuteScalar();

                var name = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                var finalName = string.IsNullOrWhiteSpace(name)
                    ? string.Format("{0}{1}", ChatServiceConstants.DEFAULT_USER_PREFIX, userId)
                    : name.Trim();

                if (finalName.Length > ChatServiceConstants.MAX_DISPLAYNAME_LENGTH)
                {
                    finalName = finalName.Substring(0, ChatServiceConstants.MAX_DISPLAYNAME_LENGTH);
                }

                Logger.DebugFormat(
                    "GetUserDisplayName: UserId={0}, DisplayName={1}",
                    userId,
                    finalName);

                return finalName;
            }
        }

        public void InsertChatMessage(int userId, string displayName, string messageText)
        {
            using (var sqlConnection = new SqlConnection(connectionStringProvider()))
            using (var command = new SqlCommand(ChatSql.Text.INSERT_CHAT_MESSAGE, sqlConnection))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;

                command.Parameters.Add(ChatServiceConstants.PARAM_USER_ID, SqlDbType.Int).Value = userId;
                command.Parameters.Add(ChatServiceConstants.PARAM_DISPLAY_NAME, SqlDbType.NVarChar, ChatServiceConstants.MAX_DISPLAYNAME_LENGTH).Value = displayName ?? string.Empty;
                command.Parameters.Add(ChatServiceConstants.PARAM_MESSAGE_TEXT, SqlDbType.NVarChar, ChatServiceConstants.MAX_MESSAGE_LENGTH).Value = messageText ?? string.Empty;

                sqlConnection.Open();

                var affectedRows = command.ExecuteNonQuery();
                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        string.Format("InsertChatMessage affectedRows={0}.", affectedRows));
                }
            }
        }

        public ChatPageResult GetMessagesPaged(int maxCount, int sinceId)
        {
            using (var sqlConnection = new SqlConnection(connectionStringProvider()))
            using (var command = new SqlCommand(ChatSql.Text.SELECT_MESSAGES_PAGED, sqlConnection))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = ChatServiceConstants.DEFAULT_COMMAND_TIMEOUT_SECONDS;

                command.Parameters.Add(ChatServiceConstants.PARAM_MAX_COUNT, SqlDbType.Int).Value = maxCount;
                command.Parameters.Add(ChatServiceConstants.PARAM_SINCE_ID, SqlDbType.Int).Value = sinceId;

                sqlConnection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    var ordId = reader.GetOrdinal(ChatServiceConstants.COL_CHAT_MESSAGE_ID);
                    var ordUserId = reader.GetOrdinal(ChatServiceConstants.COL_USER_ID);
                    var ordDisplayName = reader.GetOrdinal(ChatServiceConstants.COL_DISPLAY_NAME);
                    var ordText = reader.GetOrdinal(ChatServiceConstants.COL_MESSAGE_TEXT);
                    var ordSentUtc = reader.GetOrdinal(ChatServiceConstants.COL_SENT_UTC);

                    var messages = new List<ChatMessageDto>();

                    while (reader.Read())
                    {
                        var message = new ChatMessageDto
                        {
                            ChatMessageId = reader.GetInt32OrDefault(ordId, 0),
                            UserId = reader.GetInt32OrDefault(ordUserId, 0),
                            DisplayName = reader.GetStringOrDefault(ordDisplayName, string.Empty),
                            MessageText = reader.GetStringOrDefault(ordText, string.Empty),
                            SentUtc = reader.GetDateTimeOrDefault(ordSentUtc, DateTime.MinValue)
                        };

                        messages.Add(message);
                    }

                    var lastId = messages.Count == 0 ? sinceId : messages[messages.Count - 1].ChatMessageId;

                    return new ChatPageResult(messages, lastId);
                }
            }
        }
    }

    public sealed class ChatPageResult
    {
        public ChatPageResult(List<ChatMessageDto> messages, int lastChatMessageId)
        {
            Messages = messages ?? new List<ChatMessageDto>();
            LastChatMessageId = lastChatMessageId;
        }

        public List<ChatMessageDto> Messages { get; }

        public int LastChatMessageId { get; }
    }
}
