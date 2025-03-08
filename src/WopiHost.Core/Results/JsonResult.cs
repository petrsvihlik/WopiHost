using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results;

/// <summary>
/// Represents an <see cref="JsonResult"/> that when executed will produce a JSON response.
/// </summary>
/// <typeparam name="T"></typeparam>
public class JsonResult<T> : JsonResult
    where T : class
{
    /// <summary>
    /// Gets the data object.
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonResult{T}"/> class.
    /// </summary>
    /// <param name="value">value</param>
    public JsonResult(T? value) : base(value)
    {
        Data = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonResult{T}"/> class.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="serializerSettings"></param>
    public JsonResult(T? value, object? serializerSettings) : base(value, serializerSettings)
    {
        Data = value;
    }
}
