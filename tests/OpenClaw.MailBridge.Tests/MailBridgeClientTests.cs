using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;
using ClientProgram = OpenClaw.MailBridge.Client.Program;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class MailBridgeClientTests
{
    [TestMethod]
    public void Client_program_should_prefer_pipe_name_override_over_settings_fallback()
    {
        var pipeName = ClientProgram.ResolvePipeName(
            new Dictionary<string, string> { ["pipe_name"] = "override-pipe" }
        );

        pipeName.Should().Be("override-pipe");
    }

    [TestMethod]
    public async Task Client_program_should_keep_json_on_stdout_and_diagnostics_on_stderr_with_exit_code_mapping()
    {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());

        var exitCode = await ClientProgram.RunAsync(
            new[] { "status" },
            (_, req) =>
                Task.FromResult(
                    RpcResponse.Failure(req.Id, BridgeErrorCodes.InvalidRequest, "Bad request")
                ),
            stdout,
            stderr
        );

        exitCode.Should().Be(5);
        stdout.ToString().Should().Contain("\"ok\":false");
        stdout.ToString().Should().Contain(BridgeErrorCodes.InvalidRequest);
        stderr.ToString().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Client_program_send_should_read_message_mode_response_without_invalid_operation_exception()
    {
        var pipeName = $"mailbridge-client-test-{Guid.NewGuid():N}";
        var request = new RpcRequest("req-1", BridgeMethods.GetStatus, null);
        var expectedResponse = RpcResponse.Success(
            request.Id,
            new BridgeStatusDto(
                BridgeState.ready.ToString(),
                BridgeMode.safe.ToString(),
                true,
                false,
                null,
                null,
                null
            )
        );

        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );

            await server.WaitForConnectionAsync();

            using var requestStream = new MemoryStream();
            var requestBuffer = new byte[4096];

            // Read the full client request before sending the bridge response.
            do
            {
                var read = await server.ReadAsync(requestBuffer);
                requestStream.Write(requestBuffer, 0, read);
            } while (!server.IsMessageComplete);

            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(expectedResponse);
            await server.WriteAsync(responseBytes);
            await server.FlushAsync();
        });

        var response = await ClientProgram.Send(pipeName, request);

        response.Ok.Should().BeTrue();
        response.Id.Should().Be(request.Id);
        response.Error.Should().BeNull();
        await serverTask;
    }

    [TestMethod]
    public async Task Client_program_send_should_return_internal_error_when_server_closes_without_json_payload()
    {
        var pipeName = $"mailbridge-client-empty-response-{Guid.NewGuid():N}";
        var request = new RpcRequest("req-1", BridgeMethods.GetStatus, null);

        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );

            await server.WaitForConnectionAsync();

            using var requestStream = new MemoryStream();
            var requestBuffer = new byte[4096];

            // Drain the client request, then close the pipe without returning a response payload.
            do
            {
                var read = await server.ReadAsync(requestBuffer);
                requestStream.Write(requestBuffer, 0, read);
            } while (!server.IsMessageComplete);
        });

        var response = await ClientProgram.Send(pipeName, request);

        response.Ok.Should().BeFalse();
        response.Id.Should().Be(request.Id);
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InternalError);
        response.Error.Message.Should().Contain("Empty response");
        await serverTask;
    }

    [TestMethod]
    public async Task Client_program_send_should_return_internal_error_when_server_sends_truncated_json()
    {
        var pipeName = $"mailbridge-client-truncated-{Guid.NewGuid():N}";
        var request = new RpcRequest("req-1", BridgeMethods.GetStatus, null);

        var serverTask = Task.Run(async () =>
        {
            using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );

            await server.WaitForConnectionAsync();

            using var requestStream = new MemoryStream();
            var requestBuffer = new byte[4096];

            do
            {
                var read = await server.ReadAsync(requestBuffer);
                requestStream.Write(requestBuffer, 0, read);
            } while (!server.IsMessageComplete);

            // Write truncated JSON that starts valid but is incomplete.
            var truncated = Encoding.UTF8.GetBytes(
                "{\"id\":\"req-1\",\"ok\":true,\"result\":{\"state\":\"rea"
            );
            await server.WriteAsync(truncated);
            await server.FlushAsync();
        });

        var response = await ClientProgram.Send(pipeName, request);

        response.Ok.Should().BeFalse();
        response.Id.Should().Be(request.Id);
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InternalError);
        response.Error.Message.Should().Contain("Malformed response");
        await serverTask;
    }
}
