using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Audit-emission tests for <see cref="GraphSubscriptionManager"/> (issue #124, AC2/AC4):
/// <c>CreateAsync</c>/<c>RenewAsync</c> emit exactly one audit record per success/failure
/// outcome, and <c>HandleLifecycleAsync</c> emits exactly one audit record for a failed
/// <c>reauthorizationRequired</c> renewal and one for a <c>removed</c> lifecycle event.
/// </summary>
[TestClass]
public sealed class GraphSubscriptionManagerAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    private const string ClientState = "deterministic-client-state";

    private const string SubscriptionResponse = """
        {
          "id": "sub-audit-1",
          "resource": "users/paula@contoso.com/mailFolders('Inbox')/messages",
          "changeType": "created,updated",
          "expirationDateTime": "2026-07-10T08:00:00Z"
        }
        """;

    private const string UnauthorizedBody = """
        { "error": { "code": "InvalidAuthenticationToken", "message": "Access token is empty." } }
        """;

    private static FakeSubscriptionStore StoreWithSub1() =>
        new()
        {
            Records =
            {
                ["sub-1"] = new GraphSubscriptionRecord(
                    "sub-1",
                    "users/paula@contoso.com/mailFolders('Inbox')/messages",
                    "paula@contoso.com",
                    ClientState,
                    Now.AddDays(2),
                    SubscriptionStatus.Active
                ),
            },
        };

    [TestMethod]
    public async Task CreateAsync_success_emits_exactly_one_subscription_created_audit_record()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(SubscriptionResponse),
                }
            )
        );
        var store = new FakeSubscriptionStore();
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.CreateAsync("req-create", CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.SubscriptionCreated);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        record.MessageId.Should().Be("sub-audit-1");
    }

    [TestMethod]
    public async Task CreateAsync_failure_emits_exactly_one_subscription_created_audit_record()
    {
        // Arrange: a 401 Graph response maps to a failure envelope.
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(UnauthorizedBody),
                }
            )
        );
        var store = new FakeSubscriptionStore();
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.CreateAsync("req-create-fail", CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.SubscriptionCreated);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
    }

    [TestMethod]
    public async Task RenewAsync_success_emits_exactly_one_subscription_renewed_audit_record()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        SubscriptionResponse.Replace("sub-audit-1", "sub-renew-1")
                    ),
                }
            )
        );
        var store = new FakeSubscriptionStore();
        store.Records["sub-renew-1"] = new GraphSubscriptionRecord(
            "sub-renew-1",
            "users/paula@contoso.com/mailFolders('Inbox')/messages",
            "paula@contoso.com",
            ClientState,
            Now.AddMinutes(20),
            SubscriptionStatus.Active
        );
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.RenewAsync("sub-renew-1", "req-renew", CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.SubscriptionRenewed);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Success);
        record.MessageId.Should().Be("sub-renew-1");
    }

    [TestMethod]
    public async Task RenewAsync_failure_emits_exactly_one_subscription_renewed_audit_record()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(UnauthorizedBody),
                }
            )
        );
        var store = new FakeSubscriptionStore();
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.RenewAsync("sub-renew-fail", "req-renew-fail", CancellationToken.None);

        // Assert
        var record = auditLog.Recorded.Should().ContainSingle().Which;
        record.ActionType.Should().Be(CloudSyncActivityType.SubscriptionRenewed);
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
    }

    [TestMethod]
    public async Task ReauthorizationRequired_failed_renewal_emits_exactly_one_subscription_expired_audit_record()
    {
        // Arrange: the PATCH renewal comes back 401, mapping to UNAUTHORIZED.
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(UnauthorizedBody),
                }
            )
        );
        var store = StoreWithSub1();
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.HandleLifecycleAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.ReauthorizationRequired),
            CancellationToken.None
        );

        // Assert: RenewAsync's own failure path also records a SubscriptionRenewed-failure
        // record; the lifecycle branch must additionally record exactly one
        // SubscriptionExpired record for the reauthorize-failed outcome.
        var record = auditLog
            .Recorded.Should()
            .ContainSingle(r => r.ActionType == CloudSyncActivityType.SubscriptionExpired)
            .Which;
        record.ResultCode.Should().Be(CloudSyncActivityResultCode.Failure);
        record.MessageId.Should().Be("sub-1");
    }

    [TestMethod]
    public async Task Removed_lifecycle_event_emits_exactly_one_subscription_removed_audit_record()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(SubscriptionResponse),
                }
            )
        );
        var store = StoreWithSub1();
        var auditLog = new FakeActionAuditLog();
        var manager = GraphSubscriptionManagerTests.Manager(
            handler,
            store,
            new FakeTimeProvider(Now),
            actionAuditLog: auditLog
        );

        // Act
        await manager.HandleLifecycleAsync(
            new LifecycleWorkItem("sub-1", LifecycleEvents.Removed),
            CancellationToken.None
        );

        // Assert: one SubscriptionRemoved record for the removal, plus one
        // SubscriptionCreated record from the recreate that follows.
        auditLog
            .Recorded.Should()
            .ContainSingle(r => r.ActionType == CloudSyncActivityType.SubscriptionRemoved)
            .Which.MessageId.Should()
            .Be("sub-1");
    }
}
