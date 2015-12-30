//using Microsoft.AspNet.Builder;
//using Microsoft.AspNet.Hosting;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;

//namespace WopiHost.Web
//{
//    public class Startup
//    {
//        public Startup(IHostingEnvironment env)
//        {
//            // Set up configuration sources.
//            var builder = new ConfigurationBuilder()
//                .AddJsonFile("appsettings.json")
//                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

//            if (env.IsDevelopment())
//            {
//                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
//                builder.AddUserSecrets();
//            }

//            builder.AddEnvironmentVariables();
//            Configuration = builder.Build();
//        }

//        public IConfigurationRoot Configuration { get; set; }

//        // This method gets called by the runtime. Use this method to add services to the container.
//        public void ConfigureServices(IServiceCollection services)
//        {

//            services.AddMvc();

//        }

//        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
//        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
//        {
//            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
//            loggerFactory.AddDebug();

//            if (env.IsDevelopment())
//            {
//                app.UseBrowserLink();
//                app.UseDeveloperExceptionPage();
//                app.UseDatabaseErrorPage();
//            }
//            else
//            {
//                app.UseExceptionHandler("/Home/Error");
//            }

//            app.UseIISPlatformHandler(options => options.AuthenticationDescriptions.Clear());

//            app.UseStaticFiles();

//            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

//            app.UseMvc(routes =>
//            {
//                routes.MapRoute(
//                    name: "default",
//                    template: "{controller=Home}/{action=Index}/{id?}");
//            });
//        }

//        // Entry point for the application.
//        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
//    }
//}

using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace SampleWeb
{
	public class Startup
	{
		public IConfiguration Configuration { get; set; }

		public Startup(IHostingEnvironment env, IApplicationEnvironment appEnv)
		{

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
				//app.UseBrowserLink();
				app.UseDeveloperExceptionPage();
			}

			// Add the platform handler to the request pipeline.
			app.UseIISPlatformHandler();

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

		// Entry point for the application.
		public static void Main(string[] args) => WebApplication.Run<Startup>(args);
	}
}
