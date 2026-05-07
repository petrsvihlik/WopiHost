using System.Text.Json;

namespace WopiHost.Abstractions;

/// <summary>
/// Opt-in <see cref="IWopiLockComparer"/> that treats two JSON-shaped lock ids as equivalent
/// when they share the same <c>S</c> (sequence) field. Designed to absorb the Office Online
/// Server / Microsoft 365 for the Web quirk where lock ids round-trip with additional
/// properties added between requests, which would otherwise produce spurious 409 lock-mismatch
/// responses against a strict ordinal comparer.
/// </summary>
/// <remarks>
/// <para>
/// Behavior:
/// </para>
/// <list type="bullet">
///   <item>If both inputs are byte-equal, returns <see langword="true"/> (fast path).</item>
///   <item>If both inputs parse as JSON objects with a string <c>S</c> property, returns
///     <see langword="true"/> when those <c>S</c> values match ordinally.</item>
///   <item>Otherwise falls back to ordinal equality (so non-JSON locks behave like the
///     default comparer).</item>
/// </list>
/// <para>
/// Inspired by cs3org/wopiserver's <c>wopilockstrictcheck=False</c> branch (which also keys
/// off the <c>S</c> field). Use only if you have observed the OOS / M365-for-the-Web quirk in
/// your environment — tolerance comes with its own correctness risk: distinct locks that
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
