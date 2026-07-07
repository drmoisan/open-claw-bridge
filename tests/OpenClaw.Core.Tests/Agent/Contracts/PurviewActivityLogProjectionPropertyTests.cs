using System;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent.Contracts;

/// <summary>
/// Property-based tests for the pure <see cref="PurviewActivityLogProjection.Project"/> mapping
/// (issue #124, T1 property-test-density obligation per <c>.claude/rules/csharp.md</c> and
/// <c>.claude/rules/quality-tiers.md</c>). Generates arbitrary well-formed
/// <see cref="ActionAuditRecord"/> instances — including action/result-code strings outside the
/// known constant sets, to exercise the default fallback branches — and asserts the projection
/// never throws and always returns non-empty <c>Id</c>/<c>CorrelationId</c> and a populated
/// <c>ActivityDateTime</c>. CsCheck prints the failing seed on a <c>Sample</c> failure.
/// </summary>
[TestClass]
public sealed class PurviewActivityLogProjectionPropertyTests
{
    // Non-empty, non-whitespace strings for required ActionAuditRecord string fields.
    private static readonly Gen<string> GenNonEmptyString = Gen.Char[
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_."
        ]
        .Array[1, 24]
        .Select(chars => new string(chars));

    private static readonly Gen<string?> GenNull = Gen.Int[0, 0].Select(_ => (string?)null);

    private static readonly Gen<string?> GenOptionalString = Gen.OneOf(
        GenNull,
        GenNonEmptyString.Select(s => (string?)s)
    );

    private static readonly Gen<DateTimeOffset> GenRecordedAt = Gen.Int[0, 3650]
        .Select(days => new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days));

    // Required identity/type strings, generated as a group of four.
    private static readonly Gen<(
        string Mailbox,
        string MessageId,
        string ActionType,
        string ActingFlags
    )> GenIdentity = Gen.Select(
        GenNonEmptyString,
        GenNonEmptyString,
        GenNonEmptyString,
        GenNonEmptyString,
        (mailbox, messageId, actionType, actingFlags) =>
            (mailbox, messageId, actionType, actingFlags)
    );

    // Correlation/result strings plus the two nullable optional fields and the timestamp.
    private static readonly Gen<(
        string CorrelationId,
        string ResultCode,
        string? EventId,
        string? ErrorDetail,
        DateTimeOffset RecordedAt
    )> GenOutcome = Gen.Select(
        GenNonEmptyString,
        GenNonEmptyString,
        GenOptionalString,
        GenOptionalString,
        GenRecordedAt,
        (correlationId, resultCode, eventId, errorDetail, recordedAt) =>
            (correlationId, resultCode, eventId, errorDetail, recordedAt)
    );

    private static readonly Gen<ActionAuditRecord> GenRecord = Gen.Select(
        GenIdentity,
        GenOutcome,
        (identity, outcome) =>
            new ActionAuditRecord(
                Mailbox: identity.Mailbox,
                MessageId: identity.MessageId,
                EventId: outcome.EventId,
                ActionType: identity.ActionType,
                ActingFlags: identity.ActingFlags,
                CorrelationId: outcome.CorrelationId,
                ResultCode: outcome.ResultCode,
                ErrorDetail: outcome.ErrorDetail,
                OriginalStartUtc: null,
                OriginalEndUtc: null,
                NewStartUtc: null,
                NewEndUtc: null,
                RecordedAtUtc: outcome.RecordedAt
            )
    );

    [TestMethod]
    public void Project_AnyValidRecord_NeverThrows()
    {
        GenRecord.Sample(
            record =>
            {
                Action act = () => PurviewActivityLogProjection.Project(record);

                act.Should().NotThrow();
            },
            iter: 1000
        );
    }

    [TestMethod]
    public void Project_AnyValidRecord_ReturnsNonEmptyIdCorrelationIdAndActivityDateTime()
    {
        GenRecord.Sample(
            record =>
            {
                var projected = PurviewActivityLogProjection.Project(record);

                projected.Id.Should().NotBeNullOrWhiteSpace();
                projected.CorrelationId.Should().NotBeNullOrWhiteSpace();
                projected.ActivityDateTime.Should().Be(record.RecordedAtUtc);
            },
            iter: 1000
        );
    }
}
