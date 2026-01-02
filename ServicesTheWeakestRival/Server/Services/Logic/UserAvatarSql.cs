using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using ServicesTheWeakestRival.Contracts.Data;

namespace ServicesTheWeakestRival.Server.Services.Logic
{
    public sealed class UserAvatarSql
    {
        private const string PARAM_USER_ID = "@UserId";
        private const string PARAM_BODY_COLOR = "@BodyColor";
        private const string PARAM_PANTS_COLOR = "@PantsColor";
        private const string PARAM_HAT_TYPE = "@HatType";
        private const string PARAM_HAT_COLOR = "@HatColor";
        private const string PARAM_FACE_TYPE = "@FaceType";
        private const string PARAM_USE_PROFILE_PHOTO = "@UseProfilePhoto";

        private const string PARAM_IN_PREFIX = "@p";

        private const string SQL_SELECT_BY_USER_ID = @"
SELECT user_id,
       body_color,
       pants_color,
       hat_type,
       hat_color,
       face_type,
       use_profile_photo
FROM dbo.UserAvatar
WHERE user_id = @UserId;";

        private const string SQL_SELECT_BY_USER_IDS_PREFIX = @"
SELECT user_id,
       body_color,
       pants_color,
       hat_type,
       hat_color,
       face_type,
       use_profile_photo
FROM dbo.UserAvatar
WHERE user_id IN (";

        private const string SQL_SELECT_BY_USER_IDS_SUFFIX = ");";

        private readonly string _connectionString;

        public UserAvatarSql(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public UserAvatarEntity GetByUserId(int userId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand(SQL_SELECT_BY_USER_ID, connection))
            {
                command.CommandType = CommandType.Text;
                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = userId;

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return MapEntity(reader);
                }
            }
        }

        public Dictionary<int, UserAvatarEntity> GetByUserIds(int[] userIds)
        {
            int[] ids = userIds ?? Array.Empty<int>();

            var result = new Dictionary<int, UserAvatarEntity>();
            if (ids.Length == 0)
            {
                return result;
            }

            string sql = BuildGetByUserIdsQuery(ids.Length);

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;

                for (int i = 0; i < ids.Length; i++)
                {
                    command.Parameters.Add(PARAM_IN_PREFIX + i, SqlDbType.Int).Value = ids[i];
                }

                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        UserAvatarEntity entity = MapEntity(reader);
                        result[entity.UserId] = entity;
                    }
                }
            }

            return result;
        }

        public void Save(UserAvatarEntity avatar)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            const string sql = @"
MERGE dbo.UserAvatar AS target
USING (SELECT @UserId AS user_id) AS source
ON (target.user_id = source.user_id)
WHEN MATCHED THEN
    UPDATE SET
        body_color = @BodyColor,
        pants_color = @PantsColor,
        hat_type = @HatType,
        hat_color = @HatColor,
        face_type = @FaceType,
        use_profile_photo = @UseProfilePhoto
WHEN NOT MATCHED THEN
    INSERT (user_id, body_color, pants_color, hat_type, hat_color, face_type, use_profile_photo)
    VALUES (@UserId, @BodyColor, @PantsColor, @HatType, @HatColor, @FaceType, @UseProfilePhoto);";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;

                command.Parameters.Add(PARAM_USER_ID, SqlDbType.Int).Value = avatar.UserId;
                command.Parameters.Add(PARAM_BODY_COLOR, SqlDbType.TinyInt).Value = avatar.BodyColor;
                command.Parameters.Add(PARAM_PANTS_COLOR, SqlDbType.TinyInt).Value = avatar.PantsColor;
                command.Parameters.Add(PARAM_HAT_TYPE, SqlDbType.TinyInt).Value = avatar.HatType;
                command.Parameters.Add(PARAM_HAT_COLOR, SqlDbType.TinyInt).Value = avatar.HatColor;
                command.Parameters.Add(PARAM_FACE_TYPE, SqlDbType.TinyInt).Value = avatar.FaceType;
                command.Parameters.Add(PARAM_USE_PROFILE_PHOTO, SqlDbType.Bit).Value = avatar.UseProfilePhoto;

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static string BuildGetByUserIdsQuery(int count)
        {
            var builder = new StringBuilder();
            builder.Append(SQL_SELECT_BY_USER_IDS_PREFIX);

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(PARAM_IN_PREFIX);
                builder.Append(i);
            }

            builder.Append(SQL_SELECT_BY_USER_IDS_SUFFIX);

            return builder.ToString();
        }

        private static UserAvatarEntity MapEntity(SqlDataReader reader)
        {
            return new UserAvatarEntity
            {
                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                BodyColor = reader.GetByte(reader.GetOrdinal("body_color")),
                PantsColor = reader.GetByte(reader.GetOrdinal("pants_color")),
                HatType = reader.GetByte(reader.GetOrdinal("hat_type")),
                HatColor = reader.GetByte(reader.GetOrdinal("hat_color")),
                FaceType = reader.GetByte(reader.GetOrdinal("face_type")),
                UseProfilePhoto = reader.GetBoolean(reader.GetOrdinal("use_profile_photo"))
            };
        }
    }
}
