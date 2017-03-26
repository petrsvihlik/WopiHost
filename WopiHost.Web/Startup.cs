using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;

namespace WopiHost.Web
{
	public class Startup
	{
		public IConfiguration Configuration { get; set; }

		public Startup(IHostingEnvironment env)
		{
			var appEnv = PlatformServices.Default.Application;
			var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).
				AddInMemoryCollection(new Dictionary<string, string>
					{ { nameof(env.WebRootPath), env.WebRootPath },
					{ nameof(appEnv.ApplicationBasePath), appEnv.ApplicationBasePath } })
				.AddJsonFile($"config.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();

			if (env.IsDevelopment())
			{
				// Override with user secrets (http://go.microsoft.com/fwlink/?LinkID=532709)
				builder.AddUserSecrets<Startup>();
			}
			Configuration = builder.Build();
		}


		/// <summary>
		/// Sets up the DI container.
		/// </summary>
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMvc();
			services.AddSingleton(Configuration);
		}

		/// <summary>
		/// Configure is called after ConfigureServices is called.
		/// </summary>
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			app.UseDeveloperExceptionPage();

			// Add static files to the request pipeline.
			app.UseStaticFiles();

			// Add MVC to the request pipeline.
			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});
		}
	}
}
