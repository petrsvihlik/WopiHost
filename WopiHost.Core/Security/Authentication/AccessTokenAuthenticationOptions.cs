
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication
{
    public class AccessTokenAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// Defines whether the token should be stored in the
        /// Http.Authentication.AuthenticationProperties after a successful authorization.
        /// </summary>
        public bool SaveToken { get; set; } = true;


        public IWopiSecurityHandler SecurityHandler { get; set; }

        public AccessTokenAuthenticationOptions()
        {
        }
    }
}
