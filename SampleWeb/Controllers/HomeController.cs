using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Microsoft.Extensions.Configuration;
using SampleWeb.Models;
using WopiHost.Abstractions;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;

namespace SampleWeb.Controllers
{
    public class HomeController : Controller
    {
        private IWopiSecurityHandler SecurityHandler { get; }
        private IWopiFileProvider FileProvider { get; }
        private IConfiguration Configuration { get; }

        public string WopiClientUrl => Configuration.GetSection("WopiClientUrl").Value;
        public string WopiHostUrl => Configuration.GetSection("WopiHostUrl").Value;


        protected WopiUrlGenerator WopiUrlGenerator => new WopiUrlGenerator(WopiClientUrl, WopiHostUrl, SecurityHandler);

        public HomeController(IWopiSecurityHandler securityHandler, IWopiFileProvider fileProvider, IConfiguration configuration)
        {
            SecurityHandler = securityHandler;
            FileProvider = fileProvider;
            Configuration = configuration;
        }

        public ActionResult Index()
        {
            return View(GetFiles());
        }


        private IEnumerable<FileModel> GetFiles()
        {
            return FileProvider.GetWopiItems().Select(file => new FileModel
            {
                FileName = file.Name,
                FileUrl = (file.WopiItemType == WopiItemType.File) ? WopiUrlGenerator.GetUrl(((IWopiFile)file).Extension, file.Identifier, WopiActionEnum.Edit) : null
            });
        }
    }
}
