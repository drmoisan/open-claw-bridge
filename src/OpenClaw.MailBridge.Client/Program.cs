using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Client;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var parsed = Parse(args);
            if (parsed is null)
                return 5;
            var req = Build(parsed.Value.command, parsed.Value.options);
            if (req is null)
                return 5;
            var resp = await Send(req);
            Console.Out.WriteLine(JsonSerializer.Serialize(resp, Json));
            if (resp.Ok)
                return 0;
            return resp.Error?.Code switch
            {
                BridgeErrorCodes.Unauthorized => 3,
                BridgeErrorCodes.OutlookUnavailable => 4,
                BridgeErrorCodes.InvalidRequest => 5,
                _ => 6,
            };
        }
        catch (UnauthorizedAccessException uae)
        {
            Console.Error.WriteLine(uae.Message);
            return 3;
        }
        catch (TimeoutException tex)
        {
            Console.Error.WriteLine(tex.Message);
            return 2;
        }
        catch (IOException ioex)
        {
            Console.Error.WriteLine(ioex.Message);
            return 2;
        }
    }

    private static async Task<RpcResponse> Send(RpcRequest req)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            "openclaw_mailbridge_v1",
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );
        await client.ConnectAsync(2000);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(req, Json);
        await client.WriteAsync(bytes);
        await client.FlushAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        do
        {
            var read = await client.ReadAsync(buffer);
            ms.Write(buffer, 0, read);
        } while (!client.IsMessageComplete);

        return JsonSerializer.Deserialize<RpcResponse>(Encoding.UTF8.GetString(ms.ToArray()), Json)
            ?? RpcResponse.Failure(req.Id, BridgeErrorCodes.InternalError, "Invalid response");
    }

    private static (string command, Dictionary<string, string> options)? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("No command");
            return null;
        }
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < args.Length - 1; i += 2)
        {
            if (!args[i].StartsWith("--"))
                continue;
            options[args[i][2..].Replace('-', '_')] = args[i + 1];
        }

        return (args[0], options);
    }

    private static RpcRequest? Build(string command, Dictionary<string, string> opts)
    {
        var id = Guid.NewGuid().ToString();
        return command switch
        {
            "status" => new RpcRequest(id, BridgeMethods.GetStatus, null),
            "list-messages" => Req(id, BridgeMethods.ListRecentMessages, opts, "since", "limit"),
            "get-message" => Req(id, BridgeMethods.GetMessage, opts, "id"),
            "list-meeting-requests" => Req(
                id,
                BridgeMethods.ListRecentMeetingRequests,
                opts,
                "since",
                "limit"
            ),
            "list-calendar" => Req(
                id,
                BridgeMethods.ListCalendarWindow,
                opts,
                "start",
                "end",
                "limit"
            ),
            "get-event" => Req(id, BridgeMethods.GetEvent, opts, "id"),
            _ => null,
        };
    }

    private static RpcRequest? Req(
        string id,
        string method,
        Dictionary<string, string> opts,
        params string[] required
    )
    {
        foreach (var key in required)
            if (!opts.ContainsKey(key.Replace('-', '_')))
            {
                Console.Error.WriteLine($"Missing --{key}");
                return null;
            }
        return new RpcRequest(id, method, opts);
    }
}
