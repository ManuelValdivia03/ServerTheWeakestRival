using System;
using BCrypt.Net;

namespace ServicesTheWeakestRival.Server.Services.Auth
{
    public sealed class PasswordService
    {
        private readonly int minLength;

        public PasswordService(int minLength)
        {
            if (minLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minLength));
            }

            this.minLength = minLength;
        }

        public bool IsValid(string password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Trim().Length >= minLength;
        }

        public string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? string.Empty);
        }

        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);
            }
            catch (SaltParseException ex)
            {
                GC.KeepAlive(ex);
                return false;
            }
            catch (ArgumentException ex)
            {
                GC.KeepAlive(ex);
                return false;
            }
        }
    }
}
