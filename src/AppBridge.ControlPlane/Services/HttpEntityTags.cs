using System.Security.Cryptography;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AppBridge.ControlPlane.Services;

public static class HttpEntityTags
{
    public static string FromContent(string prefix, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var digest = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return $"\"{prefix}-{digest}\"";
    }

    public static bool MatchesIfNoneMatch(StringValues headerValues, string currentEntityTag)
    {
        var values = headerValues.ToArray().OfType<string>().ToArray();
        if (values.Length == 0
            || !EntityTagHeaderValue.TryParseList(values, out var candidates)
            || candidates is null)
        {
            return false;
        }

        var currentTag = EntityTagHeaderValue.Parse(currentEntityTag);
        return candidates.Any(candidate =>
            string.Equals(candidate.Tag.ToString(), "*", StringComparison.Ordinal)
            || candidate.Compare(currentTag, useStrongComparison: false));
    }
}
