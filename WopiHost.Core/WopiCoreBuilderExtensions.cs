using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core
{
    public static class WopiCoreBuilderExtensions
    {
        public static void AddWopi(this IServiceCollection services)
        {
            services.AddAuthorization();

            // Add authorization handler
            services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();

            services.AddMvcCore()
                .AddApplicationPart(typeof(WopiCoreBuilderExtensions).GetTypeInfo().Assembly)
                .AddJsonFormatters()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                });
        }

        public static IApplicationBuilder UseWopi(this IApplicationBuilder app, IWopiSecurityHandler securityHandler)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            // Add MVC to the request pipeline.
            app.UseAccessTokenAuthentication(new AccessTokenAuthenticationOptions { SecurityHandler = securityHandler });

            return app;
        }
    }
}
