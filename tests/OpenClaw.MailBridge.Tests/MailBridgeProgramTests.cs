using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;
using ClientProgram = OpenClaw.MailBridge.Client.Program;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Unit tests for the internal entry-point logic of the MailBridge client program.
/// All tests inject a <c>send</c> delegate so that no named pipe, filesystem access,
/// or external process is required.
/// </summary>
/// <remarks>
/// Scenarios already covered in <see cref="MailBridgeClientTests"/>:
/// - <c>ResolvePipeName</c> with pipe_name override.
/// - <c>RunAsync</c> returning exit code 5 for <see cref="BridgeErrorCodes.InvalidRequest"/>.
/// - <c>Send</c> round-trips via a real named pipe (message mode, empty response, malformed JSON).
/// Those scenarios are not duplicated here.
/// </remarks>
[TestClass]
public partial class MailBridgeProgramTests
{
    // ─── Parse ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an empty argument list causes <c>Parse</c> to return null, signaling
    /// that no command was supplied.
    /// </summary>
    [TestMethod]
    public void Parse_WhenArgsIsEmpty_ReturnsNull()
    {
        // Act
        var result = ClientProgram.Parse([]);

        // Assert: null causes RunAsync to exit with code 5
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a single-element argument list yields the command with an empty
    /// options dictionary.
    /// </summary>
    [TestMethod]
    public void Parse_WhenArgHasSingleCommand_ReturnsCommandWithEmptyOptions()
    {
        // Act
        var result = ClientProgram.Parse(["status"]);

        // Assert
        result.Should().NotBeNull();
        result!.Value.command.Should().Be("status");
        result.Value.options.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that key–value pairs prefixed with <c>--</c> are parsed into the options
    /// dictionary and that hyphens in key names are normalized to underscores.
    /// </summary>
    [TestMethod]
    public void Parse_WhenArgHasDashPrefixedKeyValuePairs_NormalizesHyphensToUnderscores()
    {
        // Arrange: "--pipe-name" should become "pipe_name" in the dictionary
        string[] args =
        [
            "list-messages",
            "--pipe-name",
            "my-pipe",
            "--since",
            "2026-01-01T00:00:00Z",
        ];

        // Act
        var result = ClientProgram.Parse(args);

        // Assert
        result.Should().NotBeNull();
        result!.Value.command.Should().Be("list-messages");
        result.Value.options.Should().ContainKey("pipe_name").WhoseValue.Should().Be("my-pipe");
        result
            .Value.options.Should()
            .ContainKey("since")
            .WhoseValue.Should()
            .Be("2026-01-01T00:00:00Z");
    }

    /// <summary>
    /// Verifies that tokens not prefixed with <c>--</c> are silently ignored so that
    /// positional-looking arguments do not corrupt the options dictionary.
    /// </summary>
    [TestMethod]
    public void Parse_WhenArgHasNonDashPrefixedTokens_IgnoresThem()
    {
        // Arrange: "extra" has no "--" prefix; the parser should skip it
        string[] args = ["status", "extra", "value"];

        // Act
        var result = ClientProgram.Parse(args);

        // Assert
        result.Should().NotBeNull();
        result!.Value.options.Should().NotContainKey("extra");
    }

    // ─── Build ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the <c>status</c> command produces a <see cref="BridgeMethods.GetStatus"/>
    /// request with no params and a non-empty GUID id.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsStatus_ReturnsGetStatusRequest()
    {
        // Act
        var req = ClientProgram.Build("status", []);

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.GetStatus);
        req.Params.Should().BeNull();
        Guid.TryParse(req.Id, out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <c>list-messages</c> with all required options builds the correct request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsListMessages_WithRequiredOptions_ReturnsListRecentMessagesRequest()
    {
        // Arrange
        var opts = new Dictionary<string, string>
        {
            ["since"] = "2026-01-01T00:00:00Z",
            ["limit"] = "10",
        };

        // Act
        var req = ClientProgram.Build("list-messages", opts);

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.ListRecentMessages);
    }

    /// <summary>
    /// Verifies that <c>list-messages</c> with a missing required option returns null so
    /// that <c>RunAsync</c> surfaces exit code 5 without sending a malformed request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsListMessages_WithMissingRequiredOption_ReturnsNull()
    {
        // Arrange: "limit" is missing
        var opts = new Dictionary<string, string> { ["since"] = "2026-01-01T00:00:00Z" };

        // Act
        var req = ClientProgram.Build("list-messages", opts);

        // Assert
        req.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <c>get-message</c> with an <c>id</c> option builds the correct request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsGetMessage_WithId_ReturnsGetMessageRequest()
    {
        // Act
        var req = ClientProgram.Build(
            "get-message",
            new Dictionary<string, string> { ["id"] = "msg-1" }
        );

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.GetMessage);
    }

    /// <summary>
    /// Verifies that <c>get-message</c> without the <c>id</c> option returns null.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsGetMessage_WithMissingId_ReturnsNull()
    {
        // Act
        var req = ClientProgram.Build("get-message", []);

        // Assert
        req.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <c>list-meeting-requests</c> with required options builds the correct request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsListMeetingRequests_WithRequiredOptions_ReturnsCorrectRequest()
    {
        // Arrange
        var opts = new Dictionary<string, string>
        {
            ["since"] = "2026-01-01T00:00:00Z",
            ["limit"] = "5",
        };

        // Act
        var req = ClientProgram.Build("list-meeting-requests", opts);

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.ListRecentMeetingRequests);
    }

    /// <summary>
    /// Verifies that <c>list-calendar</c> with required options builds the correct request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsListCalendar_WithRequiredOptions_ReturnsCorrectRequest()
    {
        // Arrange
        var opts = new Dictionary<string, string>
        {
            ["start"] = "2026-01-01T00:00:00Z",
            ["end"] = "2026-01-31T00:00:00Z",
            ["limit"] = "100",
        };

        // Act
        var req = ClientProgram.Build("list-calendar", opts);

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.ListCalendarWindow);
    }

    /// <summary>
    /// Verifies that <c>get-event</c> with the <c>id</c> option builds the correct request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsGetEvent_WithId_ReturnsGetEventRequest()
    {
        // Act
        var req = ClientProgram.Build(
            "get-event",
            new Dictionary<string, string> { ["id"] = "evt-1" }
        );

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.GetEvent);
    }

    /// <summary>
    /// Verifies that an unrecognized command returns null so that <c>RunAsync</c> exits
    /// with code 5 rather than sending a malformed RPC request.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsUnknown_ReturnsNull()
    {
        // Act
        var req = ClientProgram.Build("not-a-command", []);

        // Assert
        req.Should().BeNull();
    }
}
