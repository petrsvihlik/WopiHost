using System;
using Microsoft.AspNetCore.Authentication;

namespace WopiHost.Core.Security.Authentication
{
	public static class AccessTokenAppBuilderExtensions
	{
	    public static AuthenticationBuilder AddTokenAuthentication(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<AccessTokenAuthenticationOptions> configureOptions)
	    {
	        return builder.AddScheme<AccessTokenAuthenticationOptions, AccessTokenHandler>(authenticationScheme, displayName, configureOptions);
	    }
    }
}
