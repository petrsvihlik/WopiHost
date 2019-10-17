using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
//using Newtonsoft.Json.Serialization;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core
{
    public static class WopiCoreBuilderExtensions
    {
        public static void AddWopi(this IServiceCollection services, IWopiSecurityHandler securityHandler)
        {
            services.AddAuthorizationCore();

            // Add authorization handler
            services.AddSingleton<IAuthorizationHandler, WopiAuthorizationHandler>();

            services.AddMvcCore()
                .AddApplicationPart(typeof(WopiCoreBuilderExtensions).GetTypeInfo().Assembly)
               
                //.AddJsonFormatters()
                //.AddJsonOptions(options =>
                //{
                //    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                //})
                ;

            services.AddAuthentication(o => { o.DefaultScheme = AccessTokenDefaults.AuthenticationScheme; })
                .AddTokenAuthentication(AccessTokenDefaults.AuthenticationScheme, AccessTokenDefaults.AuthenticationScheme, options => { options.SecurityHandler = securityHandler; });
        }
    }
}
