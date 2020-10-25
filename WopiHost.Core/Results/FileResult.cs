﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results
{
    /// <summary>
    /// Allows returning files as a result of a controller action.
    /// </summary>
    public class FileResult : ActionResult
    {
        protected Action<Stream> CopyStream { get; }
        protected byte[] Content { get; set; }
        protected Stream SourceStream { get; }
        protected string ContentType { get; }

        private FileResult(string contentType)
        {
            ContentType = contentType;
        }

        public FileResult(Stream sourceStream, string contentType) : this(contentType)
        {
            SourceStream = sourceStream;
        }

        public FileResult(byte[] content, string contentType) : this(contentType)
        {
            Content = content;
        }

        public FileResult(Action<Stream> copyStream, string contentType) : this(contentType)
        {
            CopyStream = copyStream;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = ContentType;
            var targetStream = response.Body;
            if (CopyStream is not null)
            {
                await Task.Factory.StartNew(() =>
                {
                    CopyStream(targetStream);
                });
            }
            else if (Content is not null)
            {
                await targetStream.WriteAsync(Content, 0, Content.Length);
            }
            else
            {
                using (SourceStream)
                {
                    if (SourceStream.CanSeek)
                    {
                        SourceStream.Seek(0, SeekOrigin.Begin);
                    }
                    await SourceStream.CopyToAsync(targetStream);
                }
            }
        }
    }
}
