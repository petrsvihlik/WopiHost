using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.AspNetCore.Authorization;
using WopiHost.Authorization;
using Newtonsoft.Json.Serialization;

namespace WopiHost
{
	public class Startup
	{
		//TODO: investigate objects: IApplicationEnvironment, IRuntimeEnvironment, IAssemblyLoaderContainer, IAssemblyLoadContextAccessor, ILibraryManager, IHostingEnvironment (Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default)

		public IConfigurationRoot Configuration { get; set; }

		public Startup(IHostingEnvironment env)
		{
			var appEnv = PlatformServices.Default.Application;
			var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).
				AddInMemoryCollection(new Dictionary<string, string>
					{ { nameof(env.WebRootPath), env.WebRootPath },
					{ nameof(appEnv.ApplicationBasePath), appEnv.ApplicationBasePath } })
				.AddJsonFile($"config.{env.EnvironmentName}.json", optional: true).
				AddEnvironmentVariables();

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
			services.AddAuthorization(options =>
			{
				options.AddPolicy(PolicyNames.HasValidAccessToken, policy =>
				{
					policy.Requirements.Add(new AccessTokenRequirement());
				});
			});

			services.AddTransient<IAuthorizationHandler, WopiAuthorizationHandler>();

			//TODO: check whether OWA is case sensitive, optionally remove the contract resolver
			services.AddMvc().AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());


			/* TODO: #10
			services.AddCaching();
			services.AddSession();

			services.ConfigureSession(o =>
			{
				o.IdleTimeout = TimeSpan.FromMinutes(5);
			});*/


			// Autofac resolution
			var builder = new ContainerBuilder();

			// Configuration
			builder.RegisterInstance(Configuration).As<IConfiguration>().SingleInstance();

			var path = PlatformServices.Default.Application.ApplicationBasePath;

			// File provider implementation
			var providerAssembly = Configuration.GetValue("WopiFileProviderAssemblyName", string.Empty);
#if NET46

			var assembly = AppDomain.CurrentDomain.Load(new AssemblyName(providerAssembly));
#endif

#if NETCOREAPP1_0
			var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path + "\\" + providerAssembly + ".dll");
#endif
			builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();

			builder.Populate(services);
			var container = builder.Build();
			return container.Resolve<IServiceProvider>();
		}


		// Configure is called after ConfigureServices is called.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			// Add MVC to the request pipeline.
			//TODO:#10
			//app.UseSession();
			app.UseMvc();
		}
	}
}
