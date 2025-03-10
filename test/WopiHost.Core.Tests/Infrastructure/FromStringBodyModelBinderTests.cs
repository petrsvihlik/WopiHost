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
        // Arrange
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

        // Act
        await modelBinder.BindModelAsync(bindingContext);

        // Assert
        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(requestBody, bindingContext.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_HandlesEmptyRequestBody()
    {
        // Arrange
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

        // Act
        await modelBinder.BindModelAsync(bindingContext);

        // Assert
        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(string.Empty, bindingContext.Result.Model);
    }
}
