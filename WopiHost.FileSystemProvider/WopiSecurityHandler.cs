using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
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
                if (_key == null)
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
        private Dictionary<string, ClaimsPrincipal> UserDatabase = new Dictionary<string, ClaimsPrincipal>
        {
            {"Anonymous",new ClaimsPrincipal(
                new ClaimsIdentity(new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "12345"),
                    new Claim(ClaimTypes.Name, "Anonymous"),
                    new Claim(ClaimTypes.Email, "anonymous@domain.tld"),
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
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
            };

            return tokenHandler.CreateToken(tokenDescriptor);
        }


        public ClaimsPrincipal GetPrincipal(string tokenString)
        {
            //TODO: https://github.com/aspnet/Security/tree/master/src/Microsoft.AspNetCore.Authentication.JwtBearer

            var tokenValidation = new TokenValidationParameters();
            tokenValidation.ValidateAudience = false;
            tokenValidation.ValidateIssuer = false;
            tokenValidation.ValidateActor = false;
            tokenValidation.ValidateLifetime = true;
            tokenValidation.ValidateIssuerSigningKey = true;
            tokenValidation.IssuerSigningKey = Key;

            try
            {
                // Try to validate the token
                SecurityToken token = null;
                var principal = tokenHandler.ValidateToken(tokenString, tokenValidation, out token);
                return principal;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public bool IsAuthorized(ClaimsPrincipal principal, string resourceId, IAuthorizationRequirement operation)
        {
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
