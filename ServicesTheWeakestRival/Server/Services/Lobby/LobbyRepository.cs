using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Server.Services.Logic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using TheWeakestRival.Data;

namespace ServicesTheWeakestRival.Server.Services.Lobby
{
    public sealed class LobbyRepository
    {
        private const string TABLE_USERS = "dbo.Users";
        private const string COLUMN_USER_ID = "user_id";
        private const string COLUMN_DISPLAY_NAME = "display_name";
        private const string COLUMN_PROFILE_IMAGE = "profile_image";
        private const string COLUMN_PROFILE_IMAGE_CONTENT_TYPE = "profile_image_content_type";

        private const string PARAM_PROFILE_IMAGE = "@ProfileImage";
        private const string PARAM_PROFILE_IMAGE_CONTENT_TYPE = "@ProfileImageContentType";

        private const int MAX_PROFILE_IMAGE_CONTENT_TYPE_LENGTH = 64;

        private const int ORD_MEMBER_LOBBY_ID = 0;
        private const int ORD_MEMBER_USER_ID = 1;
        private const int ORD_MEMBER_ROLE = 2;
        private const int ORD_MEMBER_JOINED_AT_UTC = 3;
        private const int ORD_MEMBER_LEFT_AT_UTC = 4;
        private const int ORD_MEMBER_IS_ACTIVE = 5;
        private const int ORD_USER_ID = 6;
        private const int ORD_USER_DISPLAY_NAME = 7;
        private const int ORD_USER_PROFILE_IMAGE = 8;
        private const int ORD_USER_PROFILE_IMAGE_CONTENT_TYPE = 9;

        private const int ORD_PROFILE_USER_ID = 0;
        private const int ORD_PROFILE_DISPLAY_NAME = 1;
        private const int ORD_PROFILE_IMAGE = 2;
        private const int ORD_PROFILE_IMAGE_CONTENT_TYPE = 3;
        private const int ORD_PROFILE_CREATED_AT = 4;
        private const int ORD_PROFILE_EMAIL = 5;

        private readonly string connectionString;

        public LobbyRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            this.connectionString = connectionString;
        }

