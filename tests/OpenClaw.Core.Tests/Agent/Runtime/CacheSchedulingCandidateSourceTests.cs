using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Unit tests for the widened candidate source (#103 AC-1): ordinary
/// (<c>item_kind = 'mail'</c>) messages are candidates alongside meeting messages,
/// while the lookback window, limit, and recency ordering are unchanged. Uses
/// in-memory shared-cache SQLite so no temp files are created. Rows are seeded
/// relative to real now with generous margins because the candidate source anchors
/// its lookback to <see cref="DateTimeOffset.UtcNow"/> (pre-existing behavior).
/// </summary>
[TestClass]
public sealed class CacheSchedulingCandidateSourceTests
{
    private static readonly BridgeStatusDto ReadyBridge = new(
        BridgeState.ready.ToString(),
        BridgeMode.enhanced.ToString(),
        true,
        false,
        null,
        null,
        null
    );

    private static MessageDto BuildMessage(
        string bridgeId,
        string itemKind,
        DateTimeOffset receivedUtc
    ) =>
        new(
            BridgeId: bridgeId,
            ItemKind: itemKind,
            Subject: "Candidate row",
            ReceivedUtc: receivedUtc,
            SentUtc: receivedUtc.AddMinutes(-5),
            Importance: 1,
            Sensitivity: 0,
            Unread: true,
            HasAttachments: false,
            MessageClass: itemKind == "meeting" ? "IPM.Schedule.Meeting.Request" : "IPM.Note",
            SenderName: "Sender",
            SenderEmail: "sender@contoso.com",
            ToJson: null,
            CcJson: null,
            BodyPreview: null,
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            SenderEmailResolved: null,
            FromEmailAddress: null,
            ConversationId: null,
            MeetingMessageType: null
        );

    private static async Task<CoreCacheRepository> CreateRepositoryAsync(
        params MessageDto[] messages
    )
    {
        var repository = new CoreCacheRepository(
            $"Data Source=candidate-source-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repository.InitializeAsync();
        await repository.UpsertMessagesAsync(messages, ReadyBridge, "req-1", DateTimeOffset.UtcNow);
        return repository;
    }

    private static CacheSchedulingCandidateSource CreateSource(
        CoreCacheRepository repository,
        int limit = 100,
        int lookbackHours = 48
    ) =>
        new(
            repository,
            Options.Create(
                new OpenClawOptions
                {
                    Polling = new PollingOptions { MessageLookbackHours = lookbackHours },
                    Defaults = new DefaultOptions { Limit = limit },
                }
            )
        );

    [TestMethod]
    public async Task GetCandidateMessageIds_returns_mail_alongside_meeting_within_window()
    {
        // Arrange: one meeting row and one ordinary mail row inside the lookback window.
        var now = DateTimeOffset.UtcNow;
        using var repository = await CreateRepositoryAsync(
            BuildMessage("meeting-in-window", "meeting", now.AddHours(-1)),
            BuildMessage("mail-in-window", "mail", now.AddHours(-2)),
            BuildMessage("meeting-stale", "meeting", now.AddHours(-100))
        );
        var source = CreateSource(repository);

        // Act
        var ids = await source.GetCandidateMessageIdsAsync(CancellationToken.None);

        // Assert: ordinary mail is a candidate alongside meeting traffic.
        ids.Should()
            .BeEquivalentTo(
                ["meeting-in-window", "mail-in-window"],
                "the widened candidate set is meeting plus ordinary mail within the window"
            );
    }

    [TestMethod]
    public async Task GetCandidateMessageIds_excludes_rows_older_than_the_lookback_window()
    {
        // Arrange: one in-window meeting row and one stale meeting row (lookback 48h).
        var now = DateTimeOffset.UtcNow;
        using var repository = await CreateRepositoryAsync(
            BuildMessage("meeting-in-window", "meeting", now.AddHours(-1)),
            BuildMessage("meeting-stale", "meeting", now.AddHours(-100))
        );
        var source = CreateSource(repository);

        // Act
        var ids = await source.GetCandidateMessageIdsAsync(CancellationToken.None);

        // Assert
        ids.Should().BeEquivalentTo(["meeting-in-window"], "the lookback window is unchanged");
    }

    [TestMethod]
    public async Task GetCandidateMessageIds_caps_the_result_at_the_configured_limit()
    {
        // Arrange: three in-window meeting rows with a limit of two.
        var now = DateTimeOffset.UtcNow;
        using var repository = await CreateRepositoryAsync(
            BuildMessage("meeting-newest", "meeting", now.AddHours(-1)),
            BuildMessage("meeting-middle", "meeting", now.AddHours(-2)),
            BuildMessage("meeting-oldest", "meeting", now.AddHours(-3))
        );
        var source = CreateSource(repository, limit: 2);

        // Act
        var ids = await source.GetCandidateMessageIdsAsync(CancellationToken.None);

        // Assert: the two most recent rows survive the cap.
        ids.Should()
            .Equal(
                ["meeting-newest", "meeting-middle"],
                "Defaults.Limit caps the recency-ordered result"
            );
    }

    [TestMethod]
    public async Task GetCandidateMessageIds_preserves_recency_ordering_across_kinds()
    {
        // Arrange: interleaved meeting and mail rows, all inside the window.
        var now = DateTimeOffset.UtcNow;
        using var repository = await CreateRepositoryAsync(
            BuildMessage("meeting-1h", "meeting", now.AddHours(-1)),
            BuildMessage("mail-2h", "mail", now.AddHours(-2)),
            BuildMessage("meeting-3h", "meeting", now.AddHours(-3)),
            BuildMessage("mail-4h", "mail", now.AddHours(-4))
        );
        var source = CreateSource(repository);

        // Act
        var ids = await source.GetCandidateMessageIdsAsync(CancellationToken.None);

        // Assert: newest-first ordering interleaves both kinds.
        ids.Should()
            .Equal(
                ["meeting-1h", "mail-2h", "meeting-3h", "mail-4h"],
                "recency ordering is preserved across meeting and mail rows"
            );
    }
}
