using System.IO.Pipes;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using OpenClaw.MailBridge.Contracts;

namespace OpenClaw.MailBridge.Tests;

public class ContractSerializationTests
{
    [Test]
    public void MailBridgeRequest_should_round_trip_with_SystemTextJson()
    {
        var request = new MailBridgeRequest(
            Operation: "sync",
            Payload: "hello-outlook",
            TimestampUtc: DateTimeOffset.Parse("2026-04-04T12:00:00Z"));

        var json = JsonSerializer.Serialize(request);
        var hydrated = JsonSerializer.Deserialize<MailBridgeRequest>(json);

        hydrated.Should().Be(request);
    }
}

public class NamedPipeIntegrationTests
{
    [Test]
    public async Task Named_pipe_request_should_round_trip_between_client_and_server()
    {
        var pipeName = $"openclaw-test-{Guid.NewGuid():N}";
        var request = new MailBridgeRequest("ping", "integration-test", DateTimeOffset.UtcNow);

        var serverTask = RunServerAsync(pipeName);
        var response = await RunClientAsync(pipeName, request);

        response.Success.Should().BeTrue();
        response.Message.Should().Be("pong");
        response.Payload.Should().Be(request.Payload);

        await serverTask;
    }

    private static async Task RunServerAsync(string pipeName)
    {
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync();

        using var reader = new StreamReader(server, leaveOpen: true);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        var requestJson = await reader.ReadLineAsync();
        var request = JsonSerializer.Deserialize<MailBridgeRequest>(requestJson!);
        var response = new MailBridgeResponse(true, "pong", request?.Payload, DateTimeOffset.UtcNow);

        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
    }

    private static async Task<MailBridgeResponse> RunClientAsync(string pipeName, MailBridgeRequest request)
    {
        await using var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        await client.ConnectAsync(2000);

        using var reader = new StreamReader(client, leaveOpen: true);
        using var writer = new StreamWriter(client) { AutoFlush = true };

        await writer.WriteLineAsync(JsonSerializer.Serialize(request));

        var responseJson = await reader.ReadLineAsync();
        return JsonSerializer.Deserialize<MailBridgeResponse>(responseJson!)!;
    }
}
