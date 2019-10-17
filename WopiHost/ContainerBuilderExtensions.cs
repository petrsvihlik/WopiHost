using System;
using Autofac;
using WopiHost.Abstractions;

namespace WopiHost
{
    public static class ContainerBuilderExtensions
    {
        public static void AddFileProvider(this ContainerBuilder builder, WopiHostOptions options)
        {
            var providerAssembly = options.WopiFileProviderAssemblyName;
            // Load file provider
#if NET48
			var assembly = AppDomain.CurrentDomain.Load(new System.Reflection.AssemblyName(providerAssembly));
#endif

#if NETCOREAPP3_0
            // Load file provider
            var path = AppContext.BaseDirectory;//PlatformServices.Default.Application.ApplicationBasePath; // http://hishambinateya.com/goodbye-platform-abstractions
            var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath($"{path}\\{providerAssembly}.dll");
#endif
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
        }


        public static void AddCobalt(this ContainerBuilder builder)
        {
            // Load cobalt when running under the full .NET Framework
#if NET48
			var cobaltAssembly = AppDomain.CurrentDomain.Load(new System.Reflection.AssemblyName("WopiHost.Cobalt"));
			builder.RegisterAssemblyTypes(cobaltAssembly).AsImplementedInterfaces();
#endif
        }
    }
}
