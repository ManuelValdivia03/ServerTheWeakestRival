using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    
    public static class AuthSql
    {
        public static class Text
        {

            public const string EXISTS_ACCOUNT_BY_EMAIL = @"
                SELECT 1 FROM dbo.Accounts WHERE email = @Email;";

            public const string LAST_VERIFICATION = @"
                SELECT TOP(1) created_at_utc
                FROM dbo.EmailVerifications
                WHERE email = @Email AND used = 0
                ORDER BY created_at_utc DESC;";

            public const string INVALIDATE_PENDING_VERIFICATIONS = @"
                UPDATE dbo.EmailVerifications
                SET used = 1, used_at_utc = SYSUTCDATETIME()
                WHERE email = @Email AND used = 0;";

            public const string INSERT_VERIFICATION = @"
                INSERT INTO dbo.EmailVerifications (email, code_hash, expires_at_utc)
                VALUES (@Email, @CodeHash, @ExpiresAtUtc);";

            public const string PICK_LATEST_VERIFICATION = @"
                SELECT TOP(1) verification_id, expires_at_utc, used
                FROM dbo.EmailVerifications
                WHERE email = @Email AND used = 0
                ORDER BY created_at_utc DESC;";

            public const string VALIDATE_VERIFICATION = @"
                SELECT CASE WHEN code_hash = @Hash THEN 1 ELSE 0 END
                FROM dbo.EmailVerifications
                WHERE verification_id = @Id;";

            public const string MARK_VERIFICATION_USED = @"
                UPDATE dbo.EmailVerifications
                SET used = 1, used_at_utc = SYSUTCDATETIME()
                WHERE verification_id = @Id;";

            public const string INCREMENT_ATTEMPTS = @"
                UPDATE dbo.EmailVerifications
                SET attempts = attempts + 1
                WHERE verification_id = @Id;";


            public const string INSERT_ACCOUNT = @"
                INSERT INTO dbo.Accounts (email, password_hash, status, created_at)
                OUTPUT INSERTED.account_id
                VALUES (@Email, @PasswordHash, @Status, SYSUTCDATETIME());";

            public const string INSERT_USER = @"
                INSERT INTO dbo.Users (user_id, display_name, profile_image_url, created_at)
                VALUES (@UserId, @DisplayName, @ProfileImageUrl, SYSUTCDATETIME());";


            public const string GET_ACCOUNT_BY_EMAIL = @"
                SELECT account_id, password_hash, status
                FROM dbo.Accounts
                WHERE email = @Email;";

            
            public const string SP_LOBBY_LEAVE_ALL_BY_USER = "dbo.usp_Lobby_LeaveAllByUser";
        }
    }
}

