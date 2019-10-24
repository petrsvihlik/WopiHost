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

namespace WopiHost.Web.Controllers
{
    public class HomeController : Controller
    {
        private WopiUrlBuilder _urlGenerator;

        private IOptionsSnapshot<WopiOptions> WopiOptions { get; }

        protected IWopiStorageProvider StorageProvider { get; set; }
        
        public WopiDiscoverer Discoverer => new WopiDiscoverer(new HttpDiscoveryFileProvider(WopiOptions.Value.ClientUrl));

        //TODO: remove test culture value and load it from configuration SECTION
        public WopiUrlBuilder UrlGenerator => _urlGenerator ?? (_urlGenerator = new WopiUrlBuilder(Discoverer, new WopiUrlSettings { UI_LLCC = new CultureInfo("en-US") }));

        public HomeController(IOptionsSnapshot<WopiOptions> wopiOptions, IWopiStorageProvider storageProvider)
        {
            WopiOptions = wopiOptions;
            StorageProvider = storageProvider;
        }

        public async Task<ActionResult> Index()
        {
            try
            {
                return View(StorageProvider.GetWopiFiles(StorageProvider.RootContainerPointer.Identifier));
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

        public async Task<ActionResult> Detail(string id)
        {
            WopiSecurityHandler securityHandler = new WopiSecurityHandler();

            IWopiFile file = StorageProvider.GetWopiFile(id);
            var token = securityHandler.GenerateAccessToken("Anonymous", file.Identifier);
            
            
            ViewData["access_token"] = securityHandler.WriteToken(token);
            //TODO: fix
            //ViewData["access_token_ttl"] = //token.ValidTo

            //http://dotnet-stuff.com/tutorials/aspnet-mvc/how-to-render-different-layout-in-asp-net-mvc


            var extension = file.Extension.TrimStart('.');
            ViewData["urlsrc"] = await UrlGenerator.GetFileUrlAsync(extension, $"{WopiOptions.Value.HostUrl}/wopi/files/{id}", WopiActionEnum.Edit);
            ViewData["favicon"] = await Discoverer.GetApplicationFavIconAsync(extension);
            return View();
        }

    }
}