        public void LeaveLobby(int userId, int lobbyId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE, sqlConnection))
            {
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_USER_ID, SqlDbType.Int).Value = userId;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_LOBBY_ID, SqlDbType.Int).Value = lobbyId;

                sqlConnection.Open();
                sqlCommand.ExecuteNonQuery();
            }
        }

        public void LeaveAllLobbiesByUser(int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_LEAVE_ALL_BY_USER, sqlConnection))
            {
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_USER_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();
                sqlCommand.ExecuteNonQuery();
            }
        }

        public int GetLobbyIdFromUid(Guid lobbyUid)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_ID_FROM_UID, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_LOBBY_UID, SqlDbType.UniqueIdentifier).Value = lobbyUid;

                sqlConnection.Open();

                object obj = sqlCommand.ExecuteScalar();
                if (obj == null)
                {
                    throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_NOT_FOUND, "Lobby no encontrado.");
                }

                return Convert.ToInt32(obj);
            }
        }

        public LobbyInfo LoadLobbyInfoByIntId(int lobbyId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_BY_ID, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_LOBBY_ID_BY_ID, SqlDbType.Int).Value = lobbyId;

                sqlConnection.Open();

                using (SqlDataReader reader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_NOT_FOUND, "Lobby no encontrado.");
                    }

                    Guid uid = reader.GetGuid(0);
                    string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    byte maxPlayers = reader.GetByte(2);
                    string accessCode = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                    return new LobbyInfo
                    {
                        LobbyId = uid,
                        LobbyName = name,
                        MaxPlayers = maxPlayers,
                        Players = new List<AccountMini>(),
                        AccessCode = accessCode
                    };
                }
            }
        }

        public List<LobbyMembers> GetLobbyMembers(int lobbyId)
        {
            var members = new List<LobbyMembers>();

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_LOBBY_MEMBERS_WITH_USERS, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_LOBBY_ID, SqlDbType.Int).Value = lobbyId;

                sqlConnection.Open();

                using (SqlDataReader reader = sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        members.Add(
                            new LobbyMembers
                            {
                                lobby_id = reader.GetInt32(ORD_MEMBER_LOBBY_ID),
                                user_id = reader.GetInt32(ORD_MEMBER_USER_ID),
                                role = reader.GetByte(ORD_MEMBER_ROLE),
                                joined_at_utc = reader.GetDateTime(ORD_MEMBER_JOINED_AT_UTC),
                                left_at_utc = reader.IsDBNull(ORD_MEMBER_LEFT_AT_UTC) ? (DateTime?)null : reader.GetDateTime(ORD_MEMBER_LEFT_AT_UTC),
                                is_active = reader.GetBoolean(ORD_MEMBER_IS_ACTIVE),
                                Users = new Users
                                {
                                    user_id = reader.GetInt32(ORD_USER_ID),
                                    display_name = reader.IsDBNull(ORD_USER_DISPLAY_NAME) ? string.Empty : reader.GetString(ORD_USER_DISPLAY_NAME),
                                    profile_image = reader.IsDBNull(ORD_USER_PROFILE_IMAGE) ? Array.Empty<byte>() : (byte[])reader.GetValue(ORD_USER_PROFILE_IMAGE),
                                    profile_image_content_type = reader.IsDBNull(ORD_USER_PROFILE_IMAGE_CONTENT_TYPE) ? string.Empty : reader.GetString(ORD_USER_PROFILE_IMAGE_CONTENT_TYPE)
                                }
                            });
                    }
                }
            }

            return members;
        }

        public string GetUserDisplayName(int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_USER_DISPLAY_NAME, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();

                object obj = sqlCommand.ExecuteScalar();
                string name = obj == null || obj == DBNull.Value ? string.Empty : Convert.ToString(obj);

                return string.IsNullOrWhiteSpace(name)
                    ? string.Concat(LobbyServiceConstants.DEFAULT_PLAYER_NAME_PREFIX, userId)
                    : name.Trim();
            }
        }

        public UpdateAccountResponse GetMyProfile(int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.GET_MY_PROFILE, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();

                using (SqlDataReader reader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_NOT_FOUND, "Usuario no encontrado.");
                    }

                    return new UpdateAccountResponse
                    {
                        UserId = reader.GetInt32(ORD_PROFILE_USER_ID),
                        DisplayName = reader.IsDBNull(ORD_PROFILE_DISPLAY_NAME) ? string.Empty : reader.GetString(ORD_PROFILE_DISPLAY_NAME),
                        ProfileImageBytes = reader.IsDBNull(ORD_PROFILE_IMAGE) ? Array.Empty<byte>() : (byte[])reader.GetValue(ORD_PROFILE_IMAGE),
                        ProfileImageContentType = reader.IsDBNull(ORD_PROFILE_IMAGE_CONTENT_TYPE) ? string.Empty : reader.GetString(ORD_PROFILE_IMAGE_CONTENT_TYPE),
                        CreatedAtUtc = reader.GetDateTime(ORD_PROFILE_CREATED_AT),
                        Email = reader.IsDBNull(ORD_PROFILE_EMAIL) ? string.Empty : reader.GetString(ORD_PROFILE_EMAIL)
                    };
                }
            }
        }

        public bool EmailExistsExceptUserId(string email, int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.EMAIL_EXISTS_EXCEPT_ID, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_EMAIL, SqlDbType.NVarChar, LobbyServiceConstants.MAX_EMAIL_LENGTH).Value = email;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();

                object exists = sqlCommand.ExecuteScalar();
                return exists != null;
            }
        }

        public void UpdateUserEmail(string email, int userId)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.UPDATE_ACCOUNT_EMAIL, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_EMAIL, SqlDbType.NVarChar, LobbyServiceConstants.MAX_EMAIL_LENGTH).Value = email;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                sqlConnection.Open();

                int rows = sqlCommand.ExecuteNonQuery();
                if (rows == 0)
                {
                    throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_NOT_FOUND, "Cuenta no encontrada.");
                }
            }
        }

        public void UpdateUserProfile(
            int userId,
            string displayName,
            byte[] profileImageBytes,
            string profileImageContentType,
            bool hasDisplayNameChange,
            bool hasProfileImageChange)
        {
            string sql = BuildUpdateUserSql(hasDisplayNameChange, hasProfileImageChange);
            if (string.IsNullOrWhiteSpace(sql))
            {
                return;
            }

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(sql, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                if (hasDisplayNameChange)
                {
                    string trimmedDisplayName = (displayName ?? string.Empty).Trim();
                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_DISPLAY_NAME, SqlDbType.NVarChar, LobbyServiceConstants.MAX_DISPLAY_NAME_LENGTH)
                        .Value = trimmedDisplayName;
                }

                if (hasProfileImageChange)
                {
                    object bytesValue = profileImageBytes == null ? (object)DBNull.Value : profileImageBytes;
                    object contentTypeValue = string.IsNullOrWhiteSpace(profileImageContentType) ? (object)DBNull.Value : profileImageContentType.Trim();

                    sqlCommand.Parameters.Add(PARAM_PROFILE_IMAGE, SqlDbType.VarBinary, -1).Value = bytesValue;
                    sqlCommand.Parameters.Add(PARAM_PROFILE_IMAGE_CONTENT_TYPE, SqlDbType.NVarChar, MAX_PROFILE_IMAGE_CONTENT_TYPE_LENGTH).Value = contentTypeValue;
                }

                sqlConnection.Open();

                int rows = sqlCommand.ExecuteNonQuery();
                if (rows == 0)
                {
                    throw LobbyServiceContext.ThrowFault(LobbyServiceConstants.ERROR_NOT_FOUND, "Usuario no encontrado.");
                }
            }
        }

        public CreateLobbyDbResult CreateLobby(int ownerId, string lobbyName, int maxPlayers)
        {
            int lobbyId;
            Guid lobbyUid;
            string accessCode;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                sqlConnection.Open();

                LeaveAllLobbiesByUser(ownerId);

                using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_CREATE, sqlConnection))
                {
                    sqlCommand.CommandType = CommandType.StoredProcedure;

                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OWNER_USER_ID, SqlDbType.Int).Value = ownerId;

                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_LOBBY_NAME, SqlDbType.NVarChar, LobbyServiceConstants.MAX_DISPLAY_NAME_LENGTH).Value =
                        string.IsNullOrWhiteSpace(lobbyName) ? (object)DBNull.Value : lobbyName.Trim();

                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_MAX_PLAYERS, SqlDbType.TinyInt).Value =
                        maxPlayers > 0 ? (byte)maxPlayers : (byte)LobbyServiceConstants.DEFAULT_MAX_PLAYERS;

                    SqlParameter pId = sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OUT_LOBBY_ID, SqlDbType.Int);
                    pId.Direction = ParameterDirection.Output;

                    SqlParameter pUid = sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OUT_LOBBY_UID, SqlDbType.UniqueIdentifier);
                    pUid.Direction = ParameterDirection.Output;

                    SqlParameter pCode = sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OUT_ACCESS_CODE, SqlDbType.NVarChar, LobbyServiceConstants.ACCESS_CODE_MAX_LENGTH);
                    pCode.Direction = ParameterDirection.Output;

                    sqlCommand.ExecuteNonQuery();

                    lobbyId = (int)pId.Value;
                    lobbyUid = (Guid)pUid.Value;
                    accessCode = (string)pCode.Value;
                }
            }

            return new CreateLobbyDbResult
            {
                LobbyId = lobbyId,
                LobbyUid = lobbyUid,
                AccessCode = accessCode
            };
        }

        public JoinLobbyDbResult JoinByCode(int userId, string accessCode)
        {
            int lobbyId;
            Guid lobbyUid;

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(LobbySql.Text.SP_LOBBY_JOIN_BY_CODE, sqlConnection))
            {
                sqlCommand.CommandType = CommandType.StoredProcedure;

                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_USER_ID, SqlDbType.Int).Value = userId;
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ACCESS_CODE, SqlDbType.NVarChar, LobbyServiceConstants.ACCESS_CODE_MAX_LENGTH).Value =
                    (accessCode ?? string.Empty).Trim().ToUpperInvariant();

                SqlParameter pId = sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OUT_LOBBY_ID, SqlDbType.Int);
                pId.Direction = ParameterDirection.Output;

                SqlParameter pUid = sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_OUT_LOBBY_UID, SqlDbType.UniqueIdentifier);
                pUid.Direction = ParameterDirection.Output;

                sqlConnection.Open();
                sqlCommand.ExecuteNonQuery();

                lobbyId = (int)pId.Value;
                lobbyUid = (Guid)pUid.Value;
            }

            return new JoinLobbyDbResult
            {
                LobbyId = lobbyId,
                LobbyUid = lobbyUid
            };
        }

        private static string BuildUpdateUserSql(bool hasDisplayNameChange, bool hasProfileImageChange)
        {
            if (!hasDisplayNameChange && !hasProfileImageChange)
            {
                return string.Empty;
            }

            var sets = new List<string>();

            if (hasDisplayNameChange)
            {
                sets.Add(string.Format("{0} = {1}", COLUMN_DISPLAY_NAME, LobbyServiceConstants.PARAM_DISPLAY_NAME));
            }

            if (hasProfileImageChange)
            {
                sets.Add(string.Format("{0} = {1}", COLUMN_PROFILE_IMAGE, PARAM_PROFILE_IMAGE));
                sets.Add(string.Format("{0} = {1}", COLUMN_PROFILE_IMAGE_CONTENT_TYPE, PARAM_PROFILE_IMAGE_CONTENT_TYPE));
            }

            return string.Format(
                "UPDATE {0} SET {1} WHERE {2} = {3};",
                TABLE_USERS,
                string.Join(", ", sets),
                COLUMN_USER_ID,
                LobbyServiceConstants.PARAM_ID);
        }
    }

    public sealed class CreateLobbyDbResult
    {
        public int LobbyId { get; set; }
        public Guid LobbyUid { get; set; }
        public string AccessCode { get; set; }
    }

    public sealed class JoinLobbyDbResult
    {
        public int LobbyId { get; set; }
        public Guid LobbyUid { get; set; }
    }
}
