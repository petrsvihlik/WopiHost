using System;
using System.Reflection;
using Autofac;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using Autofac.Framework.DependencyInjection;

namespace SampleWeb
{
    public class Startup
    {
        private readonly ILibraryManager _libraryManager;

        public Startup(IHostingEnvironment env, ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public IConfiguration Configuration { get; set; }


        /// <summary>
        /// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
        /// </summary>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            // Autofac resolution
            var builder = new ContainerBuilder();

            // Configuration
            Configuration configuration = new Configuration();
            configuration.AddEnvironmentVariables();
            builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();

            // File provider implementation
            var providerAssembly = configuration.Get("WopiFileProviderAssemblyName");
            var assembly = AppDomain.CurrentDomain.Load(new AssemblyName(providerAssembly));
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();

            builder.Populate(services);
            var container = builder.Build();
            return container.Resolve<IServiceProvider>();
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerfactory)
        {
            // Add static files to the request pipeline.
            app.UseStaticFiles();
            
            // Add MVC to the request pipeline.
            app.UseMvc(routes =>
            {
                
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action}/{id?}",
                    defaults: new { controller = "Home", action = "Index" });
            });
        }
    }
}
