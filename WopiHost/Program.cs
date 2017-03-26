using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace WopiHost
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Get hosting URL configuration
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .AddUserSecrets<Startup>()
                .Build();

            var url = config["ASPNETCORE_URLS"] ?? "http://*:5000";

            var host = new WebHostBuilder()
                .UseUrls(url)
                .UseConfiguration(config)
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
