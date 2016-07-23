using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WopiHost.Abstractions;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Web.Models;

namespace WopiHost.Web.Controllers
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

		public async Task<ActionResult> Index()
		{
			return View(await GetFilesAsync());
		}


		private async Task<IEnumerable<FileModel>> GetFilesAsync()
		{
			var fileModelTasks =   FileProvider.GetWopiItems().Select(async file => new FileModel
			{
				FileName = file.Name,
				FileUrl = (file.WopiItemType == WopiItemType.File) ? (await WopiUrlGenerator.GetUrlAsync(((IWopiFile)file).Extension, file.Identifier, WopiActionEnum.Edit)) : null
			});
			return await Task.WhenAll(fileModelTasks);			
		}
	}
}
