using System;
using System.Data;
using System.Data.SqlClient;
using ServicesTheWeakestRival.Server.Services.Logic;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public sealed class AuthRepository
    {
        private readonly Func<string> connectionStringProvider;

        public AuthRepository(Func<string> connectionStringProvider)
        {
            this.connectionStringProvider = connectionStringProvider ?? throw new ArgumentNullException(nameof(connectionStringProvider));
        }

        public void CreateRegisterVerification(string email, byte[] codeHash, DateTime expiresAtUtc)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection, transaction))
                    {
                        exists.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        var found = exists.ExecuteScalar();
                        if (found != null)
                        {
                            throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_EMAIL_TAKEN, "Email is already registered.");
                        }
                    }

                    using (var last = new SqlCommand(AuthSql.Text.LAST_VERIFICATION, connection, transaction))
                    {
                        last.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        object lastObj = last.ExecuteScalar();
                        DateTime? lastUtc = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;

                        bool isInCooldown = lastUtc.HasValue &&
                                            (DateTime.UtcNow - lastUtc.Value).TotalSeconds < AuthServiceContext.ResendCooldownSeconds;
                        if (isInCooldown)
                        {
                            throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_TOO_SOON, "Please wait before requesting another code.");
                        }
                    }

                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_VERIFICATIONS, connection, transaction))
                    {
                        invalidate.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_VERIFICATION, connection, transaction))
                    {
                        insert.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        insert.Parameters.Add("@CodeHash", SqlDbType.VarBinary, 32).Value = codeHash;
                        insert.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expiresAtUtc;
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public void CreatePasswordResetRequest(string email, byte[] codeHash, DateTime expiresAtUtc)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection, transaction))
                    {
                        exists.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        var found = exists.ExecuteScalar();
                        if (found == null)
                        {
                            throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_EMAIL_NOT_FOUND, "No account is registered with that email.");
                        }
                    }

                    using (var last = new SqlCommand(AuthSql.Text.LAST_RESET_REQUEST, connection, transaction))
                    {
                        last.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        object lastObj = last.ExecuteScalar();
                        DateTime? lastUtc = (lastObj == null || lastObj == DBNull.Value) ? (DateTime?)null : (DateTime)lastObj;

                        bool isInCooldown = lastUtc.HasValue &&
                                            (DateTime.UtcNow - lastUtc.Value).TotalSeconds < AuthServiceContext.ResendCooldownSeconds;
                        if (isInCooldown)
                        {
                            throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_TOO_SOON, "Please wait before requesting another code.");
                        }
                    }

                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_RESETS, connection, transaction))
                    {
                        invalidate.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_RESET_REQUEST, connection, transaction))
                    {
                        insert.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        insert.Parameters.Add("@CodeHash", SqlDbType.VarBinary, 32).Value = codeHash;
                        insert.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expiresAtUtc;
                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public void ReadLatestVerification(string email, out int verificationId, out DateTime expiresAtUtc, out bool used)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_VERIFICATION, connection))
            {
                pick.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_MISSING, "No pending code. Request a new one.");
                    }

                    verificationId = reader.GetInt32(0);
                    expiresAtUtc = reader.GetDateTime(1);
                    used = reader.GetBoolean(2);
                }
            }
        }

        public void ReadLatestReset(string email, out int resetId, out DateTime expiresAtUtc, out bool used)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_RESET, connection))
            {
                pick.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_MISSING, "No pending reset code. Request a new one.");
                    }

                    resetId = reader.GetInt32(0);
                    expiresAtUtc = reader.GetDateTime(1);
                    used = reader.GetBoolean(2);
                }
            }
        }

        public void ValidateVerificationCodeOrThrow(int verificationId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_VERIFICATION, connection))
                {
                    validate.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    validate.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;

                    bool ok = Convert.ToInt32(validate.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_ATTEMPTS, connection))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                            inc.ExecuteNonQuery();
                        }

                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_INVALID, "Invalid verification code.");
                    }
                }
            }
        }

        public void ValidateResetCodeOrThrow(int resetId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_RESET, connection))
                {
                    validate.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                    validate.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = codeHash;

                    bool ok = Convert.ToInt32(validate.ExecuteScalar()) == 1;
                    if (!ok)
                    {
                        using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_RESET_ATTEMPTS, connection))
                        {
                            inc.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                            inc.ExecuteNonQuery();
                        }

                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_CODE_INVALID, "Invalid reset code.");
                    }
                }
            }
        }

        public void EnsureAccountDoesNotExist(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var check = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection))
                {
                    check.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                    var exists = check.ExecuteScalar();
                    if (exists != null)
                    {
                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_EMAIL_TAKEN, "Email is already registered.");
                    }
                }
            }
        }

        public int CreateAccountAndUser(string email, string passwordHash, string displayName, string profileImageUrl)
        {
            int newAccountId;

            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var insertAcc = new SqlCommand(AuthSql.Text.INSERT_ACCOUNT, connection, transaction))
                    {
                        insertAcc.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                        insertAcc.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;
                        insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = AuthServiceConstants.ACCOUNT_STATUS_ACTIVE;
                        newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                    }

                    using (var insertUser = new SqlCommand(AuthSql.Text.INSERT_USER, connection, transaction))
                    {
                        insertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;
                        insertUser.Parameters.Add("@DisplayName", SqlDbType.NVarChar, AuthServiceConstants.DISPLAY_NAME_MAX_LENGTH).Value = displayName;
                        insertUser.Parameters.Add("@ProfileImageUrl", SqlDbType.NVarChar, AuthServiceConstants.PROFILE_URL_MAX_LENGTH).Value =
                            string.IsNullOrWhiteSpace(profileImageUrl) ? (object)DBNull.Value : profileImageUrl;
                        insertUser.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            return newAccountId;
        }

        public void GetAccountForLogin(string email, out int userId, out string storedHash, out byte status)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var get = new SqlCommand(AuthSql.Text.GET_ACCOUNT_BY_EMAIL, connection))
            {
                get.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                using (var reader = get.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw AuthServiceContext.ThrowFault(AuthServiceConstants.ERROR_INVALID_CREDENTIALS, "Email or password is incorrect.");
                    }

                    userId = reader.GetInt32(0);
                    storedHash = reader.GetString(1);
                    status = reader.GetByte(2);
                }
            }
        }

        public int UpdateAccountPassword(string email, string passwordHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                using (var updatePwd = new SqlCommand(AuthSql.Text.UPDATE_ACCOUNT_PASSWORD, connection, transaction))
                {
                    updatePwd.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                    updatePwd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 128).Value = passwordHash;

                    int rows = updatePwd.ExecuteNonQuery();
                    transaction.Commit();
                    return rows;
                }
            }
        }

        public void MarkVerificationUsed(int verificationId)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var mark = new SqlCommand(AuthSql.Text.MARK_VERIFICATION_USED, connection))
                {
                    mark.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    mark.ExecuteNonQuery();
                }
            }
        }

        public void MarkResetUsed(int resetId)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var mark = new SqlCommand(AuthSql.Text.MARK_RESET_USED, connection))
                {
                    mark.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                    mark.ExecuteNonQuery();
                }
            }
        }

        public void LeaveAllLobbiesForUser(int userId)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var cmd = new SqlCommand("dbo.usp_Lobby_LeaveAllByUser", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
