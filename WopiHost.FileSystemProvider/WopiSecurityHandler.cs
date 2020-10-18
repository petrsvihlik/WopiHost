using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
    public class WopiSecurityHandler : IWopiSecurityHandler
    {
        private readonly JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        private SymmetricSecurityKey _key = null;

        private SymmetricSecurityKey Key
        {
            get
            {
                if (_key is null)
                {
                    //RandomNumberGenerator rng = RandomNumberGenerator.Create();
                    //byte[] key = new byte[128];
                    //rng.GetBytes(key);
                    var key = Encoding.ASCII.GetBytes("secretKeysecretKeysecretKey123"/* + new Random(DateTime.Now.Millisecond).Next(1,999)*/);
                    _key = new SymmetricSecurityKey(key); 
                }
                
                return _key;
            }
        }

        //TODO: abstract
        private readonly Dictionary<string, ClaimsPrincipal> UserDatabase = new Dictionary<string, ClaimsPrincipal>
        {
            {"Anonymous", new ClaimsPrincipal(
                new ClaimsIdentity(new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "12345"),
                    new Claim(ClaimTypes.Name, "Anonymous"),
                    new Claim(ClaimTypes.Email, "anonymous@domain.tld"),

                    //TDOO: this needs to be done per file
                    new Claim(WopiClaimTypes.UserPermissions, (WopiUserPermissions.UserCanWrite | WopiUserPermissions.UserCanRename | WopiUserPermissions.UserCanAttend | WopiUserPermissions.UserCanPresent).ToString())
                })
            ) }
        };

        public SecurityToken GenerateAccessToken(string userId, string resourceId)
        {
            var user = UserDatabase[userId];

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = user.Identities.FirstOrDefault(),
                Expires = DateTime.UtcNow.AddHours(1), //access token ttl: https://wopi.readthedocs.io/projects/wopirest/en/latest/concepts.html#term-access-token-ttl
                SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
            };

            return tokenHandler.CreateToken(tokenDescriptor);
        }

        public ClaimsPrincipal GetPrincipal(string tokenString)
        {
            //TODO: https://github.com/aspnet/Security/tree/master/src/Microsoft.AspNetCore.Authentication.JwtBearer

            var tokenValidation = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateActor = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Key
            };

            try
            {
                // Try to validate the token
                var principal = tokenHandler.ValidateToken(tokenString, tokenValidation, out SecurityToken token);
                return principal;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public bool IsAuthorized(ClaimsPrincipal principal, string resourceId, WopiAuthorizationRequirement operation)
        {

            //TODO: logic
            return true;
        }

        /// <summary>
        /// Converts the security token to a Base64 string.
        /// </summary>
        public string WriteToken(SecurityToken token)
        {
            return tokenHandler.WriteToken(token);
        }
    }
}
