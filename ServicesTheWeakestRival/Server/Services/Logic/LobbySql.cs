namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class LobbySql
    {
        public static class Text
        {
            public const string SP_LOBBY_LEAVE = "dbo.usp_Lobby_Leave";
            public const string SP_LOBBY_LEAVE_ALL_BY_USER = "dbo.usp_Lobby_LeaveAllByUser";
            public const string SP_LOBBY_CREATE = "dbo.usp_Lobby_Create";
            public const string SP_LOBBY_JOIN_BY_CODE = "dbo.usp_Lobby_JoinByCode";

            public const string GET_MY_PROFILE = @"
                SELECT
                    u.user_id,
                    u.display_name,
                    u.profile_image,
                    u.profile_image_content_type,
                    u.created_at,
                    a.email
                FROM dbo.Users u
                JOIN dbo.Accounts a ON a.account_id = u.user_id
                WHERE u.user_id = @Id;";

            public const string EMAIL_EXISTS_EXCEPT_ID = @"
                SELECT 1
                FROM dbo.Accounts
                WHERE email = @E AND account_id <> @Id;";

            public const string UPDATE_ACCOUNT_EMAIL = @"
                UPDATE dbo.Accounts
                SET email = @E
                WHERE account_id = @Id;";

            public const string GET_LOBBY_ID_FROM_UID = @"
                SELECT lobby_id
                FROM dbo.Lobbies
                WHERE lobby_uid = @u;";

            public const string GET_LOBBY_BY_ID = @"
                SELECT lobby_uid, name, max_players, access_code
                FROM dbo.Lobbies
                WHERE lobby_id = @id;";

            public const string GET_USER_DISPLAY_NAME = @"
                SELECT display_name
                FROM dbo.Users
                WHERE user_id = @Id;";

            public const string GET_LOBBY_PLAYERS = @"
                SELECT
                    lp.account_id,
                    u.display_name
                FROM dbo.LobbyPlayers lp
                JOIN dbo.Users u ON u.user_id = lp.account_id
                WHERE lp.lobby_id = @LobbyId;";

            public const string GET_LOBBY_MEMBERS_WITH_USERS = @"
                SELECT
                    lm.lobby_id,
                    lm.user_id,
                    lm.role,
                    lm.joined_at_utc,
                    lm.left_at_utc,
                    lm.is_active,
                    u.user_id,
                    u.display_name,
                    u.profile_image,
                    u.profile_image_content_type
                FROM dbo.LobbyMembers lm
                JOIN dbo.Users u ON lm.user_id = u.user_id
                WHERE lm.lobby_id = @LobbyId AND lm.is_active = 1;";
        }

        private const string SQL_GET_ACTIVE_LOBBY_IDS_FOR_USER =
            @"SELECT DISTINCT lobby_id
              FROM dbo.LobbyMembers
              WHERE user_id = @UserId
                AND is_active = 1
                AND left_at_utc IS NULL;";

        private const string COLUMN_DISPLAY_NAME = "display_name";
        private const string COLUMN_PROFILE_IMAGE = "profile_image";
        private const string COLUMN_PROFILE_IMAGE_CONTENT_TYPE = "profile_image_content_type";

        private const string PARAM_DISPLAY_NAME = "@DisplayName";
        private const string PARAM_IMAGE_BYTES = "@ImageBytes";
        private const string PARAM_IMAGE_CONTENT_TYPE = "@ImageContentType";

        public static string BuildUpdateUser(bool setName, bool setImg)
        {
            if (!setName && !setImg)
            {
                return null;
            }

            var sql = "UPDATE dbo.Users SET ";

            if (setName)
            {
                sql += COLUMN_DISPLAY_NAME + " = " + PARAM_DISPLAY_NAME;
            }

            if (setImg)
            {
                sql += (setName ? ", " : string.Empty)
                    + COLUMN_PROFILE_IMAGE + " = " + PARAM_IMAGE_BYTES + ", "
                    + COLUMN_PROFILE_IMAGE_CONTENT_TYPE + " = " + PARAM_IMAGE_CONTENT_TYPE;
            }

            sql += " WHERE user_id = @Id;";
            return sql;
        }
    }
}
