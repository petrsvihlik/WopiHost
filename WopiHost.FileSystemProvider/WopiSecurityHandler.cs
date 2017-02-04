using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
			KeyString = key ?? new Random(DateTime.Now.Millisecond).Next(0, int.MaxValue).ToString();
		}


		public ClaimsPrincipal GetPrincipal(string token)
		{
			//TODO: get principal from token validator.ValidateToken(token, validationParameters, out validatedToken);
			//https://github.com/aspnet/Security/tree/master/src/Microsoft.AspNetCore.Authentication.JwtBearer

			var principal = new ClaimsPrincipal();
			principal.AddIdentity(new ClaimsIdentity(new List<Claim>
				{
					new Claim(ClaimTypes.NameIdentifier, "12345"),
					new Claim(ClaimTypes.Name, "Anonymous"),
					new Claim(ClaimTypes.Email, "anonymous@domain.tld"),
					new Claim(WopiClaimTypes.UserPermissions, (WopiUserPermissions.UserCanWrite | WopiUserPermissions.UserCanRename | WopiUserPermissions.UserCanAttend | WopiUserPermissions.UserCanPresent).ToString())
				}));
			return principal;
		}

		public bool IsAuthorized(ClaimsPrincipal principal, string resource, IAuthorizationRequirement operation)
		{
			return true;
		}

		/// <summary>
		/// Validates the given value against the authorization token.
		/// </summary>
		/// <param name="value">Value to validate</param>
		/// <param name="token">Authorization token</param>
		/// <returns>TRUE if the token is valid.</returns>
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
			using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
			{
				byte[] salt = new byte[_saltLength];
				generator.GetBytes(salt);
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
