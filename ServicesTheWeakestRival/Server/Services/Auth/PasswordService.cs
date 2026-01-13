using System;
using BCrypt.Net;

namespace ServicesTheWeakestRival.Server.Services.Auth
{
    public sealed class PasswordService
    {
        private const int MIN_ALLOWED_MIN_LENGTH = 0;

        private const string EMPTY_STRING = "";

        private readonly int minLength;

        public PasswordService(int minLength)
        {
            if (minLength <= MIN_ALLOWED_MIN_LENGTH)
            {
                throw new ArgumentOutOfRangeException(nameof(minLength));
            }

            this.minLength = minLength;
        }

        public bool IsValid(string password)
        {
            return !string.IsNullOrWhiteSpace(password)
                && password.Trim().Length >= minLength;
        }

        public static string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? EMPTY_STRING);
        }

        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(password ?? EMPTY_STRING, storedHash);
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
