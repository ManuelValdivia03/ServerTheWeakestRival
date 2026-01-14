using ServicesTheWeakestRival.Contracts.Data;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace ServicesTheWeakestRival.Server.Services.Friends.Infrastructure
{
    internal interface IFriendAccountRepository
    {
        SearchAccountItem[] SearchAccounts(SqlConnection connection, int myAccountId, string likeQuery, int maxResults);

        List<AccountMini> GetAccountsByIds(
            SqlConnection connection,
            int myAccountId,
            int[] ids,
            IDictionary<int, UserAvatarEntity> avatarsByUserId);

        UserProfileImageEntity GetProfileImage(SqlConnection connection, int accountId);
    }

    internal sealed class UserProfileImageEntity
    {
        public UserProfileImageEntity(byte[] bytes, string contentType, System.DateTime? updatedAtUtc, string profileImageCode)
        {
            Bytes = bytes ?? System.Array.Empty<byte>();
            ContentType = contentType ?? string.Empty;
            UpdatedAtUtc = updatedAtUtc;
            ProfileImageCode = profileImageCode ?? string.Empty;
        }

        public byte[] Bytes 
        { 
            get; 
        }
        public string ContentType 
        { 
            get; 
        }
        public System.DateTime? UpdatedAtUtc 
        { 
            get; 
        }
        public string ProfileImageCode 
        { 
            get; 
        }
    }
}
