using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Model binder for binding request body to a string.
/// </summary>
public sealed class FromStringBodyModelBinder : IModelBinder
{
    /// <inheritdoc/>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;

        try
        {
            if (!request.Body.CanSeek)
            {
                request.EnableBuffering();
            }
            string? body = null;
            if (request.ContentLength is not null && request.ContentLength > 0)
            {
                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body);
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            bindingContext.Result = ModelBindingResult.Success(body ?? string.Empty);
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, ex, bindingContext.ModelMetadata);
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}
