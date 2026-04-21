using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Unit tests for <see cref="HostAdapterResponseMapper.MapFailure{T}"/>.
/// Each branch of the method is exercised in isolation using pure in-memory inputs;
/// no external processes, filesystem access, or HTTP stack is involved.
/// </summary>
/// <remarks>
/// Scenarios for NOT_FOUND → 404 and OUTLOOK_UNAVAILABLE → 503 (error-code path) are
/// already covered in <see cref="HostAdapterMappingTests"/> and are not duplicated here.
/// </remarks>
[TestClass]
public class HostAdapterResponseMapperTests
{
    // ─── Arrange helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal ready-state <see cref="BridgeStatusDto"/> for use in tests that
    /// need a non-null bridge snapshot but do not exercise bridge state logic.
    /// </summary>
    private static BridgeStatusDto ReadyBridge() =>
        new(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            OutlookConnected: true,
            CacheStale: false,
            StaleReason: null,
            LastInboxScanUtc: null,
            LastCalendarScanUtc: null
        );

    /// <summary>
    /// Invokes <see cref="HostAdapterResponseMapper.MapFailure{T}"/> with defaults for
    /// parameters not relevant to the specific scenario under test.
    /// </summary>
    private static AdapterCommandResult<string> CallMapFailure(
        RpcError? error = null,
        string stderr = "",
        int cliExitCode = 1,
        BridgeStatusDto? bridge = null
    ) =>
        HostAdapterResponseMapper.MapFailure<string>(
            "req-id",
            "test-version",
            bridge,
            error,
            stderr,
            cliExitCode
        );

