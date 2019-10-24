using System;
using System.Runtime.Loader;
using Autofac;

namespace WopiHost
{
    public static class ContainerBuilderExtensions
    {
        public static void AddFileProvider(this ContainerBuilder builder, string storageProviderAssemblyName)
        {
            // Load file provider
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath($"{AppContext.BaseDirectory}\\{storageProviderAssemblyName}.dll");
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
        }


        public static void AddCobalt(this ContainerBuilder builder)
        {
            // Load Cobalt            
            var cobaltAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath($"{AppContext.BaseDirectory}\\WopiHost.Cobalt.dll");
			builder.RegisterAssemblyTypes(cobaltAssembly).AsImplementedInterfaces();
        }
    }
}
