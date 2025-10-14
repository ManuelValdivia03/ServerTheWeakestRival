using System;
using System.Security.Cryptography;
using System.Text;

namespace ServicesTheWeakestRival.Server.Infrastructure
{
    internal static class SecurityUtil
    {
        public static string CreateNumericCode(int digits = 6)
        {
            if (digits <= 0) digits = 6;

            var chars = new char[digits];
            var oneByte = new byte[1];

            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < digits; i++)
                {
                    byte val;
                    do
                    {
                        rng.GetBytes(oneByte);
                        val = oneByte[0];
                    } while (val >= 250); 
                    chars[i] = (char)('0' + (val % 10));
                }
            }

            return new string(chars);
        }

        public static byte[] Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            }
        }
    }
}
