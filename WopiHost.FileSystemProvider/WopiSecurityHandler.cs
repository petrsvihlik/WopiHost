using System;
using System.Security.Cryptography;
using System.Text;

using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiSecurityHandler : IWopiSecurityHandler
	{
		private const int _saltLength = 8;

		private string KeyString { get; }

		private byte[] Key => Encoding.UTF8.GetBytes(KeyString);

		public WopiSecurityHandler(string key = null)
		{
			KeyString = key ?? Environment.MachineName + Environment.ProcessorCount;
		}

		public bool ValidateAccessToken(string value, string token)
		{
			var saltBase64Length = GetBase64Length(_saltLength);
			if (token != null && token.Length >= saltBase64Length)
			{
				var targetHash = GetHash(value, token.Substring(0, saltBase64Length));
				return String.Equals(token, targetHash);
			}
			return false;
		}

		public string GenerateAccessToken(string value)
		{
			return GetHash(value, GetSalt());
		}

		private string GetSalt()
		{
			using (RandomNumberGenerator g = RandomNumberGenerator.Create())
			{
				byte[] salt = new byte[_saltLength];
				g.GetBytes(salt);
				return Convert.ToBase64String(salt);
			}
		}

		private string GetHash(string value, string salt)
		{
			using (var hmacsha256 = new HMACSHA256(Key))
			{
				var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(salt + value));
				return salt + Convert.ToBase64String(hash);
			}
		}

		private int GetBase64Length(int inputLength)
		{
			var codeSize = ((inputLength * 4) / 3);
			var paddingSize = (inputLength % 3) == 0 ? (3 - (inputLength % 3)) : 0;
			var crlfsSize = 2 + (2 * (codeSize + paddingSize) / 72);
			var totalSize = codeSize + paddingSize + crlfsSize;
			return totalSize;
		}
	}
}
