using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;

namespace WopiHost.Web.Controllers
{
    public class HomeController : Controller
    {
        private WopiUrlBuilder _urlGenerator;

        private IConfiguration Configuration { get; }

        protected IWopiStorageProvider StorageProvider { get; set; }

        public string WopiHostUrl => Configuration.GetValue("WopiHostUrl", string.Empty);

        /// <summary>
        /// URL to OWA or OOS
        /// </summary>
        public string WopiClientUrl => Configuration.GetValue("WopiClientUrl", string.Empty);

        public WopiDiscoverer Discoverer => new WopiDiscoverer(new HttpDiscoveryFileProvider(WopiClientUrl));

        //TODO: remove test culture value and load it from configuration SECTION
        public WopiUrlBuilder UrlGenerator => _urlGenerator ?? (_urlGenerator = new WopiUrlBuilder(Discoverer, new WopiUrlSettings { UI_LLCC = new CultureInfo("en-US") }));

        public HomeController(IConfiguration configuration, IWopiStorageProvider storageProvider)
        {
            Configuration = configuration;
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
            ViewData["urlsrc"] = await UrlGenerator.GetFileUrlAsync(extension, $"{WopiHostUrl}/wopi/files/{id}", WopiActionEnum.Edit);
            ViewData["favicon"] = await Discoverer.GetApplicationFavIconAsync(extension);
            return View();
        }

    }
}
