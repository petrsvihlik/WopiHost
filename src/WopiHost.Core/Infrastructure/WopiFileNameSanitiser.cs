using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Provides the cross-platform forbidden-character sanitisation shared by the file-name and
/// container-name negotiation paths.
/// </summary>
/// <remarks>
/// The WOPI spec is silent on which characters are forbidden, and the provider's
/// <see cref="IWopiWritableStorageProvider.CheckValidFileName"/> is the source of truth. Every shipped
/// provider rejects the same Windows-portable set, so this best-effort scrub lets the spec's "host
/// should try to generate a different name based on the requested name" branch produce a candidate
/// the provider almost always accepts on the second attempt.
/// </remarks>
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
    /// <c>_</c>, then trims leading/trailing whitespace.
    /// </summary>
    /// <param name="input">The name to scrub.</param>
    /// <returns>The scrubbed name, or <see langword="null"/> when it is empty or a path-navigation
    /// segment (<c>.</c>/<c>..</c>), so the caller can substitute its own fallback.</returns>
    public static string? ScrubOrNull(string input)
    {
        var scrubbed = ForbiddenChars.Aggregate(input, (cur, c) => cur.Replace(c, '_')).Trim();
        return string.IsNullOrWhiteSpace(scrubbed) || scrubbed is "." or ".." ? null : scrubbed;
    }

    /// <summary>
    /// Sanitises <paramref name="invalidName"/>'s stem with <see cref="ScrubOrNull"/>, preserving
    /// the original extension. When the scrubbed stem is unusable, substitutes
    /// <paramref name="fallbackStem"/>.
    /// </summary>
    /// <param name="writable">The provider that validates the candidate.</param>
    /// <param name="invalidName">The name that failed validation.</param>
    /// <param name="fallbackStem">The stem to use when the scrubbed stem is unusable.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rebuilt candidate when it passes
    /// <see cref="IWopiWritableStorageProvider.CheckValidFileName"/>; otherwise <see langword="null"/>.</returns>
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
    /// Extracts the extension, including the leading dot, from <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The extension, or an empty string when the name has no extension (no dot, or a leading dot only).</returns>
    public static string ExtractExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[dot..] : string.Empty;
    }
}
