using ServicesTheWeakestRival.Server.Infrastructure;
using System;

namespace ServicesTheWeakestRival.Server.Services.AuthRefactor.Email
{
    public static class EmailCodeFactory
    {
        public static EmailCodeInfo Create()
        {
            string code = SecurityUtil.CreateNumericCode(AuthServiceConstants.EMAIL_CODE_LENGTH);
            byte[] codeHash = SecurityUtil.Sha256(code);
            DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(AuthServiceContext.CodeTtlMinutes);

            return new EmailCodeInfo(code, codeHash, expiresAtUtc);
        }
    }
}
