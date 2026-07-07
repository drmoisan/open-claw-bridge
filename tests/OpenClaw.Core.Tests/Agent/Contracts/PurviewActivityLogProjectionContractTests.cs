using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent.Contracts;

/// <summary>
/// Mocked-Graph/Purview contract tests for <see cref="PurviewActivityLogProjection"/> (issue
/// #124, AC4): confirms the projected <see cref="PurviewActivityLogRecord"/>'s populated field
/// set matches the pinned <c>directoryAudit</c>-style schema (spec.md decision 4) exactly — no
/// missing and no extra fields — for one representative record per
/// <see cref="CloudSyncActivityType"/> value plus one representative existing send/calendar
/// record. No live Graph/Purview endpoint is called; this test asserts the projection's output
/// shape against the pinned field set only.
/// </summary>
[TestClass]
public sealed class PurviewActivityLogProjectionContractTests
{
    /// <summary>
    /// The pinned Microsoft Graph <c>directoryAudit</c>-style field set (spec.md decision 4),
    /// verified against Microsoft Learn
    /// (<c>https://learn.microsoft.com/en-us/graph/api/resources/directoryaudit</c>).
    /// </summary>
    private static readonly IReadOnlySet<string> PinnedFieldSet = new HashSet<string>
    {
        nameof(PurviewActivityLogRecord.Id),
        nameof(PurviewActivityLogRecord.ActivityDateTime),
        nameof(PurviewActivityLogRecord.ActivityDisplayName),
        nameof(PurviewActivityLogRecord.Category),
        nameof(PurviewActivityLogRecord.CorrelationId),
        nameof(PurviewActivityLogRecord.OperationType),
        nameof(PurviewActivityLogRecord.Result),
        nameof(PurviewActivityLogRecord.ResultReason),
        nameof(PurviewActivityLogRecord.InitiatedBy),
        nameof(PurviewActivityLogRecord.TargetResources),
        nameof(PurviewActivityLogRecord.AdditionalDetails),
    };

    private static ActionAuditRecord NewCloudSyncRecord(string actionType) =>
        new(
            Mailbox: "owner@contoso.com",
            MessageId: "sub-contract-001",
            EventId: null,
            ActionType: actionType,
            ActingFlags: CloudSyncActingFlags.NotApplicable,
            CorrelationId: "55555555-5555-5555-5555-555555555555",
            ResultCode: CloudSyncActivityResultCode.Success,
            ErrorDetail: null,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero)
        );

    private static ActionAuditRecord NewSendRecord() =>
        new(
            Mailbox: "owner@contoso.com",
            MessageId: "msg-contract-001",
            EventId: "evt-1",
            ActionType: SentActionKey.ProposalReply,
            ActingFlags: "SendEnabled=True;CalendarWriteEnabled=False",
            CorrelationId: "66666666-6666-6666-6666-666666666666",
            ResultCode: ActionAuditResultCode.Sent,
            ErrorDetail: null,
            OriginalStartUtc: null,
            OriginalEndUtc: null,
            NewStartUtc: null,
            NewEndUtc: null,
            RecordedAtUtc: new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.Zero)
        );

    private static void AssertFieldSetMatchesPinnedSchema(PurviewActivityLogRecord projected)
    {
        var actualFieldSet = projected.GetType().GetProperties().Select(p => p.Name).ToHashSet();

        actualFieldSet
            .Should()
            .BeEquivalentTo(
                PinnedFieldSet,
                "the projection must populate exactly the pinned directoryAudit-style field set"
            );
    }

    [TestMethod]
    [DataRow(CloudSyncActivityType.SubscriptionCreated)]
    [DataRow(CloudSyncActivityType.SubscriptionRenewed)]
    [DataRow(CloudSyncActivityType.SubscriptionExpired)]
    [DataRow(CloudSyncActivityType.SubscriptionRemoved)]
    [DataRow(CloudSyncActivityType.WebhookReceived)]
    [DataRow(CloudSyncActivityType.WebhookRejected)]
    [DataRow(CloudSyncActivityType.DeltaReconciliationRun)]
    public void Project_EachCloudSyncActivityType_MatchesPinnedDirectoryAuditFieldSet(
        string actionType
    )
    {
        // Arrange
        var record = NewCloudSyncRecord(actionType);

        // Act
        var projected = PurviewActivityLogProjection.Project(record);

        // Assert
        AssertFieldSetMatchesPinnedSchema(projected);
    }

    [TestMethod]
    public void Project_ExistingSendActionType_MatchesPinnedDirectoryAuditFieldSet()
    {
        // Arrange
        var record = NewSendRecord();

        // Act
        var projected = PurviewActivityLogProjection.Project(record);

        // Assert
        AssertFieldSetMatchesPinnedSchema(projected);
    }
}
