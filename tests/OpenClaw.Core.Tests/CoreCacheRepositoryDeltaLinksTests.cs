using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for the <see cref="IDeltaLinkStore"/> implementation on
/// <see cref="OpenClaw.Core.CoreCacheRepository"/> (issue #117, AC-3): verbatim set/get
/// round-trip, null for an unknown mailbox, overwrite-on-second-set (upsert), and
/// schema-ensure idempotency. Uses in-memory shared-cache SQLite so no temp files are
/// created.
/// </summary>
[TestClass]
public sealed class CoreCacheRepositoryDeltaLinksTests
{
    private const string Mailbox = "paula@contoso.com";

    /// <summary>A realistic opaque delta link with query string and special characters.</summary>
    private const string DeltaLink =
        "https://graph.microsoft.com/v1.0/users/paula%40contoso.com/mailFolders('Inbox')/messages/delta"
        + "?$deltatoken=LztZwWjo5IivWBhyxw5rACKxf7mPm9Cts_pmZBZ%2Fro9c%3D&$select=id,subject&x=a+b%20c";

    private static readonly DateTimeOffset Now = new(2026, 7, 3, 9, 0, 0, TimeSpan.Zero);

    private static string NewConnectionString(string label) =>
        $"Data Source=core-dl-{label}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    [TestMethod]
    public async Task Set_then_get_round_trips_the_link_verbatim()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("roundtrip"));

        // Act
        await repo.SetDeltaLinkAsync(Mailbox, DeltaLink, Now, CancellationToken.None);
        var stored = await repo.GetDeltaLinkAsync(Mailbox, CancellationToken.None);

        // Assert
        stored
            .Should()
            .Be(
                DeltaLink,
                "the delta link is opaque and must round-trip verbatim, including "
                    + "percent-escapes and query-string characters"
            );
    }

    [TestMethod]
    public async Task Get_for_unknown_mailbox_returns_null()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("unknown"));

        // Act
        var stored = await repo.GetDeltaLinkAsync("nobody@contoso.com", CancellationToken.None);

        // Assert
        stored.Should().BeNull("no link has been stored for the mailbox");
    }

    [TestMethod]
    public async Task Second_set_overwrites_the_stored_link()
    {
        // Arrange
        using var repo = new OpenClaw.Core.CoreCacheRepository(NewConnectionString("overwrite"));
        await repo.SetDeltaLinkAsync(Mailbox, DeltaLink, Now, CancellationToken.None);
        const string NewerLink = DeltaLink + "&round=2";

        // Act
        await repo.SetDeltaLinkAsync(Mailbox, NewerLink, Now.AddHours(1), CancellationToken.None);
        var stored = await repo.GetDeltaLinkAsync(Mailbox, CancellationToken.None);

        // Assert
        stored.Should().Be(NewerLink, "the second set upserts over the first link");
    }

    [TestMethod]
    public async Task Schema_ensure_is_idempotent_across_two_repository_instances()
    {
        // Arrange: two repository instances against the same database each run their
        // own lazy schema-ensure; the second must not fail on the existing table.
        var connectionString = NewConnectionString("idempotent");
        using var first = new OpenClaw.Core.CoreCacheRepository(connectionString);
        await first.SetDeltaLinkAsync(Mailbox, DeltaLink, Now, CancellationToken.None);

        using var second = new OpenClaw.Core.CoreCacheRepository(connectionString);

        // Act
        var act = async () => await second.GetDeltaLinkAsync(Mailbox, CancellationToken.None);

        // Assert
        (await act.Should().NotThrowAsync("CREATE TABLE IF NOT EXISTS is idempotent"))
            .Which.Should()
            .Be(DeltaLink, "the second instance reads the link stored by the first");
    }
}
