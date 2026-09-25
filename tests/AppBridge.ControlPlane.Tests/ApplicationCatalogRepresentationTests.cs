using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Services;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

public sealed class ApplicationCatalogRepresentationTests
{
    [Fact]
    public void Etag_is_stable_for_the_same_authorized_content_and_changes_with_the_body()
    {
        var applicationId = Guid.CreateVersion7();
        var first = ApplicationCatalogRepresentation.Create(
        [CreateApplication(applicationId, "Domínio Contábil")]);
        var sameContent = ApplicationCatalogRepresentation.Create(
        [CreateApplication(applicationId, "Domínio Contábil")]);
        var changedContent = ApplicationCatalogRepresentation.Create(
        [CreateApplication(applicationId, "Domínio Contábil atualizado")]);

        Assert.Equal(first.EntityTag, sameContent.EntityTag);
        Assert.NotEqual(first.EntityTag, changedContent.EntityTag);
        Assert.StartsWith("\"cat-", first.EntityTag, StringComparison.Ordinal);
        Assert.EndsWith("\"", first.EntityTag, StringComparison.Ordinal);
        Assert.Contains("\"items\"", System.Text.Encoding.UTF8.GetString(first.Body), StringComparison.Ordinal);
        Assert.Contains("\"nextCursor\":null", System.Text.Encoding.UTF8.GetString(first.Body), StringComparison.Ordinal);
    }

    [Fact]
    public void If_none_match_uses_weak_comparison_and_accepts_lists_and_wildcards()
    {
        var representation = ApplicationCatalogRepresentation.Create(Array.Empty<RemoteApplication>());

        Assert.True(ApplicationCatalogRepresentation.MatchesIfNoneMatch("*", representation.EntityTag));
        Assert.True(ApplicationCatalogRepresentation.MatchesIfNoneMatch(
            $"W/{representation.EntityTag}", representation.EntityTag));
        Assert.True(ApplicationCatalogRepresentation.MatchesIfNoneMatch(
            new StringValues([$"\"other\", {representation.EntityTag}"]), representation.EntityTag));
        Assert.False(ApplicationCatalogRepresentation.MatchesIfNoneMatch(
            "\"other\"", representation.EntityTag));
        Assert.False(ApplicationCatalogRepresentation.MatchesIfNoneMatch(
            "invalid entity tag", representation.EntityTag));
    }

    private static RemoteApplication CreateApplication(Guid id, string displayName)
        => new()
        {
            Id = id,
            DisplayName = displayName,
            Description = "Escrita fiscal",
            LaunchMode = ApplicationLaunchMode.RemoteApp
        };
}
