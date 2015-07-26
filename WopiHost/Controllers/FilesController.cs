using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cobalt;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.ConfigurationModel;
using WopiDiscovery;
using WopiDiscovery.Enumerations;
using WopiHost.Attributes;
using WopiHost.Contracts;
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
            get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(Configuration.Get("WopiClientUrl"))); }
        }


        public FilesController(IWopiFileProvider fileProvider, IConfiguration configuration)
        {
            FileProvider = fileProvider;
            Configuration = configuration;
        }

        private EditSession GetEditSession(string fileId)
        {
            //TODO: implement new file - https://msdn.microsoft.com/en-us/library/hh695196(v=office.12).aspx
            //TODO: sessionid should be sessionid+fileid
            string sessionId = fileId;
            EditSession editSession = SessionManager.Current.GetSession(fileId);

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
            //using (var stream = editSession.File.ReadStream)
            //{
            //    return new FileResult(stream, "application/octet-stream");
            //}
            return new FileResult(editSession.File.ReadStream, "application/octet-stream");
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
            using (var stream = editSession.File.WriteStream)
            {
                await Context.Request.Body.CopyToAsync(stream);
            }
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
        public IActionResult PerformAction(string id, [FromQuery]string access_token)
        {
            var editSession = GetEditSession(id);
            string wopiOverrideHeader = Context.Request.Headers["X-WOPI-Override"];

            if (wopiOverrideHeader.Equals("COBALT"))
            {
                CobaltSession cobaltSession = ((CobaltSession)editSession); //TODO: refactoring needed
                var ms = new MemoryStream();
                Context.Request.Body.CopyTo(ms);
                var atomRequest = new AtomFromStream(ms); //TODO: Take a look at other AtomFrom*** classes
                RequestBatch requestBatch = new RequestBatch();

                object ctx;
                ProtocolVersion protocolVersion;

                requestBatch.DeserializeInputFromProtocol(atomRequest, out ctx, out protocolVersion);
                cobaltSession.ExecuteRequestBatch(requestBatch);

                foreach (Request request in requestBatch.Requests)
                {
                    if (request.GetType() == typeof(PutChangesRequest) && request.PartitionId == FilePartitionId.Content)
                    {
                        editSession.Save();
                    }
                }
                var response = requestBatch.SerializeOutputToProtocol(protocolVersion);

                Context.Response.Headers.Add("X-WOPI-CorellationID", new[] { Context.Request.Headers["X-WOPI-CorrelationID"] });
                Context.Response.Headers.Add("request-id", new[] { Context.Request.Headers["X-WOPI-CorrelationID"] });

                MemoryStream responseStream = new MemoryStream();
                response.CopyTo(responseStream);

                return new FileResult(responseStream, "application/octet-stream");
            }
            else if (wopiOverrideHeader.Equals("LOCK") || wopiOverrideHeader.Equals("UNLOCK") || wopiOverrideHeader.Equals("REFRESH_LOCK"))
            {
                //TODO: implement locking
                // https://msdn.microsoft.com/en-us/library/hh623363(v=office.12).aspx

                //TODO: Replace the else-if with separate methods (e.g. use actionselector?)
                // https://artokai.wordpress.com/2013/10/04/asp-net-mvc-header-based-routing/
                // https://artokai.wordpress.com/2013/10/
                // http://stackoverflow.com/questions/12344522/how-to-append-a-prefix-to-action-name-according-to-a-particular-route

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
