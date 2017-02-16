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
	}
}