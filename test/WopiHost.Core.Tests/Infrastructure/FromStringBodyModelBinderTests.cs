using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class FromStringBodyModelBinderTests
{
    [Fact]
    public async Task BindModelAsync_BindsRequestBodyToString()
    {
        var modelBinder = new FromStringBodyModelBinder();
        var context = new DefaultHttpContext();
        var requestBody = "Test request body";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        context.Request.ContentLength = requestBody.Length;

        var bindingContext = new DefaultModelBindingContext
        {
            ActionContext = new ActionContext()
            {
                HttpContext = context
            },
            ModelState = new ModelStateDictionary(),
            ModelName = "body",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string))
        };

        await modelBinder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(requestBody, bindingContext.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_HandlesEmptyRequestBody()
    {
        var modelBinder = new FromStringBodyModelBinder();
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;

        var bindingContext = new DefaultModelBindingContext
        {
            ActionContext = new ActionContext()
            {
                HttpContext = context
            },
            ModelState = new ModelStateDictionary(),
            ModelName = "body",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string))
        };

        await modelBinder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(string.Empty, bindingContext.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_NonSeekableBody_EnablesBuffering()
    {
        var modelBinder = new FromStringBodyModelBinder();
        var context = new DefaultHttpContext();
        var requestBody = "non-seekable";
        context.Request.Body = new NonSeekableStream(Encoding.UTF8.GetBytes(requestBody));
        context.Request.ContentLength = requestBody.Length;

        var bindingContext = new DefaultModelBindingContext
        {
            ActionContext = new ActionContext { HttpContext = context },
            ModelState = new ModelStateDictionary(),
            ModelName = "body",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)),
        };

        await modelBinder.BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(requestBody, bindingContext.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_BodyThrows_RecordsModelStateErrorAndFails()
    {
        var modelBinder = new FromStringBodyModelBinder();
        var context = new DefaultHttpContext();
        context.Request.Body = new ThrowingStream();
        context.Request.ContentLength = 1; // forces the binder to attempt a read

        var bindingContext = new DefaultModelBindingContext
        {
            ActionContext = new ActionContext { HttpContext = context },
            ModelState = new ModelStateDictionary(),
            ModelName = "body",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)),
        };

        await modelBinder.BindModelAsync(bindingContext);

        Assert.False(bindingContext.Result.IsModelSet);
        Assert.False(bindingContext.ModelState.IsValid);
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 1;
        public override long Position { get => 0; set => throw new IOException("boom"); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");
        public override long Seek(long offset, SeekOrigin origin) => throw new IOException("boom");
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
