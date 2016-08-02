using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using WopiHost.Web.Models;

namespace WopiHost.Web.Controllers
{
	public class HomeController : Controller
	{
		private IConfiguration Configuration { get; }

		public string WopiClientUrl => Configuration.GetSection("WopiClientUrl").Value;

		public string WopiHostUrl => Configuration.GetSection("WopiHostUrl").Value;

		public HomeController(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public async Task<ActionResult> Index()
		{
			return View(await GetFilesAsync());
		}

		private async Task<IEnumerable<Models.File>> GetFilesAsync()
		{
			HttpClient client = new HttpClient();
			//TODO: get token
			//TODO: root folder id http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetRootContainer.html?highlight=EnumerateChildren
			Stream stream = await client.GetStreamAsync(WopiHostUrl + "/wopi/containers/TODO/children?access_token=todo");

			var serializer = new JsonSerializer();

			using (var sr = new StreamReader(stream))
			using (var jsonTextReader = new JsonTextReader(sr))
			{
				Folder folder = serializer.Deserialize<Folder>(jsonTextReader);
				return folder.Children;
			}
		}
	}
}
