using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Configuration;
using WopiHost.Abstractions;
using WopiHost.Attributes;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Models;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	[ServiceFilter(typeof(WopiAuthorizationAttribute))]
	public class FilesController : Controller
	{
		private WopiDiscoverer _wopiDiscoverer;

		public IWopiFileProvider FileProvider { get; set; }
		public IConfiguration Configuration { get; set; }

		private WopiDiscoverer WopiDiscoverer
		{
			get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(Configuration.GetSection("WopiClientUrl").Value)); }
		}


		public FilesController(IWopiFileProvider fileProvider, IConfiguration configuration)
		{
			FileProvider = fileProvider;
			Configuration = configuration;
		}

		private EditSession GetEditSession(string fileId)
		{
			var sessionId = /*Context.Session.GetString("SessionID");
			if (string.IsNullOrEmpty(sessionId))
			{
				sessionId = Guid.NewGuid().ToString();
				Context.Session.SetString("SessionID", sessionId);
			}
			sessionId += "|" +*/ fileId;
			EditSession editSession = SessionManager.Current.GetSession(sessionId);

			if (editSession == null)
			{
				IWopiFile file = FileProvider.GetWopiFile(fileId);

				//TODO: remove hardcoded action 'Edit'
				if (WopiDiscoverer.RequiresCobalt(file.Extension, WopiActionEnum.Edit))
				{
					editSession = new CobaltSession(file, sessionId);
				}
				else
				{
					editSession = new FileSession(file, sessionId);
				}
				SessionManager.Current.AddSession(editSession);
			}

			return editSession;
		}

		/// <summary>
		/// Returns the metadata about a file specified by an identifier.
		/// Specification: https://msdn.microsoft.com/en-us/library/hh643136.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>
		/// </summary>
		/// <param name="id">File identifier.</param>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet(Constants.EntryRoute)]
		[Produces("application/json")]
		public CheckFileInfo GetCheckFileInfo(string id, [FromQuery]string access_token)
		{
			return GetEditSession(id)?.GetCheckFileInfo();
		}

		/// <summary>
		/// Returns contents of a file specified by an identifier.
		/// Specification: https://msdn.microsoft.com/en-us/library/hh657944.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
		/// </summary>
		/// <param name="id"></param>
		/// <param name="access_token"></param>
		/// <returns></returns>
		[HttpGet("{id}/contents")]
		[Produces("application/octet-stream")]
		public FileResult GetContents(string id, [FromQuery]string access_token)
		{
			var editSession = GetEditSession(id);
			return new FileResult(editSession.GetFileContent(), "application/octet-stream");
		}

		/// <summary>
		/// Updates a file specified by an identifier. (Only for non-cobalt files.)
		/// Specification: https://msdn.microsoft.com/en-us/library/hh657364.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
		/// </summary>
		/// <param name="id"></param>
		/// <param name="access_token"></param>
		/// <returns></returns>
		[HttpPut("{id}/contents")]
		[HttpPost("{id}/contents")]
		[Produces("application/octet-stream")]
		public async Task<IActionResult> PutContents(string id, [FromQuery]string access_token)
		{
			var editSession = GetEditSession(id);
			editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());
			return new HttpStatusCodeResult((int)HttpStatusCode.OK);
		}


		/// <summary>
		/// Changes the contents of the file in accordance with [MS-FSSHTTP] and performs other operations like locking.
		/// MS-FSSHTTP Specification: https://msdn.microsoft.com/en-us/library/dd943623.aspx
		/// Specification: https://msdn.microsoft.com/en-us/library/hh659581.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>
		/// </summary>
		/// <param name="id"></param>
		/// <param name="access_token"></param>
		[HttpPost("{id}")]
		[Produces("application/octet-stream", "text/html")]
		public async Task<IActionResult> PerformAction(string id, [FromQuery]string access_token)
		{
			var editSession = GetEditSession(id);
			string wopiOverrideHeader = HttpContext.Request.Headers["X-WOPI-Override"];

			if (wopiOverrideHeader.Equals("COBALT"))
			{
				var responseAction = editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());

				HttpContext.Response.Headers.Add("X-WOPI-CorellationID", HttpContext.Request.Headers["X-WOPI-CorrelationID"]);
				HttpContext.Response.Headers.Add("request-id", HttpContext.Request.Headers["X-WOPI-CorrelationID"]);

				return new FileResult(responseAction, "application/octet-stream");
			}
			else if (wopiOverrideHeader.Equals("LOCK") || wopiOverrideHeader.Equals("UNLOCK") || wopiOverrideHeader.Equals("REFRESH_LOCK"))
			{
				//TODO: implement locking (https://github.com/petrsvihlik/WopiHost/issues/4)
				//TODO: Replace the else-ifs with separate methods (https://github.com/petrsvihlik/WopiHost/issues/7)
				return new HttpStatusCodeResult((int)HttpStatusCode.OK);

			}
			else
			{
				// Unsupported action
				return new HttpStatusCodeResult((int)HttpStatusCode.NotImplemented);
			}
		}

	}
}
