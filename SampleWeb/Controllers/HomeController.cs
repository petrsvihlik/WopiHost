using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.ConfigurationModel;
using SampleWeb.Models;
using WopiDiscovery.Enumerations;
using WopiHost.Contracts;
using WopiHost.Urls;

namespace SampleWeb.Controllers
{
    public class HomeController : Controller
    {
        private IWopiSecurityHandler SecurityHandler { get; }
        private IWopiFileProvider FileProvider { get; }
        private IConfiguration Configuration { get; }

        public string WopiClientUrl => Configuration.Get("WopiClientUrl");
        public string WopiHostUrl => Configuration.Get("WopiHostUrl");


        protected WopiUrlGenerator WopiUrlGenerator => new WopiUrlGenerator(WopiClientUrl, WopiHostUrl, SecurityHandler);

        public HomeController(IWopiSecurityHandler securityHandler, IWopiFileProvider fileProvider, IConfiguration configuration)
        {
            SecurityHandler = securityHandler;
            FileProvider = fileProvider;
            Configuration = configuration;
        }

        public ActionResult Index()
        {
            ViewBag.Message = "List of files.";
            return View(GetFiles());
        }


        private IEnumerable<FileModel> GetFiles()
        {
            return FileProvider.GetWopiFiles().Select(file => new FileModel
            {
                FileName = file.Name,
                FileUrl = WopiUrlGenerator.GetUrl(file.Extension, file.Identifier, WopiActionEnum.Edit)
            });
        }
    }
}
