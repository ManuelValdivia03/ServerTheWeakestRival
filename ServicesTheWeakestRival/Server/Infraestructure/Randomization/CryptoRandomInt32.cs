using System;
using System.Security.Cryptography;

namespace ServicesTheWeakestRival.Server.Infrastructure.Randomization
{
    internal static class CryptoRandomInt32
    {
        private const int BYTES_PER_UINT32 = 4;

        private const long UINT32_RANGE = 4294967296L;

        private static readonly RandomNumberGenerator Generator = RandomNumberGenerator.Create();

        public static int GetInt32(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive));
            }

            long range = (long)maxExclusive - minInclusive;

            long limit = UINT32_RANGE - (UINT32_RANGE % range);

            uint value;

            do
            {
                value = NextUInt32();
            }
            while (value >= limit);

            int result = (int)(minInclusive + (long)(value % range));
            return result;
        }

        private static uint NextUInt32()
        {
            byte[] buffer = new byte[BYTES_PER_UINT32];
            Generator.GetBytes(buffer);
            return BitConverter.ToUInt32(buffer, 0);
        }
    }
}
