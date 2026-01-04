using System.Text;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public static class FriendSql
    {
        public static class Text
        {
            public const string EXISTS_FRIEND = @"
                SELECT 1 
                FROM dbo.FriendRequests 
                WHERE ((from_user_id = @Me AND to_user_id = @Target) OR (from_user_id = @Target AND to_user_id = @Me))
                  AND status = @Accepted;";

            public const string PENDING_OUT = @"
                SELECT friend_request_id 
                FROM dbo.FriendRequests 
                WHERE from_user_id = @Me AND to_user_id = @Target AND status = @Pending;";

            public const string PENDING_IN = @"
                SELECT friend_request_id 
                FROM dbo.FriendRequests 
                WHERE from_user_id = @Target AND to_user_id = @Me AND status = @Pending;";

            public const string ACCEPT_INCOMING = @"
                UPDATE dbo.FriendRequests
                SET status = @Accepted, responded_at = SYSUTCDATETIME()
                OUTPUT INSERTED.friend_request_id
                WHERE friend_request_id = @ReqId;";

            public const string INSERT_REQUEST = @"
                INSERT INTO dbo.FriendRequests (from_user_id, to_user_id, status, sent_at, responded_at)
                OUTPUT INSERTED.friend_request_id
                VALUES (@Me, @Target, @Pending, SYSUTCDATETIME(), NULL);";

            public const string REOPEN_REQUEST = @"
                UPDATE dbo.FriendRequests
                SET status = @Pending, sent_at = SYSUTCDATETIME(), responded_at = NULL
                OUTPUT INSERTED.friend_request_id
                WHERE from_user_id = @Me AND to_user_id = @Target AND status IN (@Declined, @Cancelled);";

            public const string CHECK_REQUEST = @"
                SELECT friend_request_id, from_user_id, to_user_id, status
                FROM dbo.FriendRequests
                WHERE friend_request_id = @Id;";

            public const string ACCEPT_REQUEST = @"
                UPDATE dbo.FriendRequests
                SET status = @Accepted, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND to_user_id = @Me
                  AND status = @Pending;";

            public const string GET_REQUEST = CHECK_REQUEST;

            public const string REJECT_REQUEST = @"
                UPDATE dbo.FriendRequests
                SET status = @Rejected, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND to_user_id = @Me
                  AND status = @Pending;";

            public const string CANCEL_REQUEST = @"
                UPDATE dbo.FriendRequests
                SET status = @Cancelled, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id
                  AND from_user_id = @Me
                  AND status = @Pending;";

            public const string LATEST_ACCEPTED = @"
                SELECT TOP(1) friend_request_id
                FROM dbo.FriendRequests
                WHERE status = @Accepted
                  AND ((from_user_id = @Me AND to_user_id = @Other) OR (from_user_id = @Other AND to_user_id = @Me))
                ORDER BY responded_at DESC;";

            public const string MARK_CANCELLED = @"
                UPDATE dbo.FriendRequests
                SET status = @Cancelled, responded_at = SYSUTCDATETIME()
                WHERE friend_request_id = @Id;";

            public const string FRIENDS = @"
                SELECT from_user_id, to_user_id, responded_at
                FROM dbo.FriendRequests
                WHERE status = @Accepted
                  AND (from_user_id = @Me OR to_user_id = @Me);";

            public const string PENDING_INCOMING = @"
                SELECT friend_request_id, from_user_id, to_user_id, sent_at
                FROM dbo.FriendRequests
                WHERE to_user_id = @Me AND status = @Pending
                ORDER BY sent_at DESC;";

            public const string PENDING_OUTGOING = @"
                SELECT friend_request_id, from_user_id, to_user_id, sent_at
                FROM dbo.FriendRequests
                WHERE from_user_id = @Me AND status = @Pending
                ORDER BY sent_at DESC;";

            public const string PRESENCE_UPDATE = @"
                UPDATE dbo.UserPresence
                SET last_seen_utc = SYSUTCDATETIME(), device = @Dev
                WHERE user_id = @Me;";

            public const string PRESENCE_INSERT = @"
                INSERT INTO dbo.UserPresence (user_id, last_seen_utc, device)
                VALUES (@Me, SYSUTCDATETIME(), @Dev);";

            public const string FRIENDS_PRESENCE = @"
                DECLARE @now DATETIME2(3) = SYSUTCDATETIME();
                SELECT F.friend_id,
                       CASE WHEN P.last_seen_utc IS NOT NULL
                                 AND P.last_seen_utc >= DATEADD(SECOND, -@Window, @now)
                            THEN 1 ELSE 0 END AS is_online,
                       P.last_seen_utc
                FROM (
                    SELECT CASE WHEN fr.from_user_id = @Me THEN fr.to_user_id ELSE fr.from_user_id END AS friend_id
                    FROM dbo.FriendRequests fr
                    WHERE fr.status = @Accepted AND (fr.from_user_id = @Me OR fr.to_user_id = @Me)
                ) F
                LEFT JOIN dbo.UserPresence P ON P.user_id = F.friend_id
                ORDER BY F.friend_id;";

            public const string FRIEND_SUMMARY = @"
                SELECT a.account_id, a.email, u.display_name, u.profile_image_url, p.last_seen_utc
                FROM dbo.Accounts a
                LEFT JOIN dbo.Users u ON u.user_id = a.account_id
                LEFT JOIN dbo.UserPresence p ON p.user_id = a.account_id
                WHERE a.account_id = @Id;";

            public const string SEARCH_ACCOUNTS = @"
                SELECT TOP(@Max)
                    a.account_id,
                    ISNULL(u.display_name, a.email) AS display_name,
                    a.email,
                    u.profile_image_url,
                    -- flags
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 1
                          AND ((fr.from_user_id = @Me AND fr.to_user_id = a.account_id)
                            OR (fr.from_user_id = a.account_id AND fr.to_user_id = @Me))
                    ) THEN 1 ELSE 0 END AS is_friend,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = @Me AND fr.to_user_id = a.account_id
                    ) THEN 1 ELSE 0 END AS has_outgoing,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = a.account_id AND fr.to_user_id = @Me
                    ) THEN 1 ELSE 0 END AS has_incoming,
                    (
                        SELECT TOP(1) fr.friend_request_id
                        FROM dbo.FriendRequests fr
                        WHERE fr.status = 0 AND fr.from_user_id = a.account_id AND fr.to_user_id = @Me
                        ORDER BY fr.sent_at DESC
                    ) AS incoming_id
                FROM dbo.Accounts a
                LEFT JOIN dbo.Users u ON u.user_id = a.account_id
                WHERE a.account_id <> @Me
                  AND (a.email LIKE @Qemail OR ISNULL(u.display_name, '') LIKE @Qname)
                ORDER BY display_name;";
        }

        public static string BuildGetAccountsByIdsQuery(int paramCount)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < paramCount; i++)
            {
                if (i > 0) builder.Append(",");
                builder.Append("@p").Append(i);
            }

            var inList = builder.ToString();
            return
                "SELECT a.account_id, ISNULL(u.display_name, a.email) AS display_name, a.email, u.profile_image_url " +
                "FROM dbo.Accounts a LEFT JOIN dbo.Users u ON u.user_id = a.account_id " +
                $"WHERE a.account_id IN ({inList}) AND a.account_id <> @Me";
        }
    }
}
