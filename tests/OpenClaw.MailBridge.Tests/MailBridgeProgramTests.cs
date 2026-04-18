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
public class MailBridgeProgramTests
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

    // ─── RunAsync: exit code mapping ─────────────────────────────────────────────

    private static Task<int> RunWithResponse(string command, RpcResponse response)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        return ClientProgram.RunAsync(
            [command],
            (_, _) => Task.FromResult(response),
            stdout,
            stderr
        );
    }

    /// <summary>
    /// Verifies that a successful RPC response yields exit code 0.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsSuccess_ReturnsExitCode0()
    {
        // Arrange: a well-formed success response
        var response = RpcResponse.Success("id-1", new { value = 1 });

        // Act
        var exitCode = await RunWithResponse("status", response);

        // Assert
        exitCode.Should().Be(0);
    }

    /// <summary>
    /// Verifies that an UNAUTHORIZED error code from the bridge yields exit code 3.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsUnauthorizedError_ReturnsExitCode3()
    {
        // Arrange
        var response = RpcResponse.Failure("id-1", BridgeErrorCodes.Unauthorized, "Auth rejected.");

        // Act
        var exitCode = await RunWithResponse("status", response);

        // Assert: exit code 3 is the well-known "auth failed" signal for callers
        exitCode.Should().Be(3);
    }

    /// <summary>
    /// Verifies that an OUTLOOK_UNAVAILABLE error code from the bridge yields exit code 4.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsOutlookUnavailableError_ReturnsExitCode4()
    {
        // Arrange
        var response = RpcResponse.Failure(
            "id-1",
            BridgeErrorCodes.OutlookUnavailable,
            "Outlook is down."
        );

        // Act
        var exitCode = await RunWithResponse("status", response);

        // Assert
        exitCode.Should().Be(4);
    }

    /// <summary>
    /// Verifies that a PAYLOAD_TOO_LARGE error code from the bridge yields exit code 5,
    /// matching the same code as INVALID_REQUEST so callers treat both as bad-input signals.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsPayloadTooLargeError_ReturnsExitCode5()
    {
        // Arrange
        var response = RpcResponse.Failure("id-1", BridgeErrorCodes.PayloadTooLarge, "Too big.");

        // Act
        var exitCode = await RunWithResponse("status", response);

        // Assert
        exitCode.Should().Be(5);
    }

    /// <summary>
    /// Verifies that any unrecognized error code yields the fallback exit code 6.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsUnrecognizedErrorCode_ReturnsExitCode6()
    {
        // Arrange
        var response = RpcResponse.Failure("id-1", "SOME_OTHER_CODE", "Unexpected failure.");

        // Act
        var exitCode = await RunWithResponse("status", response);

        // Assert
        exitCode.Should().Be(6);
    }

    /// <summary>
    /// Verifies that an <see cref="UnauthorizedAccessException"/> thrown by the send
    /// delegate yields exit code 3, consistent with RPC-level UNAUTHORIZED semantics.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendThrowsUnauthorizedAccessException_ReturnsExitCode3()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = await ClientProgram.RunAsync(
            ["status"],
            (_, _) => throw new UnauthorizedAccessException("Access denied."),
            stdout,
            stderr
        );

        // Assert
        exitCode.Should().Be(3);
        stderr.ToString().Should().Contain("Access denied.");
    }

    /// <summary>
    /// Verifies that a <see cref="TimeoutException"/> thrown by the send delegate yields
    /// exit code 2, indicating a transport-level failure to callers.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendThrowsTimeoutException_ReturnsExitCode2()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = await ClientProgram.RunAsync(
            ["status"],
            (_, _) => throw new TimeoutException("Connection timed out."),
            stdout,
            stderr
        );

        // Assert
        exitCode.Should().Be(2);
        stderr.ToString().Should().Contain("Connection timed out.");
    }

    /// <summary>
    /// Verifies that an <see cref="IOException"/> thrown by the send delegate yields exit
    /// code 2, treating pipe I/O failures as transport errors.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendThrowsIOException_ReturnsExitCode2()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = await ClientProgram.RunAsync(
            ["status"],
            (_, _) => throw new IOException("Pipe broken."),
            stdout,
            stderr
        );

        // Assert
        exitCode.Should().Be(2);
        stderr.ToString().Should().Contain("Pipe broken.");
    }

    /// <summary>
    /// Verifies that an empty argument list (causing <c>Parse</c> to return null) yields
    /// exit code 5 without invoking the send delegate.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenParseReturnsNull_ReturnsExitCode5WithoutCallingDelegate()
    {
        // Arrange: no args → Parse returns null
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var sendWasCalled = false;

        // Act
        var exitCode = await ClientProgram.RunAsync(
            [],
            (_, _) =>
            {
                sendWasCalled = true;
                return Task.FromResult(RpcResponse.Success("id", null));
            },
            stdout,
            stderr
        );

        // Assert
        exitCode.Should().Be(5);
        sendWasCalled.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that an unknown command (causing <c>Build</c> to return null) yields exit
    /// code 5 without invoking the send delegate.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenBuildReturnsNull_ReturnsExitCode5WithoutCallingDelegate()
    {
        // Arrange: unknown command → Build returns null
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var sendWasCalled = false;

        // Act
        var exitCode = await ClientProgram.RunAsync(
            ["not-a-command"],
            (_, _) =>
            {
                sendWasCalled = true;
                return Task.FromResult(RpcResponse.Success("id", null));
            },
            stdout,
            stderr
        );

        // Assert
        exitCode.Should().Be(5);
        sendWasCalled.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the JSON-serialized response is written to stdout and that stderr
    /// remains empty for a successful response.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenSendReturnsSuccess_WritesJsonToStdoutAndLeavesStderrEmpty()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var response = RpcResponse.Success("id-out", new { value = "check" });

        // Act
        await ClientProgram.RunAsync(
            ["status"],
            (_, _) => Task.FromResult(response),
            stdout,
            stderr
        );

        // Assert: response must appear on stdout; diagnostics must not bleed into stderr
        stdout.ToString().Should().Contain("\"ok\":true");
        stderr.ToString().Should().BeEmpty();
    }
}
