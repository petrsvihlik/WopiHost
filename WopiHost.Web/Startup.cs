using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;

namespace WopiHost.Web
{
    public class Startup
    {
        public IConfiguration Configuration { get; set; }

        public Startup(IWebHostEnvironment env)
        {
            var baseDir = System.AppContext.BaseDirectory;
            var builder = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).
                AddInMemoryCollection(new Dictionary<string, string>
                    { { nameof(env.WebRootPath), env.WebRootPath },
                    { "ApplicationBasePath", baseDir } })
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
            services.AddControllersWithViews();
            services.AddMvc();
            services.AddSingleton(Configuration);

            // Configuration
            services.AddOptions();
            services.Configure<WopiHostOptions>(Configuration);
            services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();//Configuration.GetSection("Logging")
                loggingBuilder.AddDebug();
            });
        }

        /// <summary>
        /// Configure is called after ConfigureServices is called.
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseHttpsRedirection();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            app.UseRouting();

            // Add MVC to the request pipeline.
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
