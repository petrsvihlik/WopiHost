using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace WopiHost.Security
{
	public static class AccessTokenAppBuilderExtensions
	{
		public static IApplicationBuilder UseAccessTokenAuthentication(this IApplicationBuilder app)
		{
			if (app == null)
			{
				throw new ArgumentNullException(nameof(app));
			}

			return app.UseMiddleware<AccessTokenAuthenticationMiddleware>();
		}

		public static IApplicationBuilder UseAccessTokenAuthentication(this IApplicationBuilder app, AccessTokenAuthenticationOptions options)
		{
			if (app == null)
			{
				throw new ArgumentNullException(nameof(app));
			}
			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			return app.UseMiddleware<AccessTokenAuthenticationMiddleware>(Options.Create(options));
		}
	}
}
