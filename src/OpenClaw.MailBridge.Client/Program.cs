using System.IO.Pipes;
using System.Text.Json;
using OpenClaw.MailBridge.Contracts;

namespace OpenClaw.MailBridge.Client;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        var pipeName = GetArgument(args, "--pipe-name") ?? "openclaw-mail-bridge";
        var message = GetArgument(args, "--message") ?? "ping";

        return await SendAsync(pipeName, message);
    }

    private static async Task<int> SendAsync(string pipeName, string message)
    {
        var request = new MailBridgeRequest(
            Operation: "ping",
            Payload: message,
            TimestampUtc: DateTimeOffset.UtcNow);

        try
        {
            await using var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            await client.ConnectAsync(1500);

            using var reader = new StreamReader(client, leaveOpen: true);
            using var writer = new StreamWriter(client) { AutoFlush = true };

            var requestJson = JsonSerializer.Serialize(request, JsonOptions);
            await writer.WriteLineAsync(requestJson);

            var responseJson = await reader.ReadLineAsync();
            Console.WriteLine(responseJson ?? string.Empty);

            return string.IsNullOrWhiteSpace(responseJson) ? 1 : 0;
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine($"Unable to connect to named pipe '{pipeName}'. Start OpenClaw.MailBridge first.");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Named pipe I/O failed: {ex.Message}");
            return 1;
        }
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
