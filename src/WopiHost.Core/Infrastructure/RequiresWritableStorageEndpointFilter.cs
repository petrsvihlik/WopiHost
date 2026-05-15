using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Short-circuits the request with <c>501 Not Implemented</c> when no
/// <see cref="IWopiWritableStorageProvider"/> is registered. Attached to endpoints that mutate
/// storage (PUT, DELETE, RENAME, …) so read-only hosts surface the unimplemented capability the
/// way the WOPI spec mandates.
/// </summary>
internal sealed class RequiresWritableStorageEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.RequestServices.GetService<IWopiWritableStorageProvider>() is null)
        {
            return new ValueTask<object?>(TypedResults.StatusCode(StatusCodes.Status501NotImplemented));
        }
        return next(context);
    }
}
