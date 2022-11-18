using System.Runtime.Loader;
using Autofac;

namespace WopiHost;

public static class ContainerBuilderExtensions
{
    public static void AddFileProvider(this ContainerBuilder builder, string storageProviderAssemblyName)
    {
        // Load file provider
        //TODO: load by name? AssemblyLoadContext.Default.LoadFromAssemblyName
        //TODO: Unloadability https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
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
