using System;

namespace ServicesTheWeakestRival.Contracts.Data
{
    public sealed class BeginPasswordResetRequest
    {
        public string Email { get; set; }
    }

    public sealed class BeginPasswordResetResponse
    {
        public DateTime ExpiresAtUtc { get; set; }
        public int ResendAfterSeconds { get; set; }
    }

    public sealed class CompletePasswordResetRequest
    {
        public string Email { get; set; }
        public string Code { get; set; }
        public string NewPassword { get; set; }
    }
}
