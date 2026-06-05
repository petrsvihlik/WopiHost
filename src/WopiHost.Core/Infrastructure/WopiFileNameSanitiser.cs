using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Cross-platform forbidden-character sanitisation shared by the file-name + container-name
/// negotiation paths. WOPI's spec is silent on which characters are forbidden — the upstream
/// provider's <see cref="IWopiWritableStorageProvider.CheckValidFileName"/> is the source of
/// truth — but every concrete provider in this repo rejects the same Windows-portable set, so a
/// central best-effort scrub lets the spec's "host should try to generate a different name based
/// on the requested name" branch land at a candidate that's almost always acceptable to the
/// provider on the second swing.
/// </summary>
internal static class WopiFileNameSanitiser
{
    /// <summary>
    /// Characters every shipped provider rejects in file/container names. Mirrors the
    /// Windows-invalid-character set <em>plus</em> space (trimmed off the result), so the
    /// sanitised stem is portable across the file-system + Azure-blob providers.
    /// </summary>
    public const string ForbiddenChars = "<>:\"/\\|?* ";

    /// <summary>
    /// Replaces every <see cref="ForbiddenChars"/> occurrence in <paramref name="input"/> with
    /// <c>_</c>, then trims leading/trailing whitespace. Returns <see langword="null"/> when the
    /// scrubbed result is empty or path-nav (<c>.</c>/<c>..</c>) so the caller can substitute its
    /// own fallback.
    /// </summary>
    public static string? ScrubOrNull(string input)
    {
        var scrubbed = ForbiddenChars.Aggregate(input, (cur, c) => cur.Replace(c, '_')).Trim();
        return string.IsNullOrWhiteSpace(scrubbed) || scrubbed is "." or ".." ? null : scrubbed;
    }

    /// <summary>
    /// Sanitises <paramref name="invalidName"/>'s stem with <see cref="ScrubOrNull"/>, preserving
    /// the original extension. When the scrubbed stem is unusable, substitutes
    /// <paramref name="fallbackStem"/>. Returns the rebuilt candidate only when it passes
    /// <see cref="IWopiWritableStorageProvider.CheckValidFileName"/>; otherwise returns
    /// <see langword="null"/> (caller decides whether to surface the failure or apply its own
    /// fallback shape).
    /// </summary>
    public static async Task<string?> TryBuildValidCandidateAsync(
        IWopiWritableStorageProvider writable,
        string invalidName,
        string fallbackStem,
        CancellationToken cancellationToken)
    {
        var ext = ExtractExtension(invalidName);
        var stem = ext.Length == 0 ? invalidName : invalidName[..^ext.Length];
        var sanitisedStem = ScrubOrNull(stem) ?? fallbackStem;
        var candidate = sanitisedStem + ext;
        return await writable.CheckValidFileName(candidate, cancellationToken).ConfigureAwait(false)
            ? candidate
            : null;
    }

    /// <summary>
    /// Extracts the extension (including the leading dot) from <paramref name="name"/>. Returns
    /// an empty string when the name has no extension (no dot, or leading dot only).
    /// </summary>
    public static string ExtractExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[dot..] : string.Empty;
    }
}
