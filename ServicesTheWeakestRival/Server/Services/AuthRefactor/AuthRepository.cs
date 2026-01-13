using System;
using System.Data;
using System.Data.SqlClient;
using ServicesTheWeakestRival.Server.Services.Logic;
using ServicesTheWeakestRival.Server.Services.AuthRefactor.RepositoryModels;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor
{
    public sealed class AuthRepository
    {
        private const string PARAM_CODE_HASH = "@CodeHash";
        private const string PARAM_EXPIRES_AT_UTC = "@ExpiresAtUtc";

        private const string PARAM_ID = "@Id";
        private const string PARAM_HASH = "@Hash";

        private const string PARAM_PASSWORD_HASH = "@PasswordHash";
        private const string PARAM_STATUS = "@Status";
        private const string PARAM_USER_ID = "@UserId";
        private const string PARAM_DISPLAY_NAME = "@DisplayName";

        private const string PARAM_PROFILE_IMAGE = "@ProfileImage";
        private const string PARAM_PROFILE_IMAGE_CONTENT_TYPE = "@ProfileImageContentType";
        private const string PARAM_PROFILE_IMAGE_UPDATED_AT_UTC = "@ProfileImageUpdatedAtUtc";

        private const int VERIFICATION_COL_ID = 0;
        private const int VERIFICATION_COL_EXPIRES_AT_UTC = 1;
        private const int VERIFICATION_COL_USED = 2;

        private const int RESET_COL_ID = 0;
        private const int RESET_COL_EXPIRES_AT_UTC = 1;
        private const int RESET_COL_USED = 2;

        private const int LOGIN_COL_USER_ID = 0;
        private const int LOGIN_COL_PASSWORD_HASH = 1;
        private const int LOGIN_COL_STATUS = 2;

        private const int PROFILE_IMAGE_COL_BYTES = 0;
        private const int PROFILE_IMAGE_COL_CONTENT_TYPE = 1;
        private const int PROFILE_IMAGE_COL_UPDATED_AT_UTC = 2;

        private const int EMPTY_BINARY_LENGTH = 0;

        private readonly Func<string> connectionStringProvider;

        public AuthRepository(Func<string> connectionStringProvider)
        {
            this.connectionStringProvider = connectionStringProvider
                ?? throw new ArgumentNullException(nameof(connectionStringProvider));
        }

        public void CreateRegisterVerification(string email, byte[] codeHash, DateTime expiresAtUtc)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var invalidateCommand = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_VERIFICATIONS, connection, transaction))
                    {
                        invalidateCommand.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        invalidateCommand.ExecuteNonQuery();
                    }

                    using (var insertCommand = new SqlCommand(AuthSql.Text.INSERT_VERIFICATION, connection, transaction))
                    {
                        insertCommand.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        insertCommand.Parameters.Add(
                            PARAM_CODE_HASH,
                            SqlDbType.VarBinary,
                            AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                        insertCommand.Parameters.Add(PARAM_EXPIRES_AT_UTC, SqlDbType.DateTime2).Value = expiresAtUtc;

                        insertCommand.ExecuteNonQuery();
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
                    using (var invalidateCommand = new SqlCommand(AuthSql.Text.INVALIDATE_PENDING_RESETS, connection, transaction))
                    {
                        invalidateCommand.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        invalidateCommand.ExecuteNonQuery();
                    }

                    using (var insertCommand = new SqlCommand(AuthSql.Text.INSERT_RESET_REQUEST, connection, transaction))
                    {
                        insertCommand.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                        insertCommand.Parameters.Add(
                            PARAM_CODE_HASH,
                            SqlDbType.VarBinary,
                            AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                        insertCommand.Parameters.Add(PARAM_EXPIRES_AT_UTC, SqlDbType.DateTime2).Value = expiresAtUtc;

                        insertCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public VerificationLookupResult ReadLatestVerification(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pickCommand = new SqlCommand(AuthSql.Text.PICK_LATEST_VERIFICATION, connection))
            {
                pickCommand.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = pickCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return VerificationLookupResult.NotFound();
                    }

                    int verificationId = reader.GetInt32(VERIFICATION_COL_ID);
                    DateTime expiresAtUtc = reader.GetDateTime(VERIFICATION_COL_EXPIRES_AT_UTC);
                    bool used = reader.GetBoolean(VERIFICATION_COL_USED);

                    return VerificationLookupResult.FoundVerification(
                        new VerificationRow(verificationId, expiresAtUtc, used));
                }
            }
        }

        public ResetLookupResult ReadLatestReset(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var pickCommand = new SqlCommand(AuthSql.Text.PICK_LATEST_RESET, connection))
            {
                pickCommand.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = pickCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return ResetLookupResult.NotFound();
                    }

                    int resetId = reader.GetInt32(RESET_COL_ID);
                    DateTime expiresAtUtc = reader.GetDateTime(RESET_COL_EXPIRES_AT_UTC);
                    bool used = reader.GetBoolean(RESET_COL_USED);

                    return ResetLookupResult.FoundReset(new ResetRow(resetId, expiresAtUtc, used));
                }
            }
        }

        public CodeValidationResult ValidateVerificationCodeOrThrow(int verificationId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var validateCommand = new SqlCommand(AuthSql.Text.VALIDATE_VERIFICATION, connection))
            {
                validateCommand.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = verificationId;
                validateCommand.Parameters.Add(PARAM_HASH, SqlDbType.VarBinary, AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                connection.Open();

                bool isValid = Convert.ToInt32(validateCommand.ExecuteScalar()) == AuthServiceConstants.SQL_TRUE;

                if (isValid)
                {
                    return CodeValidationResult.Valid();
                }

                using (var incrementAttemptsCommand = new SqlCommand(AuthSql.Text.INCREMENT_ATTEMPTS, connection))
                {
                    incrementAttemptsCommand.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = verificationId;
                    incrementAttemptsCommand.ExecuteNonQuery();
                }

                return CodeValidationResult.Invalid();
            }
        }

        public CodeValidationResult ValidateResetCodeOrThrow(int resetId, byte[] codeHash)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var validateCommand = new SqlCommand(AuthSql.Text.VALIDATE_RESET, connection))
            {
                validateCommand.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = resetId;
                validateCommand.Parameters.Add(PARAM_HASH, SqlDbType.VarBinary, AuthServiceConstants.SHA256_HASH_BYTES).Value = codeHash;

                connection.Open();

                bool isValid = Convert.ToInt32(validateCommand.ExecuteScalar()) == AuthServiceConstants.SQL_TRUE;

                if (isValid)
                {
                    return CodeValidationResult.Valid();
                }

                using (var incrementAttemptsCommand = new SqlCommand(AuthSql.Text.INCREMENT_RESET_ATTEMPTS, connection))
                {
                    incrementAttemptsCommand.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = resetId;
                    incrementAttemptsCommand.ExecuteNonQuery();
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
                    using (var insertAccountCommand = new SqlCommand(AuthSql.Text.INSERT_ACCOUNT, connection, transaction))
                    {
                        insertAccountCommand.Parameters.Add(
                            AuthServiceConstants.PARAMETER_EMAIL,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.EMAIL_MAX_LENGTH).Value = data.Email;

                        insertAccountCommand.Parameters.Add(
                            PARAM_PASSWORD_HASH,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.PASSWORD_HASH_MAX_LENGTH).Value = data.PasswordHash;

                        insertAccountCommand.Parameters.Add(PARAM_STATUS, SqlDbType.TinyInt).Value = AuthServiceConstants.ACCOUNT_STATUS_ACTIVE;

                        newAccountId = Convert.ToInt32(insertAccountCommand.ExecuteScalar());
                    }

                    using (var insertUserCommand = new SqlCommand(AuthSql.Text.INSERT_USER, connection, transaction))
                    {
                        insertUserCommand.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = newAccountId;

                        insertUserCommand.Parameters.Add(
                            PARAM_DISPLAY_NAME,
                            SqlDbType.NVarChar,
                            AuthServiceConstants.DISPLAY_NAME_MAX_LENGTH).Value = data.DisplayName;

                        if (!data.ProfileImage.HasImage)
                        {
                            insertUserCommand.Parameters.Add(
                                PARAM_PROFILE_IMAGE,
                                SqlDbType.VarBinary,
                                AuthServiceConstants.SQL_VARBINARY_MAX).Value = DBNull.Value;

                            insertUserCommand.Parameters.Add(
                                PARAM_PROFILE_IMAGE_CONTENT_TYPE,
                                SqlDbType.NVarChar,
                                ProfileImageConstants.CONTENT_TYPE_MAX_LENGTH).Value = DBNull.Value;

                            insertUserCommand.Parameters.Add(PARAM_PROFILE_IMAGE_UPDATED_AT_UTC, SqlDbType.DateTime2).Value = DBNull.Value;
                        }
                        else
                        {
                            insertUserCommand.Parameters.Add(
                                PARAM_PROFILE_IMAGE,
                                SqlDbType.VarBinary,
                                AuthServiceConstants.SQL_VARBINARY_MAX).Value = data.ProfileImage.Bytes;

                            insertUserCommand.Parameters.Add(
                                PARAM_PROFILE_IMAGE_CONTENT_TYPE,
                                SqlDbType.NVarChar,
                                ProfileImageConstants.CONTENT_TYPE_MAX_LENGTH).Value = data.ProfileImage.ContentType;

                            insertUserCommand.Parameters.Add(PARAM_PROFILE_IMAGE_UPDATED_AT_UTC, SqlDbType.DateTime2).Value = DateTime.UtcNow;
                        }

                        insertUserCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            return newAccountId;
        }

        public LoginLookupResult GetAccountForLogin(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var getCommand = new SqlCommand(AuthSql.Text.GET_ACCOUNT_BY_EMAIL, connection))
            {
                getCommand.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH)
                    .Value = email;

                connection.Open();

                using (var reader = getCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return LoginLookupResult.NotFound();
                    }

                    int userId = reader.GetInt32(LOGIN_COL_USER_ID);
                    string storedHash = reader.IsDBNull(LOGIN_COL_PASSWORD_HASH) ? string.Empty : reader.GetString(LOGIN_COL_PASSWORD_HASH);
                    byte status = reader.GetByte(LOGIN_COL_STATUS);

                    return LoginLookupResult.FoundAccount(new LoginAccountRow(userId, storedHash, status));
                }
            }
        }

        public ProfileImageRecord ReadUserProfileImage(int userId)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var cmd = new SqlCommand(AuthSql.Text.GET_PROFILE_IMAGE_BY_USER_ID, connection))
            {
                cmd.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;
                connection.Open();

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return ProfileImageRecord.Empty(userId);
                    }

                    byte[] imageBytes = reader.IsDBNull(PROFILE_IMAGE_COL_BYTES) ? null : (byte[])reader[PROFILE_IMAGE_COL_BYTES];
                    string contentType = reader.IsDBNull(PROFILE_IMAGE_COL_CONTENT_TYPE) ? null : reader.GetString(PROFILE_IMAGE_COL_CONTENT_TYPE);
                    DateTime? updatedAtUtc = reader.IsDBNull(PROFILE_IMAGE_COL_UPDATED_AT_UTC) ? (DateTime?)null : reader.GetDateTime(PROFILE_IMAGE_COL_UPDATED_AT_UTC);

                    if (imageBytes == null || imageBytes.Length == EMPTY_BINARY_LENGTH)
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
                    updatePwd.Parameters.Add(
                        AuthServiceConstants.PARAMETER_EMAIL,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                    updatePwd.Parameters.Add(
                        PARAM_PASSWORD_HASH,
                        SqlDbType.NVarChar,
                        AuthServiceConstants.PASSWORD_HASH_MAX_LENGTH).Value = passwordHash;

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
                    mark.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = verificationId;
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
                    mark.Parameters.Add(PARAM_ID, SqlDbType.Int).Value = resetId;
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
                cmd.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;
                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool ExistsAccountByEmail(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var exists = new SqlCommand(AuthSql.Text.EXISTS_ACCOUNT_BY_EMAIL, connection))
            {
                exists.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

                connection.Open();

                return exists.ExecuteScalar() != null;
            }
        }

        public LastRequestUtcResult ReadLastVerificationUtc(string email)
        {
            using (var connection = new SqlConnection(connectionStringProvider()))
            using (var last = new SqlCommand(AuthSql.Text.LAST_VERIFICATION, connection))
            {
                last.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

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
                last.Parameters.Add(
                    AuthServiceConstants.PARAMETER_EMAIL,
                    SqlDbType.NVarChar,
                    AuthServiceConstants.EMAIL_MAX_LENGTH).Value = email;

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
        public int UserId 
        { 
            get;
        }
        public byte[] ImageBytes 
        { 
            get; 
        }
        public string ContentType 
        { 
            get; 
        }
        public DateTime? UpdatedAtUtc 
        { 
            get; 
        }

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
