using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Autofac.Framework.DependencyInjection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using WopiHost.Attributes;

namespace WopiHost
{
	public class Startup
	{
		private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
		private readonly ILibraryManager _libraryManager;
		private readonly IApplicationEnvironment _appEnv;
		private readonly IHostingEnvironment _env;
		private readonly IAssemblyLoaderContainer _loaderContainer;
		public IConfigurationRoot Configuration { get; set; }

		public Startup(IHostingEnvironment env, IAssemblyLoaderContainer container,
					   IAssemblyLoadContextAccessor accessor, ILibraryManager libraryManager, IApplicationEnvironment appEnv)
		{
			_env = env;
			_loaderContainer = container;
			_loadContextAccessor = accessor;
			_libraryManager = libraryManager;
			_appEnv = appEnv;



			var builder = new ConfigurationBuilder().SetBasePath(appEnv.ApplicationBasePath).
				AddInMemoryCollection(new Dictionary<string, string>
					{ { nameof(env.WebRootPath), env.WebRootPath },
					{ nameof(appEnv.ApplicationBasePath), appEnv.ApplicationBasePath } })
				/*.AddJsonFile("config.json")
				.AddJsonFile($"config.{env.EnvironmentName}.json", optional: true)*/;

			builder.AddEnvironmentVariables();

			if (env.IsDevelopment())
			{
				// Override with user secrets (http://go.microsoft.com/fwlink/?LinkID=532709)
				builder.AddUserSecrets();
			}
			Configuration = builder.Build();
		}

		/// <summary>
		/// Sets up the DI container. Loads types dynamically (http://docs.autofac.org/en/latest/register/scanning.html)
		/// </summary>
		public IServiceProvider ConfigureServices(IServiceCollection services)
		{
			services.AddMvc();

			/* TODO: #10
			services.AddCaching();
			services.AddSession();

			services.ConfigureSession(o =>
			{
				o.IdleTimeout = TimeSpan.FromMinutes(5);
			});*/

			services.AddTransient<WopiAuthorizationAttribute>();

			// Autofac resolution
			var builder = new ContainerBuilder();

			// Configuration
			builder.RegisterInstance(Configuration).As<IConfiguration>().SingleInstance();

			// File provider implementation
			var providerAssembly = Configuration.GetSection("WopiFileProviderAssemblyName").Value;
			var assembly = AppDomain.CurrentDomain.Load(new AssemblyName(providerAssembly));
			builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();

			builder.Populate(services);
			var container = builder.Build();
			return container.Resolve<IServiceProvider>();
		}


		// Configure is called after ConfigureServices is called.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.MinimumLevel = LogLevel.Information;
			loggerFactory.AddConsole();
			loggerFactory.AddDebug();

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			// Add the platform handler to the request pipeline.
			app.UseIISPlatformHandler();

			// Add MVC to the request pipeline.
			//TODO:#10
			//app.UseSession();
			app.UseMvc();
		}
	}
}
