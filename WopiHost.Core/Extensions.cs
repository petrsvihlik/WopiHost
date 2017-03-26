using System;
using System.IO;
using System.Threading.Tasks;

namespace WopiHost.Core
{
    internal static class Extensions
    {
        /// <summary>
        /// Copies the stream to a byte array.
        /// </summary>
        /// <param name="input">Stream to read from</param>
        /// <returns>Byte array copy of a stream</returns>
        public static async Task<byte[]> ReadBytesAsync(this Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await input.CopyToAsync(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Tries to parse integer from string. Returns null if parsing fails.
        /// </summary>
        /// <param name="s">String to parse</param>
        /// <returns>Integer parsed from <see cref="s"/></returns>
        public static int? ToNullableInt(this string s)
        {
            int i;
            if (int.TryParse(s, out i)) return i;
            return null;
        }

        /// <summary>
        /// Converts <see cref="DateTime"/> to UNIX timestamp.
        /// </summary>
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            long unixTimeStampInTicks = (dateTime.ToUniversalTime() - unixStart).Ticks;
            return unixTimeStampInTicks / TimeSpan.TicksPerSecond;
        }
    }
}