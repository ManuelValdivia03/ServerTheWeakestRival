using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Server.Services.Auth
{
    public sealed class PasswordService
    {
        private const int BCRYPT_WORK_FACTOR = 10;

        private readonly int passwordMinLength;

        public PasswordService(int passwordMinLength)
        {
            if (passwordMinLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(passwordMinLength));
            }

            this.passwordMinLength = passwordMinLength;
        }

        public bool IsValid(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return password.Length >= this.passwordMinLength;
        }

        public string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(
                password ?? string.Empty,
                workFactor: BCRYPT_WORK_FACTOR);
        }

        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);
        }
    }
}