    // ─── UNAUTHORIZED branch ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an UNAUTHORIZED error code maps to HTTP 401 with the bridge error
    /// code preserved and retryable set to false.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenErrorCodeIsUnauthorized_Returns401WithRetryableFalse()
    {
        // Arrange
        var error = new RpcError(BridgeErrorCodes.Unauthorized, "Token was rejected.");

        // Act
        var result = CallMapFailure(error: error);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.Unauthorized);
        result.Envelope.Error.Retryable.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CLI exit code 3 triggers the UNAUTHORIZED mapping even when the
    /// RPC error is absent, using the built-in fallback message.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs3AndErrorIsNull_Returns401WithDefaultMessage()
    {
        // Act
        var result = CallMapFailure(error: null, cliExitCode: 3);

        // Assert: exit code 3 is the well-known "auth rejected" exit code for the CLI bridge
        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.Unauthorized);
        result
            .Envelope.Error.Message.Should()
            .Contain(
                "unauthorized",
                Exactly.Once(),
                because: "default message must describe the rejection"
            );
    }

    // ─── OUTLOOK_UNAVAILABLE branch ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that CLI exit code 4 triggers the OUTLOOK_UNAVAILABLE mapping without
    /// a corresponding RPC error, and that the result is marked retryable because Outlook
    /// availability is typically transient.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs4AndErrorIsNull_Returns503RetryableTrue()
    {
        // Act
        var result = CallMapFailure(error: null, cliExitCode: 4);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.OutlookUnavailable);
        result.Envelope.Error.Retryable.Should().BeTrue();
    }

    // ─── INVALID_REQUEST / PAYLOAD_TOO_LARGE branch ──────────────────────────────

    /// <summary>
    /// Verifies that an INVALID_REQUEST error code maps to HTTP 400 with the error code
    /// used as both the adapter code and the bridge error code.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenErrorCodeIsInvalidRequest_Returns400()
    {
        // Arrange
        var error = new RpcError(BridgeErrorCodes.InvalidRequest, "Parameter 'since' is required.");

        // Act
        var result = CallMapFailure(error: error);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        result.Envelope.Error.Retryable.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a PAYLOAD_TOO_LARGE error code maps to HTTP 400 and that the original
    /// bridge error code is preserved in the response so callers can distinguish the two
    /// 400-class subtypes.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenErrorCodeIsPayloadTooLarge_Returns400AndPreservesOriginalBridgeErrorCode()
    {
        // Arrange
        var error = new RpcError(BridgeErrorCodes.PayloadTooLarge, "Request body exceeds limit.");

        // Act
        var result = CallMapFailure(error: error);

        // Assert: adapter-level code is normalized to INVALID_REQUEST, but bridge code is preserved
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        result.Envelope.Error.BridgeErrorCode.Should().Be(BridgeErrorCodes.PayloadTooLarge);
    }

    /// <summary>
    /// Verifies that CLI exit code 5 triggers the 400 mapping without a corresponding
    /// RPC error and uses the fallback message.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs5AndErrorIsNull_Returns400()
    {
        // Act
        var result = CallMapFailure(error: null, cliExitCode: 5);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
    }

    // ─── cliExitCode == 2 (TRANSPORT_FAILURE) branch ─────────────────────────────

    /// <summary>
    /// Verifies that CLI exit code 2 maps to a 502 TRANSPORT_FAILURE response that is
    /// marked retryable, regardless of the RPC error payload.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs2_Returns502TransportFailureRetryableTrue()
    {
        // Act
        var result = CallMapFailure(error: null, stderr: string.Empty, cliExitCode: 2);

        // Assert: exit code 2 signals a bridge transport fault — caller should retry
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Retryable.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that when CLI exit code 2 is accompanied by non-empty stderr, the trimmed
    /// stderr content is surfaced as the error message to provide actionable diagnostics.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs2AndStderrIsPopulated_UsesTrimmedStderrAsMessage()
    {
        // Act
        var result = CallMapFailure(
            error: null,
            stderr: "  Bridge pipe not found.  ",
            cliExitCode: 2
        );

        // Assert
        result.Envelope.Error!.Message.Should().Be("Bridge pipe not found.");
    }

    /// <summary>
    /// Verifies that when CLI exit code 2 occurs with empty stderr, the built-in fallback
    /// message is used rather than exposing a blank or null string to callers.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenCliExitCodeIs2AndStderrIsEmpty_UsesDefaultTransportMessage()
    {
        // Act
        var result = CallMapFailure(error: null, stderr: string.Empty, cliExitCode: 2);

        // Assert
        result.Envelope.Error!.Message.Should().NotBeNullOrWhiteSpace();
        result.Envelope.Error.Message.Should().Contain("unavailable");
    }

    // ─── Fallback branch ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when no known error code or special exit code matches, an RPC error
    /// with an unrecognized code is surfaced as 502 using the error's original code.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenErrorCodeIsUnknown_ReturnsFallback502WithOriginalCode()
    {
        // Arrange: an error code not handled by any specific branch
        var error = new RpcError("UNRECOGNIZED_CODE", "Something unusual happened.");

        // Act
        var result = CallMapFailure(error: error, cliExitCode: 0);

        // Assert: the original error code is preserved in the 502 fallback
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        result.Envelope.Error!.Code.Should().Be("UNRECOGNIZED_CODE");
        result.Envelope.Error.Message.Should().Be("Something unusual happened.");
    }

    /// <summary>
    /// Verifies that when both the error and exit code are unknown, the fallback 502 uses
    /// the generic TRANSPORT_FAILURE code and default message.
    /// </summary>
    [TestMethod]
    public void MapFailure_WhenErrorIsNullAndExitCodeIsUnknown_ReturnsFallback502WithDefaultMessage()
    {
        // Act
        var result = CallMapFailure(error: null, cliExitCode: 99);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ─── Cross-cutting: bridge snapshot and CLI exit code propagation ─────────────

    /// <summary>
    /// Verifies that the bridge snapshot supplied to <see cref="HostAdapterResponseMapper.MapFailure{T}"/>
    /// is forwarded into the response meta regardless of which error branch is taken.
    /// </summary>
    [TestMethod]
    public void MapFailure_AlwaysPropagatesBridgeSnapshotInMeta()
    {
        // Arrange
        var bridge = ReadyBridge();
        var error = new RpcError(BridgeErrorCodes.NotFound, "Not found.");

        // Act
        var result = HostAdapterResponseMapper.MapFailure<string>(
            "req-bridge",
            "test-version",
            bridge,
            error,
            string.Empty,
            0
        );

        // Assert: bridge snapshot must be available for observability regardless of error type
        result.Envelope.Meta.Bridge.Should().Be(bridge);
    }

    /// <summary>
    /// Verifies that the CLI exit code is forwarded in the result for every mapped failure
    /// to preserve process diagnostic information for logging.
    /// </summary>
    [TestMethod]
    public void MapFailure_AlwaysForwardsCliExitCodeInResult()
    {
        // Arrange: use the fallback branch so that only generic 502 forwarding is exercised
        var error = new RpcError("SOME_CODE", "Details.");

        // Act
        var result = HostAdapterResponseMapper.MapFailure<string>(
            "req-exit",
            "test-version",
            null,
            error,
            string.Empty,
            cliExitCode: 7
        );

        // Assert
        result.CliExitCode.Should().Be(7);
    }
}
