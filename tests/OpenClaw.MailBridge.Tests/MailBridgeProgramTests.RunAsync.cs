using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;
using ClientProgram = OpenClaw.MailBridge.Client.Program;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// <c>RunAsync</c> exit-code-mapping tests for <see cref="ClientProgram"/>, split out of
/// <see cref="MailBridgeProgramTests"/> to keep each source file within the
/// 500-line limit. Behavior is unchanged; the test methods are moved verbatim.
/// </summary>
public partial class MailBridgeProgramTests
{
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
