using System;
using System.Runtime.Serialization;

namespace ServicesTheWeakestRival.Contracts.Data
{
    [DataContract]
    public sealed class RegisterRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Email { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Password { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public string DisplayName { get; set; }
        [DataMember(Order = 4, IsRequired = false)] public string ProfileImageUrl { get; set; }
    }

    [DataContract]
    public sealed class RegisterResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public AuthToken Token { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public int UserId { get; set; }
    }

    [DataContract]
    public sealed class LoginRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Email { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Password { get; set; }
    }

    [DataContract]
    public sealed class LoginResponse
    {
        [DataMember(Order = 1, IsRequired = true)] public AuthToken Token { get; set; }
    }

    [DataContract]
    public sealed class LogoutRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Token { get; set; }
    }

    [DataContract]
    public sealed class AuthToken
    {
        [DataMember(Order = 1, IsRequired = true)] public int UserId { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Token { get; set; }
        [DataMember(Order = 3, IsRequired = true)] public DateTime ExpiresAtUtc { get; set; }
    }

}

