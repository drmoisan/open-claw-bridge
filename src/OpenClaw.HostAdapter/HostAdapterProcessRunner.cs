using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter;

internal sealed class HostAdapterProcessRunner(IOptions<HostAdapterOptions> optionsAccessor)
    : IHostAdapterProcessRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    public async Task<AdapterCommandResult<T>> ExecuteAsync<T>(
        ProcessStartInfo startInfo,
        string requestId,
        BridgeStatusDto? bridge,
        Func<JsonElement, T> projector,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
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

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var cliExitCode = process.ExitCode;

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
