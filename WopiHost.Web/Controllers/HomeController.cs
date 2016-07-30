using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

		private IConfiguration Configuration { get; }

		public string WopiClientUrl => Configuration.GetSection("WopiClientUrl").Value;

		public string WopiHostUrl => Configuration.GetSection("WopiHostUrl").Value;


		protected WopiUrlGenerator WopiUrlGenerator => new WopiUrlGenerator(WopiClientUrl, WopiHostUrl, SecurityHandler);

		public HomeController(IWopiSecurityHandler securityHandler, IConfiguration configuration)
		{
			SecurityHandler = securityHandler;
			Configuration = configuration;
		}

		public async Task<ActionResult> Index()
		{
			return View(await GetFilesAsync());
		}

		private async Task<IEnumerable<FileModel>> GetFilesAsync()
		{
			HttpClient client = new HttpClient();
			Stream stream = await client.GetStreamAsync(WopiHostUrl + "/folders/TODO?access_token=todo");

			var serializer = new JsonSerializer();

			using (var sr = new StreamReader(stream))
			using (var jsonTextReader = new JsonTextReader(sr))
			{
				dynamic x = serializer.Deserialize(jsonTextReader);
			}


			//var fileModelTasks =   FileProvider.GetWopiItems().Select(async file => new FileModel
			//{
			//	Name = file.Name,
			//	Url = (file.WopiItemType == WopiItemType.File) ? (await WopiUrlGenerator.GetUrlAsync(((IWopiFile)file).Extension, file.Identifier, WopiActionEnum.Edit)) : null
			//});
			//return await Task.WhenAll(fileModelTasks);	
			return null;		
		}
	}
}
