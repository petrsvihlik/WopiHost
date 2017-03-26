using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using WopiHost.Abstractions;
using WopiHost.Core;
using WopiHost.Core.Models;

namespace WopiHost
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; set; }

        private IContainer _container;

        public Startup(IHostingEnvironment env)
        {
            var appEnv = PlatformServices.Default.Application;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", true, true)
                .AddInMemoryCollection(new Dictionary<string, string>
                    { { nameof(env.WebRootPath), env.WebRootPath },
                    { nameof(appEnv.ApplicationBasePath), appEnv.ApplicationBasePath } })
                .AddJsonFile($"config.{env.EnvironmentName}.json", true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                // Override with user secrets (http://go.microsoft.com/fwlink/?LinkID=532709)
                builder.AddUserSecrets<Startup>();
            }
            Configuration = builder.Build();
        }

        /// <summary>
        /// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
        /// </summary>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            // Ideally, pass a persistant dictionary implementation
            services.AddSingleton<IDictionary<string, LockInfo>>(d => new Dictionary<string, LockInfo>());
            
            // Add WOPI
            services.AddWopi();

            // Autofac resolution
            var builder = new ContainerBuilder();

            // Configuration
            builder.RegisterInstance(Configuration).As<IConfiguration>().SingleInstance();

            // Add cobalt
            builder.AddCobalt();

            // Add file provider implementation
            builder.AddFileProvider(Configuration);

            builder.Populate(services);
            _container = builder.Build();
            return new AutofacServiceProvider(_container);//_container.Resolve<IServiceProvider>();
        }


        /// <summary>
        /// Configure is called after ConfigureServices is called.
        /// </summary>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWopi(_container.Resolve<IWopiSecurityHandler>()).UseMvc();
        }
    }
}
