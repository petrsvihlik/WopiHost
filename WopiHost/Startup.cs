using System.Collections.Generic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core;
using WopiHost.Core.Models;
using Microsoft.Extensions.Hosting;

namespace WopiHost
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; set; }

        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"config.{env.EnvironmentName}.json", true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                // Override with user secrets (http://go.microsoft.com/fwlink/?LinkID=532709)
                builder.AddUserSecrets<Startup>();
            }
            Configuration = builder.Build();
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            var config = Configuration.GetSection(WopiConfigurationSections.WOPI_ROOT).Get<WopiHostOptions>();
            // Add file provider
            builder.AddFileProvider(config.StorageProviderAssemblyName);

            if (config.UseCobalt)
            {
                // Add cobalt
                builder.AddCobalt();
            }
        }

        /// <summary>
        /// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();//.AddControllersAsServices(); https://autofaccn.readthedocs.io/en/latest/integration/aspnetcore.html#controllers-as-services

            // Ideally, pass a persistent dictionary implementation
            services.AddSingleton<IDictionary<string, LockInfo>>(d => new Dictionary<string, LockInfo>());

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();//Configuration.GetSection("Logging")
                loggingBuilder.AddDebug();
            });

            // Configuration
            services.AddOptions();

            var config = Configuration.GetSection(WopiConfigurationSections.WOPI_ROOT);

            services.Configure<WopiHostOptions>(config);            


            // Add WOPI (depends on file provider)
            services.AddWopi(GetSecurityHandler(services, config.Get<WopiHostOptions>().StorageProviderAssemblyName));
        }

        private IWopiSecurityHandler GetSecurityHandler(IServiceCollection services, string storageProviderAssemblyName)
        {
            var providerBuilder = new ContainerBuilder();
            // Add file provider implementation
            providerBuilder.AddFileProvider(storageProviderAssemblyName);
            providerBuilder.Populate(services);
            var providerContainer = providerBuilder.Build();
            return providerContainer.Resolve<IWopiSecurityHandler>();
        }


        /// <summary>
        /// Configure is called after ConfigureServices is called.
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            // Automatically authenticate
            app.UseAuthentication();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
