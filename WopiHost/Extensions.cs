using System.IO;
using System.Threading.Tasks;

namespace WopiHost
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
    }
}