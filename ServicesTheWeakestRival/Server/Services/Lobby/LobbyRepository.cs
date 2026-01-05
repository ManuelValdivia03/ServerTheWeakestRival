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
        private readonly string connectionString;

        public LobbyRepository(string connectionString)
        {
            this.connectionString = connectionString ?? string.Empty;
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
                    string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                    byte maxPlayers = reader.GetByte(2);
                    string accessCode = reader.IsDBNull(3) ? null : reader.GetString(3);

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
                                lobby_id = reader.GetInt32(0),
                                user_id = reader.GetInt32(1),
                                role = reader.GetByte(2),
                                joined_at_utc = reader.GetDateTime(3),
                                left_at_utc = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                is_active = reader.GetBoolean(5),
                                Users = new Users
                                {
                                    user_id = reader.GetInt32(6),
                                    display_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    profile_image_url = reader.IsDBNull(8) ? null : reader.GetString(8)
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
                string name = obj == null || obj == DBNull.Value ? null : Convert.ToString(obj);

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
                        UserId = reader.GetInt32(0),
                        DisplayName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        ProfileImageUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                        CreatedAtUtc = reader.GetDateTime(3),
                        Email = reader.GetString(4)
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

        public void UpdateUserProfile(int userId, string displayName, string profileImageUrl, bool hasDisplayNameChange, bool hasProfileImageChange)
        {
            string sqlLobby = LobbySql.BuildUpdateUser(hasDisplayNameChange, hasProfileImageChange);
            if (string.IsNullOrWhiteSpace(sqlLobby))
            {
                return;
            }

            using (var sqlConnection = new SqlConnection(connectionString))
            using (var sqlCommand = new SqlCommand(sqlLobby, sqlConnection))
            {
                sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_ID, SqlDbType.Int).Value = userId;

                if (hasDisplayNameChange)
                {
                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_DISPLAY_NAME, SqlDbType.NVarChar, LobbyServiceConstants.MAX_DISPLAY_NAME_LENGTH)
                        .Value = displayName.Trim();
                }

                if (hasProfileImageChange)
                {
                    sqlCommand.Parameters.Add(LobbyServiceConstants.PARAM_IMAGE_URL, SqlDbType.NVarChar, LobbyServiceConstants.MAX_PROFILE_IMAGE_URL_LENGTH)
                        .Value = profileImageUrl.Trim();
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
