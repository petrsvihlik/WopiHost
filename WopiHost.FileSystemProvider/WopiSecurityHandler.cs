using System;
using System.Security.Cryptography;
using System.Text;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiSecurityHandler : IWopiSecurityHandler
	{
		private const int _saltLength = 8;
		private const int _base64Correction = 4;

		private string KeyString { get; }

		private byte[] Key => Encoding.UTF8.GetBytes(KeyString);

		readonly HashAlgorithm hasher = SHA256.Create();

		public WopiSecurityHandler(string key = null)
		{
			KeyString = key ?? Environment.MachineName + Environment.ProcessorCount;
		}

		public bool ValidateAccessToken(string value, string token)
		{
			   var correctedSaltLength = _saltLength + _base64Correction;
			if (token.Length >= correctedSaltLength)
			{
				var targetHash = GetHash(value, token.Substring(0, correctedSaltLength));
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
			byte[] salt = new byte[_saltLength];
			var saltHash = hasher.ComputeHash(salt);
			return Convert.ToBase64String(saltHash);
		}

		private string GetHash(string value, string salt)
		{
			using (var hmacsha256 = new HMACSHA256(Key))
			{
				var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(salt + value));
				return salt + Convert.ToBase64String(hash);
			}
		}
	}
}
