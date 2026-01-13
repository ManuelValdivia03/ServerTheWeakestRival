using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Email
{
    public sealed class EmailCodeInfo
    {
        public string Code 
        {
            get; 
        }
        public byte[] Hash 
        {
            get; 
        }
        public DateTime ExpiresAtUtc 
        { 
            get; 
        }

        public EmailCodeInfo(string code, byte[] hash, DateTime expiresAtUtc)
        {
            Code = code ?? string.Empty;
            Hash = hash ?? Array.Empty<byte>();
            ExpiresAtUtc = expiresAtUtc;
        }
    }
}
