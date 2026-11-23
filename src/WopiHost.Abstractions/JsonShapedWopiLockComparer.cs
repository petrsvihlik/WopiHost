using System.Text.Json;

namespace WopiHost.Abstractions;

/// <summary>
/// Provides an opt-in <see cref="IWopiLockComparer"/> that treats two JSON-shaped lock ids as
/// equivalent when they share the same <c>S</c> (sequence) field.
/// </summary>
/// <remarks>
/// <para>
/// Absorbs the Office Online Server / Microsoft 365 for the Web quirk where lock ids round-trip
/// with properties added between requests, which would otherwise produce spurious <c>409</c>
/// lock-mismatch responses against a strict ordinal comparer. Comparison rules:
/// </para>
/// <list type="bullet">
///   <item><description>Ordinally equal inputs (including two <see langword="null"/> values)
///     are equal.</description></item>
///   <item><description>If both inputs parse as JSON objects with a string <c>S</c> property,
///     they are equal when those <c>S</c> values match ordinally.</description></item>
///   <item><description>Otherwise they are not equal, so non-JSON locks behave like
///     <see cref="OrdinalWopiLockComparer"/>.</description></item>
/// </list>
/// <para>
/// Modeled on cs3org/wopiserver's <c>wopilockstrictcheck=False</c> branch, which also keys off
/// the <c>S</c> field. Use only after observing the quirk in practice: distinct locks that
/// happen to share an <c>S</c> value are treated as equivalent, which can mask lost updates.
/// </para>
/// </remarks>
public sealed class JsonShapedWopiLockComparer : IWopiLockComparer
{
    /// <inheritdoc />
    public bool AreEqual(string? storedLockId, string? candidateLockId)
    {
        if (string.Equals(storedLockId, candidateLockId, StringComparison.Ordinal))
        {
            return true;
        }
        if (storedLockId is null || candidateLockId is null)
        {
            return false;
        }
        var storedS = TryReadJsonSField(storedLockId);
        var candidateS = TryReadJsonSField(candidateLockId);
        if (storedS is not null && candidateS is not null)
        {
            return string.Equals(storedS, candidateS, StringComparison.Ordinal);
        }
        return false;
    }

    private static string? TryReadJsonSField(string raw)
    {
        if (raw.Length == 0 || raw[0] != '{')
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("S", out var sField)
                && sField.ValueKind == JsonValueKind.String)
            {
                return sField.GetString();
            }
        }
        catch (JsonException)
        {
            // Looked like JSON but isn't — fall through to ordinal-fallback at the caller.
        }
        return null;
    }
}
