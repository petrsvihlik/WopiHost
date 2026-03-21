using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using FileResult = WopiHost.Core.Results.FileResult;

namespace WopiHost.Core.Tests.Results;

public class FileResultTests
{
    private static ActionContext CreateActionContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
    }

    private static async Task<byte[]> GetResponseBytes(ActionContext context)
    {
        context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var ms = new MemoryStream();
        await context.HttpContext.Response.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task ExecuteResultAsync_WithByteArray_WritesContentToResponseBody()
    {
        // Arrange
        var expected = "hello cobalt"u8.ToArray();
        var result = new FileResult(expected, MediaTypeNames.Application.Octet);
        var context = CreateActionContext();

        // Act
        await result.ExecuteResultAsync(context);

        // Assert
        var actual = await GetResponseBytes(context);
        Assert.Equal(expected, actual);
        Assert.Equal(MediaTypeNames.Application.Octet, context.HttpContext.Response.ContentType);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithStream_WritesContentToResponseBody()
    {
        // Arrange
        var expected = "stream content"u8.ToArray();
        var sourceStream = new MemoryStream(expected);
        var result = new FileResult(sourceStream, MediaTypeNames.Application.Octet);
        var context = CreateActionContext();

        // Act
        await result.ExecuteResultAsync(context);

        // Assert
        var actual = await GetResponseBytes(context);
        Assert.Equal(expected, actual);
        Assert.Equal(MediaTypeNames.Application.Octet, context.HttpContext.Response.ContentType);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithSeekableStream_ResetsPositionBeforeCopying()
    {
        // Arrange
        var expected = "seekable"u8.ToArray();
        var sourceStream = new MemoryStream(expected);
        sourceStream.Seek(sourceStream.Length, SeekOrigin.Begin); // move past content
        var result = new FileResult(sourceStream, MediaTypeNames.Application.Octet);
        var context = CreateActionContext();

        // Act
        await result.ExecuteResultAsync(context);

        // Assert
        var actual = await GetResponseBytes(context);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithEmptyByteArray_WritesEmptyResponse()
    {
        // Arrange
        var result = new FileResult([], MediaTypeNames.Application.Octet);
        var context = CreateActionContext();

        // Act
        await result.ExecuteResultAsync(context);

        // Assert
        var actual = await GetResponseBytes(context);
        Assert.Empty(actual);
    }
}
