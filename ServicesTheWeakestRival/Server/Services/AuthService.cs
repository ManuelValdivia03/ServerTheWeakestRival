using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.ServiceModel;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;
using BCrypt.Net;

namespace ServicesTheWeakestRival.Server.Services
{
    public sealed class AuthService : IAuthService
    {
        private static ConcurrentDictionary<string, AuthToken> TokenCache => TokenStore.Cache; private static string ConnectionString =>


            ConfigurationManager.ConnectionStrings["TheWeakestRivalDb"].ConnectionString;
        
        public PingResponse Ping(PingRequest request)
        {
            return new PingResponse
            {
                Echo = request?.Message ?? "pong",
                Utc = DateTime.UtcNow
            };
        }

        public RegisterResponse Register(RegisterRequest request)
        {
            string QueryVerifyIsNotRegistered = @"
                SELECT account_id FROM dbo.Accounts WHERE email = @Email;";

            string QueryInsertAccount = @"
                INSERT INTO dbo.Accounts (email, password_hash, status, created_at)
                OUTPUT INSERTED.account_id
                VALUES (@Email, @PasswordHash, 1, SYSUTCDATETIME());";

            string QueryInsertUser = @"
                INSERT INTO dbo.Users (user_id, display_name, profile_image_url, created_at)
                VALUES (@UserId, @DisplayName, @ProfileImageUrl, SYSUTCDATETIME());";

            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            using (var connection = new SqlConnection(ConnectionString))
            using (var sqlCommand = new SqlCommand(QueryVerifyIsNotRegistered, connection))
            {
                sqlCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;

                connection.Open();
                var exists = sqlCommand.ExecuteScalar();
                if (exists != null)
                {
                    ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                }
            }

            var passwordHash = HashPasswordBc(request.Password);

            int newAccountId = -1;
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        
                        using (var sqlCommandInsertAccount = new SqlCommand(QueryInsertAccount, connection, transaction))
                        {
                            sqlCommandInsertAccount.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;
                            sqlCommandInsertAccount.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                            newAccountId = Convert.ToInt32(sqlCommandInsertAccount.ExecuteScalar());
                        }

                        using (var sqlCommandInsertUser = new SqlCommand(QueryInsertUser, connection, transaction))
                        {
                            sqlCommandInsertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                            sqlCommandInsertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 80).Value = (object)request.DisplayName ?? DBNull.Value;
                            sqlCommandInsertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, 500).Value =
                                string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? (object)DBNull.Value : request.ProfileImageUrl;

                            sqlCommandInsertUser.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (SqlException sqlException)
                    {
                        transaction.Rollback();

                        if (IsUniqueViolation(sqlException))
                        {
                            ThrowFault("EMAIL_TAKEN", "Email is already registered.");
                        }

                        ThrowFault("DB_ERROR", $"Unexpected database error: {sqlException.Number}");
                    }
                }
            }

            var token = IssueToken(newAccountId);

            return new RegisterResponse
            {
                UserId = newAccountId,
                Token = token
            };
        }

        public LoginResponse Login(LoginRequest request)
        {
            if (request == null)
            {
                ThrowFault("INVALID_REQUEST", "Request payload is null.");
            }

            string QueryGetAccountByEmail = @"
                SELECT account_id, password_hash, status
                FROM dbo.Accounts
                WHERE email = @Email;";

            int userId;
            string storedHash;
            byte status;

            using (var connection = new SqlConnection(ConnectionString))
            using (var sqlCommand = new SqlCommand(QueryGetAccountByEmail, connection))
            {
                sqlCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = request.Email;
                connection.Open();

                using (var sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!sqlDataReader.Read())
                    {
                        ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");
                    }

                    userId = sqlDataReader.GetInt32(0);
                    storedHash = sqlDataReader.GetString(1);
                    status = sqlDataReader.GetByte(2);
                }
            }

            if (status == 0)
            {
                ThrowFault("ACCOUNT_BLOCKED", "Account is blocked.");
            }

            if (!VerifyPasswordBc(request.Password, storedHash))
            {
                ThrowFault("INVALID_CREDENTIALS", "Email or password is incorrect.");

            }
            var token = IssueToken(userId);
            return new LoginResponse { Token = token };
        }

        public void Logout(LogoutRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
            {
                return;
            }

            TokenCache.TryRemove(request.Token, out _);
        }

        private static AuthToken IssueToken(int userId)
        {
            var tokenValue = Guid.NewGuid().ToString("N");
            var expiry = DateTime.UtcNow.AddHours(24);

            var token = new AuthToken
            {
                UserId = userId,
                Token = tokenValue,
                ExpiresAtUtc = expiry
            };

            TokenCache[tokenValue] = token;
            return token;
        }

        private static void ThrowFault(string code, string message)
        {
            var fault = new ServiceFault
            {
                Code = code,
                Message = message
            };
            throw new FaultException<ServiceFault>(fault, new FaultReason(message));
        }

        private static bool IsUniqueViolation(SqlException ex)
        {
            return ex?.Number == 2627 || ex?.Number == 2601;
        }


        private static string HashPasswordBc(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? string.Empty, workFactor: 10);
        }

        private static bool VerifyPasswordBc(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);
        }
    }

    
    
}
