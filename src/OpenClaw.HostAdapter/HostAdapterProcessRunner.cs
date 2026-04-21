using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

/// <summary>
/// Holds stdout, stderr, and exit code captured from a completed process execution.
/// Returned by <see cref="HostAdapterProcessRunner.ProcessExecutor"/> to decouple process
/// I/O from the parsing and response-mapping logic in
/// <see cref="HostAdapterProcessRunner.ExecuteAsync{T}"/>.
/// </summary>
internal record ProcessExecutionResult(string Stdout, string Stderr, int ExitCode);

internal sealed class HostAdapterProcessRunner(IOptions<HostAdapterOptions> optionsAccessor)
    : IHostAdapterProcessRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    /// <summary>
    /// Starts the process described by <paramref name="startInfo"/>, waits for it to exit,
    /// and captures stdout, stderr, and the exit code.
    /// Returns null when the process fails to start (i.e. <c>Process.Start()</c> returns false).
    /// Replaced in unit tests to avoid spawning real child processes.
    /// </summary>
    internal Func<
        ProcessStartInfo,
        CancellationToken,
        Task<ProcessExecutionResult?>
    > ProcessExecutor { get; init; } =
        static async (startInfo, cancellationToken) =>
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                return null;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessExecutionResult(await stdoutTask, await stderrTask, process.ExitCode);
        };

    public async Task<AdapterCommandResult<T>> ExecuteAsync<T>(
        ProcessStartInfo startInfo,
        string requestId,
        BridgeStatusDto? bridge,
        Func<JsonElement, T> projector,
        CancellationToken cancellationToken
    )
    {
        ProcessExecutionResult? execution;
        try
        {
            execution = await ProcessExecutor(startInfo, cancellationToken);
            if (execution is null)
            {
                return HostAdapterResponses.Failure<T>(
                    StatusCodes.Status502BadGateway,
                    requestId,
                    options.AdapterVersion,
                    "TRANSPORT_FAILURE",
                    "OpenClaw.MailBridge.Client could not be started.",
                    bridge,
                    retryable: true
                );
            }
        }
        catch (Exception exception)
        {
            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status502BadGateway,
                requestId,
                options.AdapterVersion,
                "TRANSPORT_FAILURE",
                $"OpenClaw.MailBridge.Client failed to start: {exception.Message}",
                bridge,
                retryable: true
            );
        }

        var stdout = execution.Stdout;
        var stderr = execution.Stderr;
        var cliExitCode = execution.ExitCode;

        if (!TryDeserializeRpcResponse(stdout, out var response))
        {
            var message = string.IsNullOrWhiteSpace(stderr)
                ? "OpenClaw.MailBridge.Client returned an unreadable response."
                : stderr.Trim();

            return HostAdapterResponses.Failure<T>(
                StatusCodes.Status502BadGateway,
                requestId,
                options.AdapterVersion,
                "TRANSPORT_FAILURE",
                message,
                bridge,
                retryable: true,
                cliExitCode: cliExitCode
            );
        }

        if (response.Ok)
        {
            try
            {
                var data = ConvertPayload(response.Result, projector);
                var bridgeSnapshot = data is BridgeStatusDto status ? status : bridge;
                return HostAdapterResponses.Success(
                    data,
                    requestId,
                    options.AdapterVersion,
                    bridgeSnapshot,
                    cliExitCode
                );
            }
            catch (JsonException exception)
            {
                return HostAdapterResponses.Failure<T>(
                    StatusCodes.Status502BadGateway,
                    requestId,
                    options.AdapterVersion,
                    "TRANSPORT_FAILURE",
                    $"The bridge response payload could not be parsed: {exception.Message}",
                    bridge,
                    retryable: true,
                    cliExitCode: cliExitCode
                );
            }
        }

        return HostAdapterResponseMapper.MapFailure<T>(
            requestId,
            options.AdapterVersion,
            bridge,
            response.Error,
            stderr,
            cliExitCode
        );
    }

    internal static T DeserializePayload<T>(JsonElement element)
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText(), Json)
            ?? throw new JsonException($"Unable to deserialize payload as {typeof(T).Name}.");
    }

    private static T ConvertPayload<T>(object? result, Func<JsonElement, T> projector)
    {
        var element = result switch
        {
            JsonElement jsonElement => jsonElement,
            null => JsonSerializer.SerializeToElement<object?>(null, Json),
            _ => JsonSerializer.SerializeToElement(result, Json),
        };

        return projector(element);
    }

    private static bool TryDeserializeRpcResponse(string stdout, out RpcResponse response)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            response = RpcResponse.Failure(
                string.Empty,
                BridgeErrorCodes.InternalError,
                "No bridge response was written to stdout."
            );
            return false;
        }

        try
        {
            response =
                JsonSerializer.Deserialize<RpcResponse>(stdout, Json)
                ?? RpcResponse.Failure(
                    string.Empty,
                    BridgeErrorCodes.InternalError,
                    "The bridge response was empty."
                );
            return true;
        }
        catch (JsonException)
        {
            response = RpcResponse.Failure(
                string.Empty,
                BridgeErrorCodes.InternalError,
                "The bridge response was not valid JSON."
            );
            return false;
        }
    }
}
