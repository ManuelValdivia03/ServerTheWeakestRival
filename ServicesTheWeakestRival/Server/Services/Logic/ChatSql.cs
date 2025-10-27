using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    /// <summary>
    /// SQL usado por ChatService. Solo constantes (sin acceso a datos).
    /// </summary>
    public static class ChatSql
    {
        public static class Text
        {
            public const string SELECT_DISPLAY_NAME = @"
                SELECT display_name FROM dbo.Users WHERE user_id = @user_id;";

            public const string INSERT_CHAT_MESSAGE = @"
                INSERT INTO dbo.ChatMessages (user_id, display_name, message_text)
                VALUES (@user_id, @display_name, @message_text);";

            public const string SELECT_MESSAGES_PAGED = @"
                SELECT TOP (@max_count)
                    chat_message_id,
                    user_id,
                    display_name,
                    message_text,
                    sent_utc
                FROM dbo.ChatMessages
                WHERE chat_message_id > @since_id
                ORDER BY chat_message_id ASC;";
        }
    }
}

