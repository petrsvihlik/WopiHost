using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace WopiHost.Web.Controllers
{
    public class HomeController : Controller
    {
        private WopiUrlBuilder _urlGenerator;

        private IOptionsSnapshot<WopiOptions> WopiOptions { get; }
        private IWopiStorageProvider StorageProvider { get; }
        private IDiscoverer Discoverer { get; }
        public IConfiguration Configuration { get; }


        //TODO: remove test culture value and load it from configuration SECTION
        public WopiUrlBuilder UrlGenerator => _urlGenerator ??= new WopiUrlBuilder(Discoverer, new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") });

        public HomeController(IOptionsSnapshot<WopiOptions> wopiOptions, IWopiStorageProvider storageProvider, IDiscoverer discoverer, IConfiguration configuration)
        {
            WopiOptions = wopiOptions;
            StorageProvider = storageProvider;
            Discoverer = discoverer;
            Configuration = configuration;
        }

        public async Task<ActionResult> Index()
        {
            try
            {
                var files = StorageProvider.GetWopiFiles(StorageProvider.RootContainerPointer.Identifier);
                var fileViewModels = new List<FileViewModel>();
                foreach (var file in files)
                {
                    fileViewModels.Add(new FileViewModel
                    {
                        FileId = file.Identifier,
                        FileName = file.Name,
                        SupportsEdit = await Discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                        SupportsView = await Discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                        IconUri = (await Discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("file.ico", UriKind.Relative)
                    });
                }
                return View(fileViewModels);
            }
            catch (DiscoveryException ex)
            {
                return View("Error", ex);
            }
            catch (HttpRequestException ex)
            {
                return View("Error", ex);
            }
        }

        public async Task<ActionResult> Detail(string id, string wopiAction)
        {
            var actionEnum = Enum.Parse<WopiActionEnum>(wopiAction);
            var securityHandler = new WopiSecurityHandler();

            var file = StorageProvider.GetWopiFile(id);
            var token = securityHandler.GenerateAccessToken("Anonymous", file.Identifier);


            ViewData["access_token"] = securityHandler.WriteToken(token);
            //TODO: fix
            //ViewData["access_token_ttl"] = //token.ValidTo

            //http://dotnet-stuff.com/tutorials/aspnet-mvc/how-to-render-different-layout-in-asp-net-mvc
            
            var extension = file.Extension.TrimStart('.');
            ViewData["urlsrc"] = await UrlGenerator.GetFileUrlAsync(extension, $"{Configuration.GetServiceUri("wopihost")}wopi/files/{id}", actionEnum);
            ViewData["favicon"] = await Discoverer.GetApplicationFavIconAsync(extension);
            return View();
        }

    }
}
