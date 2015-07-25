using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Dnx;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Runtime;
using WopiHost.Attributes;
using WopiHost.Contracts;

namespace WopiHost
{
    public class Startup
    {
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly ILibraryManager _libraryManager;
        private readonly IHostingEnvironment _env;
        private readonly IAssemblyLoaderContainer _loaderContainer;

        public Startup(IHostingEnvironment env, IAssemblyLoaderContainer container,
                       IAssemblyLoadContextAccessor accessor, ILibraryManager libraryManager)
        {
            _env = env;
            _loaderContainer = container;
            _loadContextAccessor = accessor;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
        /// </summary>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddTransient<WopiAuthorizationAttribute>();

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
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
