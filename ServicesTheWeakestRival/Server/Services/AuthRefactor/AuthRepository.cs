using System;
using System.Data;
using System.Data.SqlClient;
using ServicesTheWeakestRival.Server.Services.Logic;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;

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
                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_VERIFICATIONS, connection, transaction))
                    {
                        invalidate.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_VERIFICATION, connection, transaction))
                    {
                        insert.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        insert.Parameters.Add(
                            "@CodeHash",
                            SqlDbType.VarBinary,
                            AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

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
                    using (var invalidate = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_RESETS, connection, transaction))
                    {
                        invalidate.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        invalidate.ExecuteNonQuery();
                    }

                    using (var insert = new SqlCommand(AuthSql.Text.INSERT_RESET_REQUEST, connection, transaction))
                    {
                        insert.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        insert.Parameters.Add(
                            "@CodeHash",
                            SqlDbType.VarBinary,
                            AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                        insert.Parameters.Add("@ExpiresAtUtc", SqlDbType.DateTime2).Value = expiresAtUtc;

                        insert.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }



        public VerificationLookupResult ReadLatestVerification(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_VERIFICATION, connection))
            {
                pick.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return VerificationLookupResult.NotFound();
                    }

                    int verificationId = reader.GetInt32(0);
                    DateTime expiresAtUtc = reader.GetDateTime(1);
                    bool used = reader.GetBoolean(2);

                    return VerificationLookupResult.FoundVerification(new VerificationRow(verificationId, expiresAtUtc, used));
                }
            }
        }


        public ResetLookupResult ReadLatestReset(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pick = new SqlCommand(AuthSql.Text.PICK_LATEST_RESET, connection))
            {
                pick.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = pick.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return ResetLookupResult.NotFound();
                    }

                    int resetId = reader.GetInt32(0);
                    DateTime expiresAtUtc = reader.GetDateTime(1);
                    bool used = reader.GetBoolean(2);

                    return ResetLookupResult.FoundReset(new ResetRow(resetId, expiresAtUtc, used));
                }
            }
        }


        public CodeValidationResult ValidateVerificationCodeOrThrow(int verificationId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_VERIFICATION, connection))
            {
                validate.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                validate.Parameters.Add("@Hash", SqlDbType.VarBinary, AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                connection.Open();

                bool isValid = Convert.ToInt32(validate.ExecuteScalar()) == AuthServiceConstants.SQL_TRUE;

                if (isValid)
                {
                    return CodeValidationResult.Valid();
                }

                using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_ATTEMPTS, connection))
                {
                    inc.Parameters.Add("@Id", SqlDbType.Int).Value = verificationId;
                    inc.ExecuteNonQuery();
                }

                return CodeValidationResult.Invalid();
            }
        }


        public CodeValidationResult ValidateResetCodeOrThrow(int resetId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var validate = new SqlCommand(AuthSql.Text.VALIDATE_RESET, connection))
            {
                validate.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                validate.Parameters.Add("@Hash", SqlDbType.VarBinary, AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                connection.Open();

                bool isValid = Convert.ToInt32(validate.ExecuteScalar()) == AuthServiceConstants.SQL_TRUE;

                if (isValid)
                {
                    return CodeValidationResult.Valid();
                }

                using (var inc = new SqlCommand(AuthSql.Text.INCREMENT_RESET_ATTEMPTS, connection))
                {
                    inc.Parameters.Add("@Id", SqlDbType.Int).Value = resetId;
                    inc.ExecuteNonQuery();
                }

                return CodeValidationResult.Invalid();
            }
        }

        public int CreateAccountAndUser(AccountRegistrationData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int newAccountId;

            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var insertAcc = new SqlCommand(AuthSql.Text.INSERT_ACCOUNT, connection, transaction))
                    {
                        insertAcc.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = data.Email;

                        insertAcc.Parameters.Add(
                            "@PasswordHash",
                            SqlDbType.NVarChar,
                            AuthServiceConstants.PASSWORD_HASH_MAX_LENGTH).Value = data.PasswordHash;

                        insertAcc.Parameters.Add("@Status", SqlDbType.TinyInt).Value = AuthServiceConstants.ACCOUNT_STATUS_ACTIVE;

                        newAccountId = Convert.ToInt32(insertAcc.ExecuteScalar());
                    }

                    using (var insertUser = new SqlCommand(AuthSql.Text.INSERT_USER, connection, transaction))
                    {
                        insertUser.Parameters.Add("@UserId", SqlDbType.Int).Value = newAccountId;

                        insertUser.Parameters.Add(
                            "@DisplayName",
                            SqlDbType.NVarChar,
                            AuthServiceConstants.DISPLAY_NAME_MAX_LENGTH).Value = data.DisplayName;

                        if (!data.ProfileImage.HasImage)
                        {
                            insertUser.Parameters.Add(
                                "@ProfileImage",
                                SqlDbType.VarBinary,
                                AuthServiceConstants.SQL_VARBINARY_MAX).Value = DBNull.Value;

                            insertUser.Parameters.Add(
                                "@ProfileImageContentType",
                                SqlDbType.NVarChar,
                                ProfileImageConstants.CONTENT_TYPE_MAX_LENGTH).Value = DBNull.Value;

                            insertUser.Parameters.Add("@ProfileImageUpdatedAtUtc", SqlDbType.DateTime2).Value = DBNull.Value;
                        }
                        else
                        {
                            insertUser.Parameters.Add(
                                "@ProfileImage",
                                SqlDbType.VarBinary,
                                AuthServiceConstants.SQL_VARBINARY_MAX).Value = data.ProfileImage.Bytes;

                            insertUser.Parameters.Add(
                                "@ProfileImageContentType",
                                SqlDbType.NVarChar,
                                ProfileImageConstants.CONTENT_TYPE_MAX_LENGTH).Value = data.ProfileImage.ContentType;

                            insertUser.Parameters.Add("@ProfileImageUpdatedAtUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                        }

                        insertUser.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
            return newAccountId;
        }

        public LoginLookupResult GetAccountForLogin(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var get = new SqlCommand(AuthSql.Text.GET_ACCOUNT_BY_EMAIL, connection))
            {
                get.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = get.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return LoginLookupResult.NotFound();
                    }

                    int userId = reader.GetInt32(0);
                    string storedHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    byte status = reader.GetByte(2);

                    return LoginLookupResult.FoundAccount(new LoginAccountRow(userId, storedHash, status));
                }
            }
        }

        public ProfileImageRecord ReadUserProfileImage(int userId)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var cmd = new SqlCommand(AuthSql.Text.GET_PROFILE_IMAGE_BY_USER_ID, connection))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return ProfileImageRecord.Empty(userId);
                    }

                    byte[] imageBytes = reader.IsDBNull(0) ? null : (byte[])reader[0];
                    string contentType = reader.IsDBNull(1) ? null : reader.GetString(1);
                    DateTime? updatedAtUtc = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);

                    if (imageBytes == null || imageBytes.Length == 0)
                    {
                        return ProfileImageRecord.Empty(userId);
                    }

                    return new ProfileImageRecord(userId, imageBytes, contentType, updatedAtUtc);
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
                    updatePwd.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value
                        = email;
                    updatePwd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, AuthServiceConstants.PASSWORD_HASH_MAX_LENGTH).Value
                        = passwordHash;

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
            using (var cmd = new SqlCommand(AuthSql.Text.SP_LOBBY_LEAVE_ALL_BY_USER, connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool ExistsAccountByEmail(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection))
            {
                exists.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                return exists.ExecuteScalar() != null;
            }
        }

        public LastRequestUtcResult ReadLastVerificationUtc(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var last = new SqlCommand(AuthSql.Text.LAST_VERIFICATION, connection))
            {
                last.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                object lastObj = last.ExecuteScalar();
                if (lastObj == null || lastObj == DBNull.Value)
                {
                    return LastRequestUtcResult.None();
                }

                return LastRequestUtcResult.From((DateTime)lastObj);
            }
        }

        public LastRequestUtcResult ReadLastResetUtc(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var last = new SqlCommand(AuthSql.Text.LAST_RESET_REQUEST, connection))
            {
                last.Parameters.Add(AuthServiceConstants.PARAMETER_EMAIL, SqlDbType.NVarChar, AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;
                connection.Open();

                object lastObj = last.ExecuteScalar();
                if (lastObj == null || lastObj == DBNull.Value)
                {
                    return LastRequestUtcResult.None();
                }

                return LastRequestUtcResult.From((DateTime)lastObj);
            }
        }


    }

    public sealed class ProfileImageRecord
    {
        public int UserId { get; }
        public byte[] ImageBytes { get; }
        public string ContentType { get; }
        public DateTime? UpdatedAtUtc { get; }

        public ProfileImageRecord(int userId, byte[] imageBytes, string contentType, DateTime? updatedAtUtc)
        {
            UserId = userId;
            ImageBytes = imageBytes ?? Array.Empty<byte>();
            ContentType = contentType ?? string.Empty;
            UpdatedAtUtc = updatedAtUtc;
        }

        public static ProfileImageRecord Empty(int userId)
        {
            return new ProfileImageRecord(userId, Array.Empty<byte>(), string.Empty, null);
        }
    }


}
