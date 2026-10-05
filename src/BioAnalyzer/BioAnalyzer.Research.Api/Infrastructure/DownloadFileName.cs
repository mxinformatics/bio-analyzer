using System.Text.RegularExpressions;

namespace BioAnalyzer.Research.Api.Infrastructure;

/// <summary>
/// Phase C2: safe blob/download file name validation (no path traversal or arbitrary keys).
/// </summary>
public static partial class DownloadFileName
{
    /// <summary>
    /// Allow only simple blob keys: letters, digits, dot, underscore, hyphen.
    /// Rejects empty, path segments, traversal, and whitespace.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFileNameRegex();

    public static bool IsValid(string? fileName, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "fileName is required.";
            return false;
        }

        // Controllers may pass URL-encoded segments; decode once.
        var candidate = Uri.UnescapeDataString(fileName.Trim());

        if (candidate.Contains('/') || candidate.Contains('\\') || candidate.Contains("..", StringComparison.Ordinal))
        {
            error = "fileName must not contain path separators or '..'.";
            return false;
        }

        if (!SafeFileNameRegex().IsMatch(candidate))
        {
            error = "fileName contains invalid characters. Allowed: A-Z, a-z, 0-9, '.', '_', '-'.";
            return false;
        }

        // Defense in depth: no absolute Windows/Unix paths after validation.
        if (Path.IsPathRooted(candidate))
        {
            error = "fileName must be a relative blob key, not a rooted path.";
            return false;
        }

        normalized = candidate;
        return true;
    }
}
