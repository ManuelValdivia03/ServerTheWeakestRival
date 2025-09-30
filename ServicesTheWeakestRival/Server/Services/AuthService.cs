using System;
using ServicesTheWeakestRival.Contracts.Data;
using ServicesTheWeakestRival.Contracts.Services;

namespace ServicesTheWeakestRival.Server.Services
{
    public class AuthService : IAuthService
    {
        public PingResponse Ping(PingRequest request) =>
            new PingResponse { Echo = request.Message, Utc = DateTime.UtcNow };

        public RegisterResponse Register(RegisterRequest request) =>
            new RegisterResponse { Token = new AuthToken { PlayerId = Guid.NewGuid(), Token = Guid.NewGuid().ToString(), ExpiresAtUtc = DateTime.UtcNow.AddHours(1) } };

        public LoginResponse Login(LoginRequest request) =>
            new LoginResponse { Token = new AuthToken { PlayerId = Guid.NewGuid(), Token = Guid.NewGuid().ToString(), ExpiresAtUtc = DateTime.UtcNow.AddHours(1) } };

        public void Logout(LogoutRequest request) { /* TODO: revoke */ }
    }
}
