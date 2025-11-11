using System;
using System.Data;
using System.Data.SqlClient;
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

        private readonly string connectionString;

        public UserAvatarSql(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            this.connectionString = connectionString;
        }

        public UserAvatarEntity GetByUserId(int userId)
        {
            const string sql = @"
SELECT user_id,
       body_color,
       pants_color,
       hat_type,
       hat_color,
       face_type,
       use_profile_photo
FROM dbo.UserAvatar
WHERE user_id = @UserId;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
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

            using (SqlConnection connection = new SqlConnection(connectionString))
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
    }
}
