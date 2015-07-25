using WopiHost.Contracts;

namespace WopiFileSystemProvider
{
    //TODO: move back to own DLL when ready
 //   public static class WopiFileSystemContainerExtension 
	//{
 //       public static IServiceCollection AddFileSystemProvider(this ContainerBuilder builder, IServiceCollection services)
 //       {
 //           // Autofac
 //           builder.RegisterType<WopiFileSystemProvider>().As<IWopiFileProvider>().InstancePerLifetimeScope();
 //           builder.RegisterType<WopiSecurityHandler>().As<IWopiSecurityHandler>().InstancePerLifetimeScope();
 //           builder.RegisterType<WopiFile>().As<IWopiFile>().InstancePerLifetimeScope();
 //           return services;
 //       }
 //       public static IServiceCollection AddFileSystemProvider(this IServiceCollection services)
 //       {
 //           services.AddTransient<IWopiFileProvider, WopiFileSystemProvider>();
 //           services.AddTransient<IWopiSecurityHandler, WopiSecurityHandler>();
 //           services.AddTransient<IWopiFile, WopiFile>();
 //           return services;
 //       }
 //   }
}
