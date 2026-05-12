using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Action filter that short-circuits with <see cref="NotImplementedResult"/> (HTTP 501) when no
/// <see cref="IWopiWritableStorageProvider"/> is registered in the request scope.
/// </summary>
/// <remarks>
/// <para>
/// Apply to any controller action that mutates storage (PutFile, RenameFile, DeleteFile,
/// CreateChildContainer, PutRelativeFile, RenameContainer, DeleteContainer, …). The attribute
/// is the single source of truth for the "writable provider must be present" precondition,
/// replacing per-action <c>if (writableStorageProvider is null) return NotImplementedResult();</c>
/// boilerplate.
/// </para>
/// <para>
/// Controllers should still defensively narrow the nullable field at the top of the action
/// body (typically via <see cref="ArgumentNullException.ThrowIfNull(object?, string?)"/>) so
/// the C# nullable-flow analysis stops warning on downstream dereferences. The filter guarantees
/// the throw is never reached in practice; the narrowing call exists purely so the compiler
/// can treat the field as non-null in the rest of the method.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresWritableStorageAttribute : Attribute, IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HttpContext.RequestServices.GetService<IWopiWritableStorageProvider>() is null)
        {
            context.Result = new NotImplementedResult();
            return;
        }
        await next().ConfigureAwait(false);
    }
}
