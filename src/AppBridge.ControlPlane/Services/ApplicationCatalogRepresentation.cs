using System.Security.Cryptography;
using System.Text.Json;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AppBridge.ControlPlane.Services;

public sealed record ApplicationCatalogItem(
    Guid Id,
    string DisplayName,
    string Description,
    string IconUrl,
    string LaunchMode,
    string ProtocolUri,
    bool Available);

public sealed record ApplicationCatalogDocument(
    IReadOnlyList<ApplicationCatalogItem> Items,
    string? NextCursor);

public sealed record ApplicationCatalogPayload(byte[] Body, string EntityTag);

public static class ApplicationCatalogRepresentation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ApplicationCatalogPayload Create(IReadOnlyList<RemoteApplication> applications)
    {
        var document = new ApplicationCatalogDocument(
            applications.Select(application => new ApplicationCatalogItem(
                application.Id,
                application.DisplayName,
                application.Description,
                $"/v1/applications/{application.Id:D}/icon",
                application.LaunchMode == ApplicationLaunchMode.RemoteApp ? "remote_app" : "confined_desktop",
                $"appbridge://launch/{application.Id:D}",
                Available: true)).ToArray(),
            NextCursor: null);
        var body = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        var digest = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        return new ApplicationCatalogPayload(body, $"\"cat-{digest}\"");
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
