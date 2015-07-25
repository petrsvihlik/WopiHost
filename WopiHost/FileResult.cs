using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

namespace WopiHost
{
    public class FileResult : ActionResult
    {
        public Stream Stream { get; }
        public string ContentType { get; private set; }

        public FileResult(Stream stream, string contentType)
        {
            Stream = stream;
            ContentType = contentType;
        }

        public async override Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = ContentType;
            Stream.Seek(0, SeekOrigin.Begin);
            await Stream.CopyToAsync(context.HttpContext.Response.Body);
        }
    }
}
