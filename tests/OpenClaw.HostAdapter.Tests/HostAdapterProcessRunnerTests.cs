using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

/// <summary>
/// Unit tests for <see cref="HostAdapterProcessRunner"/>.
/// The <c>ProcessExecutor</c> seam replaces real child-process spawning so that every
/// branch of <see cref="HostAdapterProcessRunner.ExecuteAsync{T}"/> is exercised without
/// any external process or filesystem dependency.
/// </summary>
[TestClass]
public class HostAdapterProcessRunnerTests
{
    // Uses the same serializer defaults as production code for constructing realistic stdout.
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // ─── Arrange helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="HostAdapterProcessRunner"/> whose process execution is fully
    /// controlled by the supplied <paramref name="processExecutor"/> delegate.
    /// </summary>
    private static HostAdapterProcessRunner BuildRunner(
        Func<ProcessStartInfo, CancellationToken, Task<ProcessExecutionResult?>> processExecutor,
        string adapterVersion = "test-version"
    ) =>
        new(Options.Create(new HostAdapterOptions { AdapterVersion = adapterVersion }))
        {
            ProcessExecutor = processExecutor,
        };

    /// <summary>
    /// Returns a minimal <see cref="ProcessStartInfo"/> used as a placeholder when the seam
    /// ignores the actual executable.
    /// </summary>
    private static ProcessStartInfo AnyStartInfo() => new("test.exe");

    /// <summary>
    /// Wraps the given output values in a successfully completed
    /// <see cref="ProcessExecutionResult"/> task.
    /// </summary>
    private static Task<ProcessExecutionResult?> CompletedProcess(
        string stdout,
        string stderr = "",
        int exitCode = 0
    ) =>
        Task.FromResult<ProcessExecutionResult?>(
            new ProcessExecutionResult(stdout, stderr, exitCode)
        );

    /// <summary>
    /// Serializes an <see cref="RpcResponse"/> to the JSON string that the CLI bridge
    /// would write to stdout.
    /// </summary>
    private static string ToRpcJson(RpcResponse response) =>
        JsonSerializer.Serialize(response, WebJson);

    // ─── DeserializePayload ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a valid JSON element is correctly deserialized to the requested type.
    /// </summary>
    [TestMethod]
    public void DeserializePayload_WhenElementContainsValidValue_ReturnsDeserializedValue()
    {
        // Arrange: a JSON string element representing the expected token value
        var element = JsonDocument.Parse("\"expected-token\"").RootElement;

        // Act
        var result = HostAdapterProcessRunner.DeserializePayload<string>(element);

        // Assert
        result.Should().Be("expected-token");
    }

    /// <summary>
    /// Verifies that a JSON null element causes <see cref="HostAdapterProcessRunner.DeserializePayload{T}"/>
    /// to throw <see cref="JsonException"/> rather than returning null, enforcing a
    /// non-nullable contract on the projected payload.
    /// </summary>
    [TestMethod]
    public void DeserializePayload_WhenElementIsJsonNull_ThrowsJsonException()
    {
        // Arrange: a JSON null element deserializes to null for reference types,
        // which triggers the guard throw inside DeserializePayload
        var element = JsonDocument.Parse("null").RootElement;

        // Act
        Action act = () => HostAdapterProcessRunner.DeserializePayload<string>(element);

        // Assert
        act.Should().Throw<JsonException>();
    }

    // ─── ExecuteAsync: process start failure ──────────────────────────────────────

    /// <summary>
    /// Verifies that a null return from <c>ProcessExecutor</c> (signaling that
    /// <c>Process.Start()</c> returned false) results in a TRANSPORT_FAILURE response.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenProcessExecutorReturnsNull_ReturnsTransportFailureNotStarted()
    {
        // Arrange: executor returns null, simulating OS-level launch failure
        var runner = BuildRunner((_, _) => Task.FromResult<ProcessExecutionResult?>(null));

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-null",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Message.Should().Contain("could not be started");
    }

    /// <summary>
    /// Verifies that an exception thrown by <c>ProcessExecutor</c> (e.g. the executable is
    /// not found) is caught and surfaced as a TRANSPORT_FAILURE response whose message
    /// includes the original exception text.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenProcessExecutorThrows_ReturnsTransportFailureWithExceptionMessage()
    {
        // Arrange: executor throws, simulating an OS-level error such as "file not found"
        var runner = BuildRunner(
            (_, _) =>
                Task.FromException<ProcessExecutionResult?>(
                    new InvalidOperationException("No such executable.")
                )
        );

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-throw",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Message.Should().Contain("No such executable.");
    }

    // ─── ExecuteAsync: stdout parse failures ──────────────────────────────────────

