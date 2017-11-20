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
using Microsoft.Extensions.Options;

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

            // Configuration
            services.AddOptions();
            services.Configure<WopiHostOptions>(Configuration);

            // Autofac resolution
            var builder = new ContainerBuilder();

            // Add cobalt
            builder.AddCobalt();

            // Add file provider implementation
            builder.AddFileProvider(services.BuildServiceProvider().GetRequiredService<IOptionsSnapshot<WopiHostOptions>>().Value);

            // Add WOPI (depends on file provider)
            services.AddWopi(services.BuildServiceProvider().GetRequiredService<IWopiSecurityHandler>());

            builder.Populate(services);
            _container = builder.Build();
            //return _container.Resolve<IServiceProvider>();
            return new AutofacServiceProvider(_container);
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

            app.UseMvc();
        }
    }
}
