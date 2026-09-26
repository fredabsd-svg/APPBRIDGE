using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using AppBridge.ControlPlane.Authentication;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using AppBridge.ControlPlane.Middleware;
using AppBridge.ControlPlane.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

public sealed partial class TenantIsolationTests
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;
    private readonly string _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppBridgeTest")
        ?? throw new InvalidOperationException("Defina ConnectionStrings__AppBridgeTest para executar os testes de integração.");

    [Fact]
    public async Task Queries_are_scoped_to_the_bound_tenant_and_no_context_returns_no_rows()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await AddUserAsync(tenantA, "a-subject");
        await AddUserAsync(tenantB, "b-subject");

        await using var tenantContext = CreateContext(tenantA);
        Assert.Equal("a-subject", await tenantContext.UserAccounts.Select(x => x.ExternalSubject).SingleAsync(TestContext.Current.CancellationToken));

        await using var unboundContext = CreateContext((Guid?)null);
        Assert.Empty(await unboundContext.UserAccounts.ToListAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            unboundContext.UserAccounts.Add(new UserAccount());
            await unboundContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Save_rejects_a_client_supplied_different_tenant()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await AddTenantAsync(tenantA);
        await AddTenantAsync(tenantB);

        await using var db = CreateContext(tenantA);
        db.UserAccounts.Add(new UserAccount
        {
            TenantId = tenantB,
            ExternalSubject = "forged",
            Upn = "forged@example.test",
            AdObjectSid = "S-1-5-21-1",
            DisplayName = "Conta forjada",
            Email = "forged@example.test",
            Status = UserAccountStatus.Active
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Composite_foreign_key_rejects_a_host_pool_from_another_tenant()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await AddTenantAsync(tenantA);
        await AddTenantAsync(tenantB);
        var foreignPoolId = Guid.CreateVersion7();
        await using (var tenantBContext = CreateContext(tenantB))
        {
            tenantBContext.HostPools.Add(new HostPool
            {
                Id = foreignPoolId,
                TenantId = tenantB,
                Name = "pool-b",
                BackendType = SessionBackendType.Rds
            });
            await tenantBContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var tenantAContext = CreateContext(tenantA);
        tenantAContext.Applications.Add(new RemoteApplication
        {
            TenantId = tenantA,
            HostPoolId = foreignPoolId,
            DisplayName = "Aplicativo A",
            RemoteAppAlias = "AppA",
            LaunchMode = ApplicationLaunchMode.RemoteApp,
            Status = ApplicationStatus.Draft
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => tenantAContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_the_secured_operation()
    {
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        await using var db = CreateContext(tenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION appbridge_reject_test_audit() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'simulated audit write failure'; END;
            $$ LANGUAGE plpgsql;
            DROP TRIGGER IF EXISTS appbridge_reject_test_audit ON access_event;
            CREATE TRIGGER appbridge_reject_test_audit BEFORE INSERT ON access_event
            FOR EACH ROW EXECUTE FUNCTION appbridge_reject_test_audit();
            """, TestContext.Current.CancellationToken);

        try
        {
            var auditTenantContext = new TenantContext();
            auditTenantContext.Bind(tenantId);
            var writer = new AuditWriter(db, auditTenantContext, NullLogger<AuditWriter>.Instance);
            var operationEntityId = Guid.CreateVersion7();
            var auditEvent = new AccessEvent
            {
                EventType = AccessEventType.Authentication,
                Result = AccessEventResult.Success,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.CreateVersion7()
            };

            await Assert.ThrowsAsync<AuditUnavailableException>(() => writer.ExecuteAsync(auditEvent, _ =>
            {
                db.HostPools.Add(new HostPool
                {
                    Id = operationEntityId,
                    TenantId = tenantId,
                    Name = "rollback-check",
                    BackendType = SessionBackendType.Rds
                });
                return Task.FromResult(operationEntityId);
            }, TestContext.Current.CancellationToken));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS appbridge_reject_test_audit ON access_event; DROP FUNCTION IF EXISTS appbridge_reject_test_audit();", TestContext.Current.CancellationToken);
        }

        await using var verify = CreateContext(tenantId);
        Assert.Empty(await verify.HostPools.Where(x => x.Name == "rollback-check").ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_rows_are_append_only()
    {
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        await using var db = CreateContext(tenantId);
        var auditEvent = new AccessEvent
        {
            EventType = AccessEventType.Authentication,
            Result = AccessEventResult.Success,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.CreateVersion7()
        };
        db.AccessEvents.Add(auditEvent);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        auditEvent.FailureReason = "alterado";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Metering_does_not_count_prelaunch_records()
    {
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var accountId = Guid.CreateVersion7();
        var poolId = Guid.CreateVersion7();
        var applicationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateContext(tenantId))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = accountId,
                TenantId = tenantId,
                ExternalSubject = $"metering-{tenantId:N}",
                Upn = "user@example.test",
                AdObjectSid = $"S-1-5-21-{tenantId:N}",
                DisplayName = "Usuário",
                Email = "user@example.test",
                Status = UserAccountStatus.Active
            });
            db.HostPools.Add(new HostPool { Id = poolId, TenantId = tenantId, Name = "metering", BackendType = SessionBackendType.Rds });
            db.Applications.Add(new RemoteApplication
            {
                Id = applicationId,
                TenantId = tenantId,
                HostPoolId = poolId,
                DisplayName = "Aplicativo de metering",
                RemoteAppAlias = "MeteringApp",
                LaunchMode = ApplicationLaunchMode.RemoteApp,
                Status = ApplicationStatus.Published
            });
            db.Launches.AddRange(
                new Launch
                {
                    TenantId = tenantId,
                    UserAccountId = accountId,
                    ApplicationId = applicationId,
                    Purpose = LaunchPurpose.UserInitiated,
                    RequestedAt = now,
                    Outcome = LaunchOutcome.Granted,
                    SourceIp = "127.0.0.1",
                    WorkstationName = "test",
                    RdpExpiresAt = now.AddMinutes(1),
                    CorrelationId = Guid.CreateVersion7()
                },
                new Launch
                {
                    TenantId = tenantId,
                    UserAccountId = accountId,
                    ApplicationId = applicationId,
                    Purpose = LaunchPurpose.Prelaunch,
                    RequestedAt = now,
                    Outcome = LaunchOutcome.Granted,
                    SourceIp = "127.0.0.1",
                    WorkstationName = "test",
                    RdpExpiresAt = now.AddMinutes(1),
                    CorrelationId = Guid.CreateVersion7()
                });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var meteringDb = CreateContext(tenantId);
        var count = await new LaunchMeteringService(meteringDb).CountUserInitiatedAsync(
            applicationId, now.AddMinutes(-1), now.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Launch_is_authorized_once_and_replays_the_same_signed_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        var tenantContext = CreateTenantContext(world.TenantId);
        var backend = new FakeSessionBackend(new SessionBackendTarget(world.Host, false, null));
        var signer = new FakeRdpFileSigner();
        var service = CreateLaunchService(db, tenantContext, backend, signer);
        var request = new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a");
        var idempotencyKey = Guid.CreateVersion7();

        var first = await service.LaunchAsync(world.UserId, request, idempotencyKey, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var retry = await service.LaunchAsync(world.UserId, request, idempotencyKey, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var changedBody = await service.LaunchAsync(world.UserId,
            request with { WorkstationName = "workstation-b" }, idempotencyKey, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(StatusCodes.Status201Created, first.StatusCode);
        Assert.Equal(first.Response!.LaunchId, retry.Response!.LaunchId);
        Assert.Equal(first.Response.RdpFile, retry.Response.RdpFile);
        Assert.Equal("IDEMPOTENCY_CONFLICT", changedBody.ErrorCode);
        Assert.Single(await db.Launches.Where(launch => launch.TenantId == world.TenantId).ToListAsync(cancellationToken));
        Assert.Single(signer.Calls);
    }

    [Fact]
    public async Task Prelaunch_signing_failure_cancels_a_new_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        await using var db = CreateContext(world.TenantId);
        // Um backend que cria a sessão antes do cliente conectar a devolve como nova; o registry não a
        // reutiliza porque ela ainda não tem vínculo e está fora da janela (ADR-0021).
        var eagerSession = await db.Sessions.SingleAsync(row => row.Id == world.SessionId, cancellationToken);
        eagerSession.BackendSessionId = null;
        eagerSession.LastSeenAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync(cancellationToken);
        var tenantContext = CreateTenantContext(world.TenantId);
        var backend = new FakeSessionBackend(new SessionBackendTarget(world.Host, false, world.SessionId));
        var signer = new FakeRdpFileSigner(fail: true);
        var service = CreateLaunchService(db, tenantContext, backend, signer);
        var result = await service.LaunchAsync(
            world.UserId,
            new LaunchRequest(world.ApplicationId, LaunchPurpose.Prelaunch, "workstation-a"),
            Guid.CreateVersion7(),
            "127.0.0.1",
            Guid.CreateVersion7(),
            cancellationToken);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains(world.SessionId, backend.CancelledSessions);
        Assert.Contains(await db.Launches.ToListAsync(cancellationToken), launch => launch.Outcome == LaunchOutcome.ErrorSigning);
    }

    [Fact]
    public async Task Catalog_contains_only_applications_with_a_current_permission()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var authorized = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var unauthorizedApplicationId = Guid.CreateVersion7();
        await using (var seedDb = CreateContext(authorized.TenantId))
        {
            seedDb.Applications.Add(new RemoteApplication
            {
                Id = unauthorizedApplicationId,
                TenantId = authorized.TenantId,
                HostPoolId = authorized.Host.HostPoolId,
                DisplayName = "Aplicativo sem permissão",
                RemoteAppAlias = "AppSemPermissao",
                LaunchMode = ApplicationLaunchMode.RemoteApp,
                Status = ApplicationStatus.Published
            });
            await seedDb.SaveChangesAsync(cancellationToken);
        }

        await using var db = CreateContext(authorized.TenantId);
        var authorization = new AuthorizationService(db);
        var applications = await authorization.GetApplicationsAsync(authorized.UserId, cancellationToken);

        Assert.Contains(applications, application => application.Id == authorized.ApplicationId);
        Assert.DoesNotContain(applications, application => application.DisplayName == "Aplicativo sem permissão");
        Assert.NotNull(await authorization.GetAuthorizedApplicationAsync(
            authorized.UserId, authorized.ApplicationId, cancellationToken));
        Assert.Null(await authorization.GetAuthorizedApplicationAsync(
            authorized.UserId, unauthorizedApplicationId, cancellationToken));
    }

    [Fact]
    public async Task Revoked_permission_denies_the_next_launch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        var permission = await db.ApplicationPermissions.SingleAsync(cancellationToken);
        permission.EffectiveTo = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync(cancellationToken);
        var tenantContext = CreateTenantContext(world.TenantId);
        var service = CreateLaunchService(
            db,
            tenantContext,
            new FakeSessionBackend(new SessionBackendTarget(world.Host, false, null)),
            new FakeRdpFileSigner());

        var result = await service.LaunchAsync(
            world.UserId,
            new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-revoked"),
            Guid.CreateVersion7(),
            "127.0.0.1",
            Guid.CreateVersion7(),
            cancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("PERMISSION_REVOKED", result.ErrorCode);
        Assert.Contains(await db.Launches.ToListAsync(cancellationToken), launch => launch.Outcome == LaunchOutcome.DeniedPermission);
    }

    [Fact]
    public async Task Rdp_descriptor_applies_the_default_redirection_policy()
    {
        var descriptor = new RdpDescriptorBuilder().Build(
            new RemoteApplication
            {
                DisplayName = "Aplicativo de teste",
                RemoteAppAlias = "AppTeste",
                LaunchMode = ApplicationLaunchMode.RemoteApp
            },
            new SessionHost { Fqdn = "rdsh01.example.test" },
            new UserAccount { Upn = "user@example.test" },
            new RedirectionPolicy
            {
                AllowPrinter = true,
                AllowSmartcard = true,
                AllowClipboard = true,
                AllowAudioOut = true,
                AllowDrives = false,
                AllowSerialPorts = false,
                AllowAudioIn = false,
                AllowOtherUsb = false
            });
        var lines = Encoding.UTF8.GetString(descriptor).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("redirectprinters:i:1", lines);
        Assert.Contains("redirectsmartcards:i:1", lines);
        Assert.Contains("redirectclipboard:i:1", lines);
        Assert.Contains("audiomode:i:0", lines);
        Assert.Contains("drivestoredirect:s:", lines);
        Assert.Contains("redirectcomports:i:0", lines);
        Assert.Contains("audiocapturemode:i:0", lines);
        Assert.Contains("usbdevicestoredirect:s:", lines);
        Assert.Contains("username:s:user@example.test", lines);
    }

    [Fact]
    public void Rdp_descriptor_rejects_values_that_can_change_descriptor_syntax()
    {
        var application = new RemoteApplication
        {
            DisplayName = "Aplicativo de teste",
            RemoteAppAlias = "AppTeste",
            LaunchMode = ApplicationLaunchMode.RemoteApp
        };
        var policy = new RedirectionPolicy();

        Assert.Throws<RdpDescriptorException>(() => new RdpDescriptorBuilder().Build(
            application,
            new SessionHost { Fqdn = "rdsh01.example.test:3390" },
            new UserAccount { Upn = "user@example.test" },
            policy));

        Assert.Throws<RdpDescriptorException>(() => new RdpDescriptorBuilder().Build(
            application,
            new SessionHost { Fqdn = "rdsh01.example.test" },
            new UserAccount { Upn = "user@example.test:3389" },
            policy));
    }

    [Fact]
    public async Task Per_application_redirection_exception_requires_a_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        db.RedirectionPolicies.AddRange(
            new RedirectionPolicy { TenantId = world.TenantId, ApplicationId = null },
            new RedirectionPolicy { TenantId = world.TenantId, ApplicationId = world.ApplicationId, AllowDrives = true });
        await db.SaveChangesAsync(cancellationToken);
        var resolver = new RedirectionPolicyResolver(db);

        await Assert.ThrowsAsync<RdpPolicyException>(() => resolver.ResolveAsync(world.ApplicationId, cancellationToken));

        var exceptionPolicy = await db.RedirectionPolicies.SingleAsync(policy => policy.ApplicationId == world.ApplicationId, cancellationToken);
        exceptionPolicy.ExceptionReason = "Aprovado para importação controlada";
        await db.SaveChangesAsync(cancellationToken);
        Assert.True((await resolver.ResolveAsync(world.ApplicationId, cancellationToken)).AllowDrives);
    }

    [Fact]
    public async Task Rds_backend_resolves_a_live_host_for_the_authorized_application()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        var application = await db.Applications.SingleAsync(item => item.Id == world.ApplicationId, cancellationToken);
        var user = await db.UserAccounts.SingleAsync(item => item.Id == world.UserId, cancellationToken);
        var backend = CreateRdsBackend(db);

        var target = await backend.ResolveHostAsync(application, user, cancellationToken);

        Assert.NotNull(target);
        Assert.Equal(world.Host.Id, target.Host.Id);
        Assert.False(target.SessionReused);
    }

    [Fact]
    public async Task Authentication_exchange_emits_an_appbridge_token_and_audit_event_together()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        var externalTenantId = "entra-tenant-test";
        var externalSubject = "entra-user-test";
        var tenantContext = new TenantContext();
        tenantContext.Bind(tenantId);
        await using var db = CreateContext(tenantContext);
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            TenantId = tenantId,
            ExternalSubject = externalSubject,
            Upn = "user@example.test",
            AdObjectSid = "S-1-5-21-auth-test",
            DisplayName = "Usuário autenticado",
            Email = "user@example.test",
            Status = UserAccountStatus.Active
        });
        await db.SaveChangesAsync(cancellationToken);

        var service = CreateAuthenticationSessionService(
            db,
            tenantContext,
            externalTenantId,
            tenantId,
            externalSubject);
        var result = await service.ExchangeAsync(
            new AuthenticationSessionRequest("validated-identity-token", "PC-AUTH-01"),
            "127.0.0.1",
            Guid.CreateVersion7(),
            cancellationToken);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(userId, result.Response!.User.Id);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Response.AccessToken);
        Assert.Equal(userId.ToString("D"), token.Subject);
        Assert.Equal(tenantId.ToString("D"), token.Claims.Single(claim => claim.Type == "tenant_id").Value);
        var sessionId = Guid.Parse(token.Claims.Single(claim => claim.Type == "sid").Value);
        Assert.NotEmpty(result.Response.RefreshToken);
        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, cancellationToken);
        var savedRefreshToken = await db.AuthenticationRefreshTokens.SingleAsync(cancellationToken);
        Assert.Equal(sessionId, savedRefreshToken.SessionId);
        Assert.NotEqual(result.Response.RefreshToken, savedRefreshToken.TokenHash);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(result.Response.RefreshToken))), savedRefreshToken.TokenHash);
        var auditEvent = await db.AccessEvents.SingleAsync(cancellationToken);
        Assert.Equal(AccessEventResult.Success, auditEvent.Result);
        Assert.Equal(userId, auditEvent.UserAccountId);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_replay_revokes_the_whole_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        const string externalTenantId = "entra-tenant-refresh";
        const string externalSubject = "entra-user-refresh";
        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateContext(tenantContext);
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            TenantId = tenantId,
            ExternalSubject = externalSubject,
            Upn = "refresh@example.test",
            AdObjectSid = "S-1-5-21-auth-refresh",
            DisplayName = "Usuário de refresh",
            Email = "refresh@example.test",
            Status = UserAccountStatus.Active
        });
        await db.SaveChangesAsync(cancellationToken);
        var service = CreateAuthenticationSessionService(
            db, tenantContext, externalTenantId, tenantId, externalSubject);

        var login = await service.ExchangeAsync(
            new AuthenticationSessionRequest("validated-identity-token", "PC-REFRESH-01"),
            "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var firstRefreshToken = login.Response!.RefreshToken;
        var sessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(login.Response.AccessToken)
            .Claims.Single(claim => claim.Type == "sid").Value);

        var alteredLastCharacter = firstRefreshToken[^1] == 'A' ? 'B' : 'A';
        var invalidRefresh = await service.RefreshAsync(
            new AuthenticationRefreshRequest(firstRefreshToken[..^1] + alteredLastCharacter),
            "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        Assert.Equal("REFRESH_EXPIRED", invalidRefresh.ErrorCode);
        Assert.Null((await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, cancellationToken)).RevokedAt);

        var refreshed = await service.RefreshAsync(
            new AuthenticationRefreshRequest(firstRefreshToken), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(StatusCodes.Status200OK, refreshed.StatusCode);
        Assert.NotEqual(firstRefreshToken, refreshed.Response!.RefreshToken);
        Assert.Equal(sessionId.ToString("D"), new JwtSecurityTokenHandler()
            .ReadJwtToken(refreshed.Response.AccessToken).Claims.Single(claim => claim.Type == "sid").Value);
        var tokens = await db.AuthenticationRefreshTokens.OrderBy(token => token.CreatedAt).ToListAsync(cancellationToken);
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].ConsumedAt);
        Assert.Null(tokens[1].ConsumedAt);

        var replay = await service.RefreshAsync(
            new AuthenticationRefreshRequest(firstRefreshToken), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, replay.StatusCode);
        Assert.Equal("REFRESH_EXPIRED", replay.ErrorCode);
        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, cancellationToken);
        Assert.Equal("refresh_replay", session.RevocationReason);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal(AccessEventResult.Failure, (await db.AccessEvents
            .OrderByDescending(item => item.OccurredAt).FirstAsync(cancellationToken)).Result);
    }

    [Fact]
    public async Task Logout_revokes_session_and_audits_it_atomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        const string externalTenantId = "entra-tenant-logout";
        const string externalSubject = "entra-user-logout";
        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateContext(tenantContext);
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            TenantId = tenantId,
            ExternalSubject = externalSubject,
            Upn = "logout@example.test",
            AdObjectSid = "S-1-5-21-auth-logout",
            DisplayName = "Usuário de logout",
            Email = "logout@example.test",
            Status = UserAccountStatus.Active
        });
        await db.SaveChangesAsync(cancellationToken);
        var service = CreateAuthenticationSessionService(
            db, tenantContext, externalTenantId, tenantId, externalSubject);
        var login = await service.ExchangeAsync(
            new AuthenticationSessionRequest("validated-identity-token", "PC-LOGOUT-01"),
            "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var sessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(login.Response!.AccessToken)
            .Claims.Single(claim => claim.Type == "sid").Value);

        await service.LogoutAsync(userId, sessionId, "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, cancellationToken);
        Assert.Equal("logout", session.RevocationReason);
        Assert.NotNull(session.RevokedAt);
        var logoutEvent = await db.AccessEvents.SingleAsync(item => item.EventType == AccessEventType.Logout, cancellationToken);
        Assert.Equal(AccessEventResult.Success, logoutEvent.Result);
        Assert.Equal("PC-LOGOUT-01", logoutEvent.WorkstationName);

        var revokedContext = new TenantContext();
        await using var revokedDb = CreateContext(revokedContext);
        var request = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", tenantId.ToString("D")),
                new Claim("sub", userId.ToString("D")),
                new Claim("sid", sessionId.ToString("D"))
            ], "test"))
        };
        var nextCalled = false;
        await new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(request, revokedContext, revokedDb);
        Assert.Equal(StatusCodes.Status401Unauthorized, request.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Authentication_denial_is_audited_before_the_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        var externalTenantId = "entra-tenant-disabled";
        var externalSubject = "entra-user-disabled";
        var tenantContext = new TenantContext();
        tenantContext.Bind(tenantId);
        await using var db = CreateContext(tenantContext);
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            TenantId = tenantId,
            ExternalSubject = externalSubject,
            Upn = "disabled@example.test",
            AdObjectSid = "S-1-5-21-auth-disabled",
            DisplayName = "Conta desabilitada",
            Email = "disabled@example.test",
            Status = UserAccountStatus.Disabled
        });
        await db.SaveChangesAsync(cancellationToken);
        var service = CreateAuthenticationSessionService(
            db,
            tenantContext,
            externalTenantId,
            tenantId,
            externalSubject);

        var result = await service.ExchangeAsync(
            new AuthenticationSessionRequest("validated-identity-token", "PC-AUTH-02"),
            "127.0.0.1",
            Guid.CreateVersion7(),
            cancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("USER_DISABLED", result.ErrorCode);
        var auditEvent = await db.AccessEvents.SingleAsync(cancellationToken);
        Assert.Equal(AccessEventResult.Failure, auditEvent.Result);
        Assert.Equal("USER_DISABLED", auditEvent.FailureReason);
    }

    [Fact]
    public async Task Catalog_seed_adds_and_updates_applications_from_json()
    {
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var secondApplicationId = Guid.CreateVersion7();
        var seedPath = Path.Combine(Path.GetTempPath(), $"appbridge-catalog-{Guid.CreateVersion7():N}.json");
        var seed = JsonSerializer.Serialize(new
        {
            Tenants = new[]
            {
                new
                {
                    world.TenantId,
                    Applications = new object[]
                    {
                        new
                        {
                            Id = world.ApplicationId,
                            DisplayName = "Aplicativo atualizado",
                            Description = "Atualizado pelo seed",
                            RemoteAppAlias = "AppTeste",
                            HostPoolId = world.Host.HostPoolId,
                            LaunchMode = ApplicationLaunchMode.RemoteApp,
                            Status = ApplicationStatus.Published
                        },
                        new
                        {
                            Id = secondApplicationId,
                            DisplayName = "Segundo aplicativo",
                            Description = "Criado pelo seed",
                            RemoteAppAlias = "AppSegundo",
                            HostPoolId = world.Host.HostPoolId,
                            LaunchMode = ApplicationLaunchMode.RemoteApp,
                            Status = ApplicationStatus.Published
                        }
                    }
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        });

        await File.WriteAllTextAsync(seedPath, seed, TestContext.Current.CancellationToken);
        try
        {
            await RunCatalogSeedAsync(seedPath);
            await RunCatalogSeedAsync(seedPath);

            await using var db = CreateContext(world.TenantId);
            var applications = await db.Applications.OrderBy(app => app.DisplayName).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, applications.Count);
            Assert.Equal("Aplicativo atualizado", applications.Single(app => app.Id == world.ApplicationId).DisplayName);
            Assert.Equal("Segundo aplicativo", applications.Single(app => app.Id == secondApplicationId).DisplayName);
        }
        finally
        {
            File.Delete(seedPath);
        }
    }

    [Fact]
    public async Task Catalog_seed_accepts_missing_file_and_rejects_invalid_json()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.CreateVersion7():N}.json");
        await RunCatalogSeedAsync(missingPath);

        var invalidPath = Path.Combine(Path.GetTempPath(), $"appbridge-invalid-{Guid.CreateVersion7():N}.json");
        await File.WriteAllTextAsync(invalidPath, "{ invalid", TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => RunCatalogSeedAsync(invalidPath));
        }
        finally
        {
            File.Delete(invalidPath);
        }
    }

    [Fact]
    public async Task Catalog_seed_rejects_empty_or_unknown_tenant_and_incomplete_application()
    {
        var emptyTenantPath = await WriteCatalogSeedAsync(new { Tenants = new[] { new { TenantId = Guid.Empty } } });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => RunCatalogSeedAsync(emptyTenantPath));
        }
        finally
        {
            File.Delete(emptyTenantPath);
        }

        await EnsureSchemaAsync(TestContext.Current.CancellationToken);
        var unknownTenantPath = await WriteCatalogSeedAsync(new { Tenants = new[] { new { TenantId = Guid.CreateVersion7() } } });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => RunCatalogSeedAsync(unknownTenantPath));
        }
        finally
        {
            File.Delete(unknownTenantPath);
        }

        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var incompletePath = await WriteCatalogSeedAsync(new
        {
            Tenants = new[]
            {
                new
                {
                    world.TenantId,
                    Applications = new[] { new { Id = Guid.Empty, HostPoolId = Guid.Empty } }
                }
            }
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => RunCatalogSeedAsync(incompletePath));
        }
        finally
        {
            File.Delete(incompletePath);
        }
    }

    [Fact]
    public async Task Catalog_seed_rejects_a_tenant_that_is_not_provisioned()
    {
        await EnsureSchemaAsync(TestContext.Current.CancellationToken);
        var seedPath = await WriteCatalogSeedAsync(new
        {
            Tenants = new[] { new { TenantId = Guid.CreateVersion7() } }
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => RunCatalogSeedAsync(seedPath));
        }
        finally
        {
            File.Delete(seedPath);
        }
    }

    [Fact]
    public async Task Middleware_preserves_or_generates_correlation_ids_and_enforces_tenant_claims()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSchemaAsync(cancellationToken);
        var correlationId = Guid.CreateVersion7();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId.ToString("D");
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance)
            .InvokeAsync(context);
        Assert.Equal(correlationId.ToString("D"), context.TraceIdentifier);
        Assert.Equal(correlationId.ToString("D"), context.Response.Headers[CorrelationIdMiddleware.HeaderName]);

        var generatedContext = new DefaultHttpContext();
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance)
            .InvokeAsync(generatedContext);
        Assert.True(Guid.TryParse(generatedContext.TraceIdentifier, out _));

        var failedContext = new DefaultHttpContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CorrelationIdMiddleware(_ => throw new InvalidOperationException("test failure"), NullLogger<CorrelationIdMiddleware>.Instance)
                .InvokeAsync(failedContext));

        var invalidTenantContext = new DefaultHttpContext();
        invalidTenantContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", "invalid")], "test"));
        var nextCalled = false;
        var invalidTenant = new TenantContext();
        await using var invalidDb = CreateContext(invalidTenant);
        await new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(invalidTenantContext, invalidTenant, invalidDb);
        Assert.Equal(StatusCodes.Status401Unauthorized, invalidTenantContext.Response.StatusCode);
        Assert.False(nextCalled);

        var expectedTenantId = Guid.CreateVersion7();
        await AddTenantAsync(expectedTenantId);
        var expectedUserId = Guid.CreateVersion7();
        var expectedSessionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using (var seed = CreateContext(expectedTenantId))
        {
            seed.UserAccounts.Add(new UserAccount
            {
                Id = expectedUserId,
                TenantId = expectedTenantId,
                ExternalSubject = $"middleware-{expectedTenantId:N}",
                Upn = "middleware@example.test",
                AdObjectSid = $"S-1-5-21-{expectedTenantId:N}",
                DisplayName = "Middleware",
                Email = "middleware@example.test",
                Status = UserAccountStatus.Active
            });
            seed.AuthenticationSessions.Add(new AuthenticationSession
            {
                Id = expectedSessionId,
                TenantId = expectedTenantId,
                UserAccountId = expectedUserId,
                WorkstationName = "PC-MIDDLEWARE",
                LastUsedAt = now,
                ExpiresAt = now.AddDays(1),
                AbsoluteExpiresAt = now.AddDays(30)
            });
            await seed.SaveChangesAsync(cancellationToken);
        }

        var validTenantContext = new DefaultHttpContext();
        validTenantContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", expectedTenantId.ToString("D")),
            new Claim("sub", expectedUserId.ToString("D")),
            new Claim("sid", expectedSessionId.ToString("D"))
        ], "test"));
        var tenantContext = new TenantContext();
        await using var validDb = CreateContext(tenantContext);
        await new TenantContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(validTenantContext, tenantContext, validDb);
        Assert.Equal(expectedTenantId, tenantContext.TenantId);

        var anonymousContext = new DefaultHttpContext();
        var anonymousTenant = new TenantContext();
        await using var anonymousDb = CreateContext(anonymousTenant);
        await new TenantContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(anonymousContext, anonymousTenant, anonymousDb);
        Assert.Null(anonymousTenant.TenantId);

        var publicRefreshContext = new DefaultHttpContext();
        publicRefreshContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", expectedTenantId.ToString("D")),
            new Claim("sub", expectedUserId.ToString("D")),
            new Claim("sid", Guid.CreateVersion7().ToString("D"))
        ], "test"));
        publicRefreshContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()),
            "anonymous refresh"));
        var publicTenant = new TenantContext();
        await using var publicDb = CreateContext(publicTenant);
        var refreshReachedHandler = false;
        await new TenantContextMiddleware(_ =>
        {
            refreshReachedHandler = true;
            return Task.CompletedTask;
        }).InvokeAsync(publicRefreshContext, publicTenant, publicDb);
        Assert.True(refreshReachedHandler);
        Assert.Null(publicTenant.TenantId);
    }

    [Fact]
    public async Task Identity_validator_reports_missing_configuration_and_unavailable_authority()
    {
        var unconfigured = new IdentityTokenValidator(Options.Create(new IdentityProviderOptions()));
        await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
            unconfigured.ValidateAsync("invalid-token", TestContext.Current.CancellationToken));

        var unavailable = new IdentityTokenValidator(Options.Create(new IdentityProviderOptions
        {
            Authority = "https://127.0.0.1:1/v2.0",
            Audience = Guid.CreateVersion7().ToString("D")
        }));
        await Assert.ThrowsAsync<IdentityProviderUnavailableException>(() =>
            unavailable.ValidateAsync("invalid-token", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Authentication_rejects_empty_tokens_and_invalid_workstation_names()
    {
        await using var db = CreateContext((Guid?)null);
        var tenantContext = new TenantContext();
        var service = CreateAuthenticationSessionService(
            db,
            tenantContext,
            Guid.CreateVersion7().ToString("D"),
            Guid.CreateVersion7(),
            "test-subject");

        foreach (var request in new[]
        {
            new AuthenticationSessionRequest(string.Empty, "PC-TEST"),
            new AuthenticationSessionRequest("identity-token", string.Empty),
            new AuthenticationSessionRequest("identity-token", new string('A', 129)),
            new AuthenticationSessionRequest("identity-token", "PC-TEST\r\nworkstation")
        })
        {
            var result = await service.ExchangeAsync(
                request,
                "127.0.0.1",
                Guid.CreateVersion7(),
                TestContext.Current.CancellationToken);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Equal("MALFORMED_REQUEST", result.ErrorCode);
        }
    }

    [Fact]
    public async Task Refresh_rejects_a_locator_for_an_unprovisioned_tenant_without_audit_write()
    {
        var tenantId = Guid.CreateVersion7();
        var tenantContext = new TenantContext();
        await using var db = CreateContext(tenantContext);
        var service = CreateAuthenticationSessionService(
            db, tenantContext, "entra-tenant-unknown", tenantId, "entra-user-unknown");
        var secret = Convert.ToBase64String(Enumerable.Repeat((byte)0x52, 32).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var result = await service.RefreshAsync(
            new AuthenticationRefreshRequest($"v1.{tenantId:N}.{Guid.CreateVersion7():N}.{secret}"),
            "127.0.0.1", Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("REFRESH_EXPIRED", result.ErrorCode);
        Assert.Empty(await db.AccessEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rdp_signer_rejects_non_windows_execution()
    {
        if (!OperatingSystem.IsWindows())
        {
            var signer = new RdpSignExeSigner(Options.Create(new RdpSigningOptions()), NullLogger<RdpSignExeSigner>.Instance);
            await Assert.ThrowsAsync<RdpSigningException>(() =>
                signer.SignAsync([1, 2, 3], new string('A', 64), TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.True(OperatingSystem.IsWindows());
        }
    }

    [Fact]
    public async Task Rds_cancellation_returns_for_missing_session_and_rejects_invalid_session_data()
    {
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        await using var db = CreateContext(world.TenantId);
        var backend = CreateRdsBackend(db);

        await backend.CancelSessionAsync(Guid.CreateVersion7(), "test", TestContext.Current.CancellationToken);

        var session = await db.Sessions.SingleAsync(row => row.Id == world.SessionId, TestContext.Current.CancellationToken);
        var host = await db.SessionHosts.SingleAsync(row => row.Id == world.Host.Id, TestContext.Current.CancellationToken);
        host.Fqdn = "invalid host name";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<RdsSessionException>(() =>
            backend.CancelSessionAsync(session.Id, "test", TestContext.Current.CancellationToken));

        host.Fqdn = "rdsh01.example.test";
        session.BackendSessionId = "not-a-session-id";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<RdsSessionException>(() =>
            backend.CancelSessionAsync(session.Id, "test", TestContext.Current.CancellationToken));

        session.BackendSessionId = "27";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            await Assert.ThrowsAsync<RdsSessionException>(() =>
                backend.CancelSessionAsync(session.Id, "test", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Idempotency_pruner_removes_expired_response_bodies_and_preserves_tombstones()
    {
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var now = DateTimeOffset.UtcNow;
        await using (var db = CreateContext(world.TenantId))
        {
            db.LaunchIdempotencyRecords.AddRange(
                new LaunchIdempotencyRecord
                {
                    TenantId = world.TenantId,
                    IdempotencyKey = Guid.CreateVersion7(),
                    RequestHash = new string('A', 64),
                    ExpiresAt = now.AddSeconds(-1),
                    ResponseJson = "{}"
                },
                new LaunchIdempotencyRecord
                {
                    TenantId = world.TenantId,
                    IdempotencyKey = Guid.CreateVersion7(),
                    RequestHash = new string('B', 64),
                    ExpiresAt = now.AddMinutes(1),
                    ResponseJson = "{}"
                });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
        await using var provider = services.BuildServiceProvider();
        var pruner = new IdempotencyResponsePruner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<IdempotencyResponsePruner>.Instance);
        await pruner.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        await pruner.StopAsync(TestContext.Current.CancellationToken);

        await using var verification = CreateContext(world.TenantId);
        var records = await verification.LaunchIdempotencyRecords.OrderBy(record => record.RequestHash)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Null(records.Single(record => record.RequestHash.StartsWith('A')).ResponseJson);
        Assert.NotNull(records.Single(record => record.RequestHash.StartsWith('B')).ResponseJson);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task Authentication_session_pruner_removes_old_sessions_and_cascades_token_hashes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using (var db = CreateContext(tenantId))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = userId,
                TenantId = tenantId,
                ExternalSubject = $"pruner-{tenantId:N}",
                Upn = "pruner@example.test",
                AdObjectSid = $"S-1-5-21-{tenantId:N}",
                DisplayName = "Pruner",
                Email = "pruner@example.test",
                Status = UserAccountStatus.Active
            });
            db.AuthenticationSessions.Add(new AuthenticationSession
            {
                Id = sessionId,
                TenantId = tenantId,
                UserAccountId = userId,
                WorkstationName = "PC-PRUNER",
                LastUsedAt = now.AddDays(-61),
                ExpiresAt = now.AddDays(-31),
                AbsoluteExpiresAt = now.AddDays(-31)
            });
            db.AuthenticationRefreshTokens.Add(new AuthenticationRefreshToken
            {
                TenantId = tenantId,
                SessionId = sessionId,
                TokenHash = new string('A', 64),
                ExpiresAt = now.AddDays(-31)
            });
            db.IdentityTokenRedemptions.Add(new IdentityTokenRedemption
            {
                TenantId = tenantId,
                TokenHash = new string('B', 64),
                ExpiresAt = now.AddDays(-2)
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
        await using var provider = services.BuildServiceProvider();
        var pruner = new AuthenticationSessionPruner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuthenticationSessionPruner>.Instance);
        await pruner.StartAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        await pruner.StopAsync(cancellationToken);

        await using var verification = CreateContext(tenantId);
        Assert.Empty(await verification.AuthenticationSessions.ToListAsync(cancellationToken));
        Assert.Empty(await verification.AuthenticationRefreshTokens.ToListAsync(cancellationToken));
        Assert.Empty(await verification.IdentityTokenRedemptions.ToListAsync(cancellationToken));
    }

    private async Task AddTenantAsync(Guid tenantId)
    {
        await EnsureSchemaAsync(TestContext.Current.CancellationToken);
        await using var db = CreateContext(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = tenantId.ToString("N"),
            Status = TenantStatus.Active,
            AdDomain = "example.test",
            AdOuDn = "OU=AppBridge,DC=example,DC=test"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task RunCatalogSeedAsync(string seedPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CatalogSeed:Path"] = seedPath })
            .Build();
        var environment = new TestWebHostEnvironment(Path.GetDirectoryName(seedPath)!);
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
        await using var provider = services.BuildServiceProvider();

        var seedService = new CatalogSeedHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            environment,
            configuration,
            NullLogger<CatalogSeedHostedService>.Instance);
        await seedService.StartAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> WriteCatalogSeedAsync<T>(T seed)
    {
        var path = Path.Combine(Path.GetTempPath(), $"appbridge-seed-{Guid.CreateVersion7():N}.json");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(seed, options), TestContext.Current.CancellationToken);
        return path;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var db = CreateContext((Guid?)null);
            await db.Database.MigrateAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task AddUserAsync(Guid tenantId, string externalSubject)
    {
        await AddTenantAsync(tenantId);
        await using var db = CreateContext(tenantId);
        db.UserAccounts.Add(new UserAccount
        {
            TenantId = tenantId,
            ExternalSubject = externalSubject,
            Upn = $"{externalSubject}@example.test",
            AdObjectSid = $"S-1-5-21-{externalSubject}",
            DisplayName = externalSubject,
            Email = $"{externalSubject}@example.test",
            Status = UserAccountStatus.Active
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<LaunchWorld> SeedLaunchWorldAsync(bool grantPermission, bool addSession)
    {
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var userId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var poolId = Guid.CreateVersion7();
        var hostId = Guid.CreateVersion7();
        var applicationId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var host = new SessionHost
        {
            Id = hostId,
            TenantId = tenantId,
            HostPoolId = poolId,
            Fqdn = "rdsh01.example.test",
            Status = SessionHostStatus.Online,
            MaxSessions = 20
        };

        await using var db = CreateContext(tenantId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            TenantId = tenantId,
            ExternalSubject = $"launch-{tenantId:N}",
            Upn = "user@example.test",
            AdObjectSid = $"S-1-5-21-{tenantId:N}",
            DisplayName = "Usuário de teste",
            Email = "user@example.test",
            Status = UserAccountStatus.Active
        });
        db.Groups.Add(new AppGroup
        {
            Id = groupId,
            TenantId = tenantId,
            Name = "Financeiro",
            Source = GroupSource.Local
        });
        db.UserGroupMemberships.Add(new UserGroupMembership
        {
            TenantId = tenantId,
            UserAccountId = userId,
            GroupId = groupId,
            Source = GroupSource.Local
        });
        db.HostPools.Add(new HostPool
        {
            Id = poolId,
            TenantId = tenantId,
            Name = "pool-launch",
            BackendType = SessionBackendType.Rds
        });
        db.SessionHosts.Add(host);
        db.Applications.Add(new RemoteApplication
        {
            Id = applicationId,
            TenantId = tenantId,
            HostPoolId = poolId,
            DisplayName = "Aplicativo de teste",
            RemoteAppAlias = "AppTeste",
            LaunchMode = ApplicationLaunchMode.RemoteApp,
            Status = ApplicationStatus.Published
        });
        if (grantPermission)
        {
            db.ApplicationPermissions.Add(new ApplicationPermission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = groupId,
                EffectiveFrom = now.AddHours(-1),
                GrantedBy = userId
            });
        }

        db.SigningCertificates.Add(new SigningCertificate
        {
            TenantId = tenantId,
            Thumbprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.ToString("D")))),
            Subject = "CN=AppBridge test",
            Issuer = "CN=AppBridge test CA",
            ValidFrom = now.AddDays(-1),
            ValidTo = now.AddDays(30),
            Status = SigningCertificateStatus.Active,
            ActivatedAt = now.AddDays(-1)
        });
        if (addSession)
        {
            db.Sessions.Add(new RemoteSession
            {
                Id = sessionId,
                TenantId = tenantId,
                UserAccountId = userId,
                SessionHostId = hostId,
                BackendSessionId = "27",
                StartedAt = now.AddMinutes(-5),
                LastSeenAt = now,
                SourceIp = "127.0.0.1",
                WorkstationName = "workstation-a"
            });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new LaunchWorld(tenantId, userId, applicationId, host, addSession ? sessionId : Guid.Empty);
    }

    private LaunchService CreateLaunchService(
        AppDbContext db,
        TenantContext tenantContext,
        ISessionBackend backend,
        FakeRdpFileSigner signer)
        => new(
            db,
            tenantContext,
            new AuthorizationService(db),
            backend,
            new SessionRegistry(db, tenantContext, backend, Options.Create(new SessionRegistryOptions())),
            new RedirectionPolicyResolver(db),
            new RdpDescriptorBuilder(),
            signer,
            new AuditWriter(db, tenantContext, NullLogger<AuditWriter>.Instance),
            NullLogger<LaunchService>.Instance);

    private static RdsSessionBackend CreateRdsBackend(AppDbContext db)
        => new(
            db,
            Options.Create(new RdsSessionOptions()),
            Options.Create(new SessionRegistryOptions()),
            NullLogger<RdsSessionBackend>.Instance);

    private static TenantContext CreateTenantContext(Guid tenantId)
    {
        var context = new TenantContext();
        context.Bind(tenantId);
        return context;
    }

    private AppDbContext CreateContext(Guid? tenantId)
    {
        var tenantContext = new TenantContext();
        if (tenantId is not null)
        {
            tenantContext.Bind(tenantId.Value);
        }

        return CreateContext(tenantContext);
    }

    private AppDbContext CreateContext(TenantContext tenantContext)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options, tenantContext);

    private const string TestLauncherClientId = "3f1d2c4b-launcher";

    private AuthenticationSessionService CreateAuthenticationSessionService(
        AppDbContext db,
        TenantContext tenantContext,
        string externalTenantId,
        Guid tenantId,
        string externalSubject,
        string? fixedTokenId = null)
    {
        var options = Options.Create(new IdentityProviderOptions
        {
            ClientApplicationId = TestLauncherClientId,
            TenantMappings = new Dictionary<string, string> { [externalTenantId] = tenantId.ToString("D") }
        });
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new("tid", externalTenantId),
            new("oid", externalSubject),
            new("sub", externalSubject),
            new("name", "Usuário de teste"),
            new("scp", "openid access_as_user"),
            new("azp", TestLauncherClientId),
            new("iat", now.AddMinutes(-1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("exp", now.AddMinutes(59).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        if (fixedTokenId is not null)
        {
            claims.Add(new Claim("uti", fixedTokenId));
        }

        var identity = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var key = Convert.ToBase64String(Enumerable.Repeat((byte)0x4a, 32).ToArray());
        var tokenOptions = Options.Create(new ControlPlaneTokenOptions
        {
            Issuer = "https://appbridge.test",
            Audience = "appbridge-test-client",
            SigningKey = key,
            LifetimeMinutes = 30
        });

        return new AuthenticationSessionService(
            new FakeIdentityTokenValidator(identity),
            options,
            Options.Create(new RefreshTokenOptions()),
            db,
            tenantContext,
            new AuditWriter(db, tenantContext, NullLogger<AuditWriter>.Instance),
            new ControlPlaneTokenIssuer(tokenOptions),
            NullLogger<AuthenticationSessionService>.Instance);
    }

    private sealed record LaunchWorld(Guid TenantId, Guid UserId, Guid ApplicationId, SessionHost Host, Guid SessionId);

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AppBridge.ControlPlane.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeIdentityTokenValidator(ClaimsPrincipal principal) : IIdentityTokenValidator
    {
        // Cada chamada representa um token novo do Entra, com `uti` próprio, salvo quando o teste fixa um.
        public Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken cancellationToken)
        {
            if (principal.HasClaim(claim => claim.Type == "uti"))
            {
                return Task.FromResult(principal);
            }

            var identity = new ClaimsIdentity(principal.Claims, "test");
            identity.AddClaim(new Claim("uti", Guid.NewGuid().ToString("N")));
            return Task.FromResult(new ClaimsPrincipal(identity));
        }
    }

    private sealed class FakeSessionBackend(SessionBackendTarget target) : ISessionBackend
    {
        public List<Guid> CancelledSessions { get; } = [];

        public Task<SessionBackendTarget?> ResolveHostAsync(RemoteApplication application, UserAccount user, CancellationToken cancellationToken)
            => Task.FromResult<SessionBackendTarget?>(target);

        public Task CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
        {
            CancelledSessions.Add(sessionId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BackendSessionSnapshot>> ListActiveSessionsAsync(
            IReadOnlyCollection<SessionHost> hosts,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BackendSessionSnapshot>>([]);
    }

    private sealed class FakeRdpFileSigner(bool fail = false) : IRdpFileSigner
    {
        public List<string> Calls { get; } = [];

        public Task<byte[]> SignAsync(byte[] descriptor, string certificateThumbprint, CancellationToken cancellationToken)
        {
            Calls.Add(certificateThumbprint);
            if (fail)
            {
                throw new RdpSigningException("simulated signing failure");
            }

            return Task.FromResult(descriptor.Concat(Encoding.UTF8.GetBytes("signature:s:test\r\n")).ToArray());
        }
    }
}
