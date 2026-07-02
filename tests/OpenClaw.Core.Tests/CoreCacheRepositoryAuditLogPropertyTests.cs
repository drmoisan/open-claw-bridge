using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Property-based round-trip test for the <see cref="IActionAuditLog"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #107, AC5): generated
/// <see cref="ActionAuditRecord"/> values — including non-UTC offsets on every
/// <see cref="DateTimeOffset"/> field and null/non-null combinations of all optional fields —
/// survive the persistence round-trip unchanged after UTC normalization to round-trip (O)
/// form. CsCheck prints the failing seed on a <c>Sample</c> failure, satisfying the
/// determinism print-seed requirement. Uses in-memory shared-cache SQLite; no temp files.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositoryAuditLogPropertyTests
{
    private static string NewConnectionString() =>
        $"Data Source=core-alp-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    // Non-empty, non-whitespace string component for the required fields.
    private static readonly Gen<string> GenComponent = Gen.Char[
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@._-+"
        ]
        .Array[1, 20]
        .Select(chars => new string(chars));

    // Optional string: null roughly half the time so both branches are exercised.
    private static readonly Gen<string?> GenOptionalString = Gen.Select(Gen.Bool, GenComponent)
        .Select(pair => pair.Item1 ? pair.Item2 : null);

    // Timestamp with an arbitrary whole-minute offset in the valid [-14h, +14h] range,
    // including non-UTC offsets, anchored away from DateTime.MinValue/MaxValue so the
    // offset arithmetic cannot overflow.
    private static readonly Gen<DateTimeOffset> GenTimestamp = Gen.Select(
            Gen.DateTime[new DateTime(2000, 1, 1), new DateTime(2100, 1, 1)],
            Gen.Int[-14 * 60, 14 * 60]
        )
        .Select(pair => new DateTimeOffset(
            DateTime.SpecifyKind(pair.Item1, DateTimeKind.Unspecified),
            TimeSpan.FromMinutes(pair.Item2)
        ));

    private static readonly Gen<DateTimeOffset?> GenOptionalTimestamp = Gen.Select(
            Gen.Bool,
            GenTimestamp
        )
        .Select(pair => pair.Item1 ? pair.Item2 : (DateTimeOffset?)null);

    private static readonly Gen<ActionAuditRecord> GenRecord = Gen.Select(
            Gen.Select(
                GenComponent,
                GenComponent,
                GenOptionalString,
                GenComponent,
                GenComponent,
                GenComponent,
                GenComponent,
                GenOptionalString
            ),
            Gen.Select(
                GenOptionalTimestamp,
                GenOptionalTimestamp,
                GenOptionalTimestamp,
                GenOptionalTimestamp,
                GenTimestamp
            )
        )
        .Select(pair => new ActionAuditRecord(
            Mailbox: pair.Item1.Item1,
            MessageId: pair.Item1.Item2,
            EventId: pair.Item1.Item3,
            ActionType: pair.Item1.Item4,
            ActingFlags: pair.Item1.Item5,
            CorrelationId: pair.Item1.Item6,
            ResultCode: pair.Item1.Item7,
            ErrorDetail: pair.Item1.Item8,
            OriginalStartUtc: pair.Item2.Item1,
            OriginalEndUtc: pair.Item2.Item2,
            NewStartUtc: pair.Item2.Item3,
            NewEndUtc: pair.Item2.Item4,
            RecordedAtUtc: pair.Item2.Item5
        ));

    [TestMethod]
    public async Task RecordAsync_GetByMessageIdAsync_RoundTripsAfterUtcNormalization()
    {
        await GenRecord.SampleAsync(
            async record =>
            {
                // Arrange: a fresh in-memory database per sample isolates the query.
                using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString());

                // Act
                await repo.RecordAsync(record, CancellationToken.None);
                var records = await repo.GetByMessageIdAsync(
                    record.MessageId,
                    CancellationToken.None
                );

                // Assert: the read-back record equals the input after every timestamp is
                // normalized to its UTC instant (round-trip O form preserves the ticks).
                var expected = record with
                {
                    OriginalStartUtc = record.OriginalStartUtc?.ToUniversalTime(),
                    OriginalEndUtc = record.OriginalEndUtc?.ToUniversalTime(),
                    NewStartUtc = record.NewStartUtc?.ToUniversalTime(),
                    NewEndUtc = record.NewEndUtc?.ToUniversalTime(),
                    RecordedAtUtc = record.RecordedAtUtc.ToUniversalTime(),
                };
                records.Should().ContainSingle();
                records[0].Should().Be(expected);
                records[0]
                    .RecordedAtUtc.Offset.Should()
                    .Be(TimeSpan.Zero, "stored timestamps are normalized to UTC");
            },
            iter: 100
        );
    }
}
