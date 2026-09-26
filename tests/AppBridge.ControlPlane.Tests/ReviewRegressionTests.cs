using System.Text;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using AppBridge.ControlPlane.Middleware;
using AppBridge.ControlPlane.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

// Regressões dos achados RC-01, RC-07 e RC-08 da revisão de código da S018.
public sealed partial class TenantIsolationTests
{
    [Fact]
    public async Task Tenant_middleware_runs_through_the_real_middleware_pipeline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSchemaAsync(cancellationToken);
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped(provider => CreateContext(provider.GetRequiredService<TenantContext>()));
        await using var provider = services.BuildServiceProvider();

        // UseMiddleware resolve os parâmetros de InvokeAsync pelo contêiner, como no Program.cs.
        var app = new ApplicationBuilder(provider);
        app.UseMiddleware<TenantContextMiddleware>();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        await pipeline(httpContext);

        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Idempotency_key_of_another_user_does_not_return_the_signed_descriptor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var otherUserId = await AddIntruderAsync(world);
        await using var db = CreateContext(world.TenantId);
        var service = CreateLaunchService(db, CreateTenantContext(world.TenantId),
            new FakeSessionBackend(new SessionBackendTarget(world.Host, false, null)), new FakeRdpFileSigner());
        var request = new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a");
        var idempotencyKey = Guid.CreateVersion7();

        var owner = await service.LaunchAsync(world.UserId, request, idempotencyKey, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var intruder = await service.LaunchAsync(otherUserId, request, idempotencyKey, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(StatusCodes.Status201Created, owner.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, intruder.StatusCode);
        Assert.Equal("IDEMPOTENCY_CONFLICT", intruder.ErrorCode);
        Assert.Null(intruder.Response);
    }

    [Fact]
    public void Rdp_descriptor_rejects_a_trailing_line_break()
    {
        var application = new RemoteApplication { RemoteAppAlias = "AppTeste", LaunchMode = ApplicationLaunchMode.RemoteApp };
        var user = new UserAccount { Upn = "user@example.test" };
        var policy = new RedirectionPolicy();

        Assert.Throws<RdpDescriptorException>(() => new RdpDescriptorBuilder().Build(
            application, new SessionHost { Fqdn = "rdsh01.example.test\n" }, user, policy));
        Assert.Throws<RdpDescriptorException>(() => new RdpDescriptorBuilder().Build(
            application, new SessionHost { Fqdn = "rdsh01.example.test" }, new UserAccount { Upn = "user@example.test\n" }, policy));
        Assert.Throws<RdpDescriptorException>(() => new RdpDescriptorBuilder().Build(
            new RemoteApplication { RemoteAppAlias = "AppTeste\n" }, new SessionHost { Fqdn = "rdsh01.example.test" }, user, policy));

        var descriptor = Encoding.UTF8.GetString(new RdpDescriptorBuilder().Build(
            application, new SessionHost { Fqdn = "rdsh01.example.test" }, user, policy));
        Assert.Contains("full address:s:rdsh01.example.test:3389\r\n", descriptor);
    }

    private async Task<Guid> AddIntruderAsync(LaunchWorld world)
    {
        await using var db = CreateContext(world.TenantId);
        var membership = await db.UserGroupMemberships.SingleAsync(TestContext.Current.CancellationToken);
        var user = new UserAccount
        {
            TenantId = world.TenantId,
            ExternalSubject = $"intruder-{world.TenantId:N}",
            Upn = "intruder@example.test",
            AdObjectSid = $"S-1-5-21-intruder-{world.TenantId:N}",
            DisplayName = "Outro usuário",
            Email = "intruder@example.test",
            Status = UserAccountStatus.Active
        };
        db.UserAccounts.Add(user);
        db.UserGroupMemberships.Add(new UserGroupMembership
        {
            TenantId = world.TenantId,
            UserAccountId = user.Id,
            GroupId = membership.GroupId,
            Source = GroupSource.Local
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }
}