    /// <summary>
    /// Verifies that empty stdout with empty stderr produces a TRANSPORT_FAILURE using the
    /// default "unreadable response" message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenStdoutIsEmpty_ReturnsTransportFailureUnreadableResponse()
    {
        // Arrange: process exits cleanly but writes nothing to stdout
        var runner = BuildRunner(
            (_, _) => CompletedProcess(stdout: string.Empty, stderr: string.Empty)
        );

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-empty",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert: the fallback "unreadable response" message is used when stderr is also empty
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Message.Should().Contain("unreadable response");
    }

    /// <summary>
    /// Verifies that invalid JSON on stdout combined with a populated stderr causes the
    /// trimmed stderr content to be used as the TRANSPORT_FAILURE message, since it is
    /// more informative than the generic fallback.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenStdoutIsInvalidJson_WithStderr_UsesStderrAsMessage()
    {
        // Arrange: the process wrote a plain-text error to stderr instead of a JSON envelope
        var runner = BuildRunner(
            (_, _) => CompletedProcess(stdout: "not json at all", stderr: "  Adapter crashed.  ")
        );

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-bad-json-stderr",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert: stderr is trimmed and surfaced as the error message
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Message.Should().Be("Adapter crashed.");
    }

    /// <summary>
    /// Verifies that invalid JSON on stdout with no stderr falls back to the generic
    /// "unreadable response" message rather than a blank or null message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenStdoutIsInvalidJson_WithoutStderr_UsesDefaultUnreadableMessage()
    {
        // Arrange: process stdout is syntactically malformed, stderr is empty
        var runner = BuildRunner(
            (_, _) => CompletedProcess(stdout: "{bad json}", stderr: string.Empty)
        );

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-bad-json",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Message.Should().Contain("unreadable response");
    }

    // ─── ExecuteAsync: successful process response ────────────────────────────────

    /// <summary>
    /// Verifies that a valid RPC success envelope on stdout produces a success
    /// <see cref="AdapterCommandResult{T}"/> with the correctly projected payload.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenRpcResponseIsSuccess_ReturnsSuccessWithProjectedData()
    {
        // Arrange: stdout contains a well-formed RPC success with a string payload
        var stdout = ToRpcJson(RpcResponse.Success("resp-1", "the-projected-value"));
        var runner = BuildRunner((_, _) => CompletedProcess(stdout));

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-ok",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeTrue();
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Envelope.Data.Should().Be("the-projected-value");
    }

    /// <summary>
    /// Verifies that the CLI exit code from the process is forwarded in the result, allowing
    /// callers to record it for diagnostics.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenRpcResponseIsSuccess_ForwardsCliExitCode()
    {
        // Arrange
        var stdout = ToRpcJson(RpcResponse.Success("resp-2", "value"));
        var runner = BuildRunner((_, _) => CompletedProcess(stdout, exitCode: 42));

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-exit",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert: CLI exit code must be preserved for observability
        result.CliExitCode.Should().Be(42);
    }

    /// <summary>
    /// Verifies that a <see cref="JsonException"/> raised by the projector while parsing
    /// a successful RPC payload is caught and returned as a TRANSPORT_FAILURE response
    /// containing the parse exception message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenProjectorThrowsJsonException_ReturnsTransportFailureWithParseMessage()
    {
        // Arrange: projector simulates a type-mismatch or unexpected payload shape
        var stdout = ToRpcJson(RpcResponse.Success("resp-3", "irrelevant"));
        var runner = BuildRunner((_, _) => CompletedProcess(stdout));

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-proj-fail",
            null,
            _ => throw new JsonException("Simulated parse failure."),
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.Envelope.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Envelope.Error.Message.Should().Contain("Simulated parse failure.");
    }

    // ─── ExecuteAsync: RPC failure response ───────────────────────────────────────

    /// <summary>
    /// Verifies that a valid RPC failure envelope on stdout is mapped to the correct HTTP
    /// status code and error code via <see cref="HostAdapterResponseMapper"/>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WhenRpcResponseIsFailure_ReturnsMappedHttpStatus()
    {
        // Arrange: bridge returns NOT_FOUND, which HostAdapterResponseMapper maps to 404
        var stdout = ToRpcJson(
            RpcResponse.Failure("resp-4", BridgeErrorCodes.NotFound, "Item was not found.")
        );
        var runner = BuildRunner((_, _) => CompletedProcess(stdout));

        // Act
        var result = await runner.ExecuteAsync<string>(
            AnyStartInfo(),
            "req-not-found",
            null,
            HostAdapterProcessRunner.DeserializePayload<string>,
            CancellationToken.None
        );

        // Assert
        result.Envelope.Ok.Should().BeFalse();
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.Envelope.Error!.Code.Should().Be(BridgeErrorCodes.NotFound);
    }
}
