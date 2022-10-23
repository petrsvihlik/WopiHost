using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results;

/// <summary>
/// Allows returning files as a result of a controller action.
/// </summary>
public class FileResult : ActionResult
{
    /// <summary>
    /// An action that returns a stream with data to be written to the response body.
    /// </summary>
    private Action<Stream> CopyStream { get; }

    /// <summary>
    /// Byte array with the content to be written to the response body.
    /// </summary>
    private byte[] Content { get; set; }

    /// <summary>
    /// Source stream with data to be written to the response body.
    /// </summary>
    private Stream SourceStream { get; }

    /// <summary>
    /// Response content type header value.
    /// </summary>
    protected string ContentType { get; }

    private FileResult(string contentType)
    {
        ContentType = contentType;
    }

    /// <summary>
    /// Creates a new instance of <see cref="FileResult"/> that initializes the response body with a stream.
    /// </summary>
    /// <param name="sourceStream">Source stream with data to be written to the response body.</param>
    /// <param name="contentType">Response content type header value.</param>
    public FileResult(Stream sourceStream, string contentType) : this(contentType)
    {
        SourceStream = sourceStream;
    }

    /// <summary>
    /// Creates a new instance of <see cref="FileResult"/> that initializes the response body with a byte array.
    /// </summary>
    /// <param name="content">Byte array with the content to be written to the response body.</param>
    /// <param name="contentType">Response content type header value.</param>
    public FileResult(byte[] content, string contentType) : this(contentType)
    {
        Content = content;
    }

    /// <summary>
    /// Creates a new instance of <see cref="FileResult"/> that initializes the response body with a stream retrieved from the delegate.
    /// </summary>
    /// <param name="copyStream">An action that returns a stream with data to be written to the response body.</param>
    /// <param name="contentType">Response content type header value.</param>
    public FileResult(Action<Stream> copyStream, string contentType) : this(contentType)
    {
        CopyStream = copyStream;
    }

    /// <inheritdoc/>
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
            await targetStream.WriteAsync(Content.AsMemory(0, Content.Length));
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
