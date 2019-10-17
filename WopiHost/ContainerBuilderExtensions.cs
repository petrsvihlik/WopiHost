using System;
using System.Runtime.Loader;
using Autofac;
using WopiHost.Abstractions;

namespace WopiHost
{
    public static class ContainerBuilderExtensions
    {
        public static void AddFileProvider(this ContainerBuilder builder, WopiHostOptions options)
        {
            //TODO: options should be a specific section of the config

            var providerAssembly = options.WopiFileProviderAssemblyName;
            // Load file provider
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath($"{AppContext.BaseDirectory}\\{providerAssembly}.dll");
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
        }


        public static void AddCobalt(this ContainerBuilder builder)
        {
            //TODO Convert Cobalt to .NET Standard
            // Load Cobalt            
            //var cobaltAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath($"{AppContext.BaseDirectory}\\WopiHost.Cobalt.dll");
			//builder.RegisterAssemblyTypes(cobaltAssembly).AsImplementedInterfaces();
        }
    }
}
