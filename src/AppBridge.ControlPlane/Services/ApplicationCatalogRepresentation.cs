using System.Text.Json;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;

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
        return new ApplicationCatalogPayload(body, HttpEntityTags.FromContent("cat", body));
    }
}
