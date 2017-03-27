using System;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;

namespace WopiHost
{
    public static class ContainerBuilderExtensions
    {
        public static void AddFileProvider(this ContainerBuilder builder, IConfigurationRoot configuration)
        {
            var providerAssembly = configuration.GetValue("WopiFileProviderAssemblyName", string.Empty);
#if NET461
// Load file provider
			var assembly = AppDomain.CurrentDomain.Load(new System.Reflection.AssemblyName(providerAssembly));
#endif

#if NETCOREAPP1_0
            // Load file provider
            var path = PlatformServices.Default.Application.ApplicationBasePath;
            var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path + "\\" + providerAssembly + ".dll");
#endif
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
        }


        public static void AddCobalt(this ContainerBuilder builder)
        {
#if NET461
// Load cobalt when running under the full .NET Framework
			var cobaltAssembly = AppDomain.CurrentDomain.Load(new System.Reflection.AssemblyName("WopiHost.Cobalt"));
			builder.RegisterAssemblyTypes(cobaltAssembly).AsImplementedInterfaces();
            
#endif
        }
    }
}
