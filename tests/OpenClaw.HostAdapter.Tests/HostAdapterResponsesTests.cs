using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Unit tests for <see cref="HostAdapterResponses"/>.
/// Each factory method is tested in isolation using pure in-memory data; no external
/// processes, filesystem access, or HTTP stack is involved.
/// </summary>
[TestClass]
public class HostAdapterResponsesTests
{
    // ─── Arrange helpers ──────────────────────────────────────────────────────────

    private const string TestRequestId = "test-req-id";
    private const string TestAdapterVersion = "test-version";

    /// <summary>
    /// Builds a minimal ready-state <see cref="BridgeStatusDto"/> for tests that require
    /// a non-null bridge snapshot.
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

    // ─── Success ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterResponses.Success{T}"/> produces a result with
    /// HTTP 200, <c>Ok = true</c>, and the supplied payload in <c>Data</c>.
    /// </summary>
    [TestMethod]
    public void Success_Returns200WithOkTrueAndPayloadInData()
    {
        // Act
        var result = HostAdapterResponses.Success(
            "the-value",
            TestRequestId,
            TestAdapterVersion,
            null
        );

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Envelope.Ok.Should().BeTrue();
        result.Envelope.Data.Should().Be("the-value");
        result.Envelope.Error.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the request ID and adapter version are forwarded into the response
    /// meta exactly as supplied.
    /// </summary>
    [TestMethod]
    public void Success_PropagatesRequestIdAndAdapterVersionInMeta()
    {
        // Act
        var result = HostAdapterResponses.Success("data", "my-request", "1.2.3", null);

        // Assert
        result.Envelope.Meta.RequestId.Should().Be("my-request");
        result.Envelope.Meta.AdapterVersion.Should().Be("1.2.3");
    }

    /// <summary>
    /// Verifies that the optional bridge snapshot is forwarded into the response meta so
    /// callers can observe the bridge state at the time of the successful response.
    /// </summary>
    [TestMethod]
    public void Success_PropagatesBridgeSnapshotInMeta()
    {
        // Arrange
        var bridge = ReadyBridge();

        // Act
        var result = HostAdapterResponses.Success(
            "data",
            TestRequestId,
            TestAdapterVersion,
            bridge
        );

        // Assert
        result.Envelope.Meta.Bridge.Should().Be(bridge);
    }

    /// <summary>
    /// Verifies that when the payload type is <see cref="BridgeStatusDto"/>, the payload
    /// itself is used as the bridge snapshot rather than the separately supplied bridge
    /// parameter, so status responses carry their own state.
    /// </summary>
    [TestMethod]
    public void Success_WhenDataIsBridgeStatusDto_UsesBridgeStatusDtoAsMetaBridgeSnapshot()
    {
        // Arrange: bridge parameter and the data payload differ; data must win
        var data = ReadyBridge();
        var separateBridge = new BridgeStatusDto(
            BridgeState.degraded.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );

        // Act
        var result = HostAdapterResponses.Success(
            data,
            TestRequestId,
            TestAdapterVersion,
            separateBridge
        );

        // Assert: status endpoint data is authoritative over any secondary bridge snapshot
        result.Envelope.Meta.Bridge.Should().Be(data);
    }

    /// <summary>
    /// Verifies that the optional CLI exit code is null by default and forwarded when
    /// provided, preserving process diagnostic information for callers.
    /// </summary>
    [TestMethod]
    public void Success_ForwardsCliExitCodeWhenProvided()
    {
        // Act — default (no exit code)
        var defaultResult = HostAdapterResponses.Success(
            "v",
            TestRequestId,
            TestAdapterVersion,
            null
        );

        // Act — explicit exit code
        var codedResult = HostAdapterResponses.Success(
            "v",
            TestRequestId,
            TestAdapterVersion,
            null,
            cliExitCode: 0
        );

        // Assert
        defaultResult.CliExitCode.Should().BeNull();
        codedResult.CliExitCode.Should().Be(0);
    }

    // ─── Failure ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterResponses.Failure{T}"/> returns the exact HTTP
    /// status code supplied by the caller.
    /// </summary>
    [TestMethod]
    public void Failure_ReturnsSuppliedStatusCode()
    {
        // Act
        var result = HostAdapterResponses.Failure<string>(
            StatusCodes.Status503ServiceUnavailable,
            TestRequestId,
            TestAdapterVersion,
            "SOME_CODE",
            "Some message."
        );

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// Verifies that the failure result has <c>Ok = false</c>, no data, and the error
    /// fields populated with the supplied code and message.
    /// </summary>
    [TestMethod]
    public void Failure_SetsOkFalseAndPopulatesErrorFields()
    {
        // Act
        var result = HostAdapterResponses.Failure<string>(
            StatusCodes.Status400BadRequest,
            TestRequestId,
            TestAdapterVersion,
            "ERR_CODE",
            "Descriptive error message."
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Data.Should().BeNull();
        result.Envelope.Error.Should().NotBeNull();
        result.Envelope.Error!.Code.Should().Be("ERR_CODE");
        result.Envelope.Error.Message.Should().Be("Descriptive error message.");
    }

    /// <summary>
    /// Verifies that <c>Retryable</c> and <c>BridgeErrorCode</c> are forwarded correctly
    /// into the error payload.
    /// </summary>
    [TestMethod]
    public void Failure_PropagatesRetryableAndBridgeErrorCode()
    {
        // Act
        var result = HostAdapterResponses.Failure<string>(
            StatusCodes.Status503ServiceUnavailable,
            TestRequestId,
            TestAdapterVersion,
            "ADAPTER_CODE",
            "Service down.",
            bridge: null,
            bridgeErrorCode: "BRIDGE_CODE",
            retryable: true
        );

        // Assert
        result.Envelope.Error!.Retryable.Should().BeTrue();
        result.Envelope.Error.BridgeErrorCode.Should().Be("BRIDGE_CODE");
    }

    /// <summary>
    /// Verifies that the CLI exit code is forwarded in the failure result.
    /// </summary>
    [TestMethod]
    public void Failure_ForwardsCliExitCodeWhenProvided()
    {
        // Act
        var result = HostAdapterResponses.Failure<string>(
            StatusCodes.Status502BadGateway,
            TestRequestId,
            TestAdapterVersion,
            "TRANSPORT_FAILURE",
            "Pipe not found.",
            cliExitCode: 2
        );

        // Assert
        result.CliExitCode.Should().Be(2);
    }

    // ─── InvalidRequest ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterResponses.InvalidRequest{T}"/> produces an HTTP
    /// 400 result with the INVALID_REQUEST error code and the supplied message.
    /// </summary>
    [TestMethod]
    public void InvalidRequest_Returns400WithInvalidRequestCodeAndMessage()
    {
        // Act
        var result = HostAdapterResponses.InvalidRequest<string>(
            TestRequestId,
            TestAdapterVersion,
            "Parameter 'since' is required."
        );

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        result.Envelope.Error.Message.Should().Be("Parameter 'since' is required.");
        result.Envelope.Error.Retryable.Should().BeFalse();
    }

    // ─── BridgeNotReady ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterResponses.BridgeNotReady{T}"/> produces an HTTP
    /// 409 result with the BRIDGE_NOT_READY code marked retryable, indicating the client
    /// should poll until the bridge reaches a ready state.
    /// </summary>
    [TestMethod]
    public void BridgeNotReady_Returns409WithBridgeNotReadyCodeRetryableTrue()
    {
        // Arrange
        var bridge = ReadyBridge();

        // Act
        var result = HostAdapterResponses.BridgeNotReady<string>(
            TestRequestId,
            TestAdapterVersion,
            bridge
        );

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        result.Envelope.Error!.Code.Should().Be("BRIDGE_NOT_READY");
        result.Envelope.Error.Retryable.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the bridge state string is used as the <c>BridgeErrorCode</c> in the
    /// BRIDGE_NOT_READY response so callers can reason about which state the bridge is in.
    /// </summary>
    [TestMethod]
    public void BridgeNotReady_SetsBridgeStateAsBridgeErrorCode()
    {
        // Arrange
        var bridge = new BridgeStatusDto(
            BridgeState.starting.ToString(),
            BridgeMode.safe.ToString(),
            false,
            false,
            null,
            null,
            null
        );

        // Act
        var result = HostAdapterResponses.BridgeNotReady<string>(
            TestRequestId,
            TestAdapterVersion,
            bridge
        );

        // Assert: the bridge state token is surfaced so the client knows what to expect next
        result.Envelope.Error!.BridgeErrorCode.Should().Be(BridgeState.starting.ToString());
    }

    // ─── ConfigurationError ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterResponses.ConfigurationError{T}"/> produces an
    /// HTTP 503 result with the CONFIGURATION_ERROR code, no bridge snapshot, and the
    /// supplied message.
    /// </summary>
    [TestMethod]
    public void ConfigurationError_Returns503WithConfigurationErrorCodeAndMessage()
    {
        // Act
        var result = HostAdapterResponses.ConfigurationError<string>(
            TestRequestId,
            TestAdapterVersion,
            "Token file path is not configured."
        );

        // Assert: configuration errors are not retryable and carry no bridge state
        result.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        result.Envelope.Error!.Code.Should().Be("CONFIGURATION_ERROR");
        result.Envelope.Error.Message.Should().Be("Token file path is not configured.");
        result.Envelope.Error.Retryable.Should().BeFalse();
        result.Envelope.Meta.Bridge.Should().BeNull();
    }
}
