using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent.Contracts;

/// <summary>
/// Unit tests for the pure <see cref="PurviewActivityLogProjection.Project"/> mapping
/// (issue #124, AC3): totality over every <see cref="CloudSyncActivityType"/> value, the
/// null-optional-fields edge case, and continued support for the pre-existing send/calendar
/// <see cref="SentActionKey.ProposalReply"/> action type.
/// </summary>
[TestClass]
public sealed class PurviewActivityLogProjectionTests
{
    private static ActionAuditRecord NewRecord(
        string actionType,
        string resultCode,
        string? errorDetail = null,
        string? eventId = null
    ) =>
        new(
            Mailbox: "owner@contoso.com",
            MessageId: "sub-001",
            EventId: eventId,
            ActionType: actionType,
            ActingFlags: CloudSyncActingFlags.NotApplicable,
            CorrelationId: "44444444-4444-4444-4444-444444444444",
            ResultCode: resultCode,
            ErrorDetail: errorDetail,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero)
        );

    [TestMethod]
    [DataRow(CloudSyncActivityType.SubscriptionCreated)]
    [DataRow(CloudSyncActivityType.SubscriptionRenewed)]
    [DataRow(CloudSyncActivityType.SubscriptionExpired)]
    [DataRow(CloudSyncActivityType.SubscriptionRemoved)]
    [DataRow(CloudSyncActivityType.WebhookReceived)]
    [DataRow(CloudSyncActivityType.WebhookRejected)]
    [DataRow(CloudSyncActivityType.DeltaReconciliationRun)]
    public void Project_EachCloudSyncActivityType_MapsToNonEmptyDisplayNameAndOperationType(
        string actionType
    )
    {
        // Arrange
        var record = NewRecord(actionType, CloudSyncActivityResultCode.Success);

        // Act
        var projected = PurviewActivityLogProjection.Project(record);

        // Assert
        projected.ActivityDisplayName.Should().NotBeNullOrWhiteSpace();
        projected.OperationType.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void Project_NullEventIdAndErrorDetail_DoesNotThrow()
    {
        // Arrange: EventId and ErrorDetail are the two nullable optional fields on
        // ActionAuditRecord; a CloudSync success record naturally has both null.
        var record = NewRecord(
            CloudSyncActivityType.SubscriptionCreated,
            CloudSyncActivityResultCode.Success,
            errorDetail: null,
            eventId: null
        );

        // Act
        Action act = () => PurviewActivityLogProjection.Project(record);

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Project_ExistingSendActionType_StillMapsWithoutThrowing()
    {
        // Arrange: the pre-existing send/calendar action type must remain supported.
        var record = NewRecord(SentActionKey.ProposalReply, ActionAuditResultCode.Sent);

        // Act
        var projected = PurviewActivityLogProjection.Project(record);

        // Assert
        projected.ActivityDisplayName.Should().NotBeNullOrWhiteSpace();
        projected.OperationType.Should().NotBeNullOrWhiteSpace();
        projected.Result.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void Project_ErrorDetailPresent_MapsToResultReason()
    {
        // Arrange
        var record = NewRecord(
            CloudSyncActivityType.SubscriptionRenewed,
            CloudSyncActivityResultCode.Failure,
            errorDetail: "Graph returned 401 Unauthorized."
        );

        // Act
        var projected = PurviewActivityLogProjection.Project(record);

        // Assert
        projected.ResultReason.Should().Be("Graph returned 401 Unauthorized.");
        projected.Result.Should().Be("failure");
    }
}
