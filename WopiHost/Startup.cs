using System;
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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace WopiHost
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; set; }

        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", true, true)
                .AddInMemoryCollection(new Dictionary<string, string>
                    { { nameof(env.WebRootPath), env.WebRootPath },
                    { "ApplicationBasePath", AppContext.BaseDirectory } })
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
            // Add file provider
            builder.AddFileProvider(Configuration.Get<WopiHostOptions>());

            if (Configuration.GetValue<bool>("UseCobalt"))
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
            services.Configure<WopiHostOptions>(Configuration);                       

            // Add WOPI (depends on file provider)
            services.AddWopi(GetSecurityHandler(services));

        }

        private IWopiSecurityHandler GetSecurityHandler(IServiceCollection services)
        {
            var providerBuilder = new ContainerBuilder();
            // Add file provider implementation
            providerBuilder.AddFileProvider(services.BuildServiceProvider().GetRequiredService<IOptionsSnapshot<WopiHostOptions>>().Value);
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
