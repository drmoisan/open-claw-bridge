using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

public partial class MailBridgeRuntimeTests
{
    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_invalid_request_for_unknown_method()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );
        var response = await InvokeHandlePayloadAsync(
            worker,
            """{"id":"1","method":"unknown","params":null}"""
        );
        response.Should().Contain(BridgeErrorCodes.InvalidRequest);
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_payload_too_large_error()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );
        var oversizedParams = new string('a', 70000);
        var payload =
            $"{{\"id\":\"1\",\"method\":\"{BridgeMethods.GetStatus}\",\"params\":{{\"x\":\"{oversizedParams}\"}}}}";

        var response = await InvokeHandlePayloadAsync(worker, payload);
        response.Should().Contain(BridgeErrorCodes.PayloadTooLarge);
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_client_should_return_status_for_get_status_method()
    {
        var repo = new FakeScanStateRepository
        {
            Values =
            {
                ["last_inbox_scan_utc"] = DateTimeOffset.UtcNow,
                ["last_calendar_scan_utc"] = DateTimeOffset.UtcNow,
            },
        };
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            repo,
            NullLogger<PipeRpcWorker>.Instance
        );

        var response = await InvokeHandlePayloadAsync(
            worker,
            $"{{\"id\":\"1\",\"method\":\"{BridgeMethods.GetStatus}\",\"params\":null}}"
        );
        response.Should().Contain("\"ok\":true");
        response.Should().Contain("\"state\"");
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_handle_should_return_status_and_repository_backed_items()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings) { CacheStale = true, StaleReason = "unit-test" };
        var repo = new FakeScanStateRepository
        {
            Values =
            {
                ["last_inbox_scan_utc"] = DateTimeOffset.UtcNow,
                ["last_calendar_scan_utc"] = DateTimeOffset.UtcNow,
            },
        };
        await repo.UpsertMessageAsync(
            "entry-1",
            "store-1",
            new MessageDto(
                BridgeIdCodec.MessageId("entry-1", false),
                "mail",
                "Subject",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                true,
                false,
                "IPM.Note",
                "Sender",
                "sender@example.com",
                null,
                null,
                "Preview",
                true,
                false
            )
        );

        var worker = new PipeRpcWorker(settings, state, repo, NullLogger<PipeRpcWorker>.Instance);

        var status = await worker.Handle(new RpcRequest("1", BridgeMethods.GetStatus, null));
        status.Ok.Should().BeTrue();

        var list = await worker.Handle(
            new RpcRequest(
                "2",
                BridgeMethods.ListRecentMessages,
                new Dictionary<string, string>
                {
                    ["since"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
                    ["limit"] = "10",
                }
            )
        );
        list.Ok.Should().BeTrue();
        list.Result.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Pipe_rpc_worker_write_response_should_downgrade_oversize_payload()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        await using var stream = new MemoryStream();
        var huge = new string('a', 1024 * 1024 + 128);
        await worker.WriteResponse(
            stream,
            RpcResponse.Success("id", new { huge }),
            CancellationToken.None
        );

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        json.Should().Contain(BridgeErrorCodes.InternalError);
        json.Should().Contain("Response too large");
    }

    [TestMethod]
    public void Pipe_rpc_worker_build_pipe_security_should_return_descriptor()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        if (!OperatingSystem.IsWindows())
        {
            var act = () => worker.BuildPipeSecurity();
            act.Should().Throw<PlatformNotSupportedException>();
            return;
        }

        var buildSecurity = () => worker.BuildPipeSecurity();

        try
        {
            _ = new NTAccount("openclaw-svc").Translate(typeof(SecurityIdentifier));
            buildSecurity.Should().NotThrow();
        }
        catch (IdentityNotMappedException)
        {
            buildSecurity.Should().Throw<IdentityNotMappedException>();
        }
    }

    private static async Task<string> InvokeHandlePayloadAsync(PipeRpcWorker worker, string payload)
    {
        var response = await worker.BuildResponseAsync(payload);
        await using var stream = new MemoryStream();
        await worker.WriteResponse(stream, response, CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<string> InvokeHandleClientAsync(PipeRpcWorker worker, string payload)
    {
        var pipeName = $"test_pipe_{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var method = typeof(PipeRpcWorker).GetMethod(
            "HandleClientAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cts.Token);
            await (Task)method.Invoke(worker, [server, cts.Token])!;
        });

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );
        await client.ConnectAsync(5000, cts.Token);

        var bytes = Encoding.UTF8.GetBytes(payload);
        await client.WriteAsync(bytes, 0, bytes.Length, cts.Token);
        await client.FlushAsync(cts.Token);

        await serverTask;
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await client.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            if (read == 0)
            {
                break;
            }

            ms.Write(buffer, 0, read);
            if (read < buffer.Length)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task<string> InvokeHandlePayloadThroughBackgroundWorkerAsync(
        PipeRpcWorker worker,
        string pipeName,
        string payload
    )
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await worker.StartAsync(timeout.Token);

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            );

            await client.ConnectAsync(5000, timeout.Token);
            client.ReadMode = PipeTransmissionMode.Message;

            var requestBytes = Encoding.UTF8.GetBytes(payload);
            await client.WriteAsync(requestBytes, 0, requestBytes.Length, timeout.Token);
            await client.FlushAsync(timeout.Token);

            using var ms = new MemoryStream();
            var buffer = new byte[4096];

            do
            {
                var read = await client.ReadAsync(buffer, 0, buffer.Length, timeout.Token);
                if (read == 0)
                {
                    break;
                }

                ms.Write(buffer, 0, read);
            } while (!client.IsMessageComplete);

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class DelayedStatusBridgeRepository : IBridgeRepository
    {
        private readonly FakeScanStateRepository inner = new();
        private readonly TaskCompletionSource<bool> snapshotRequested = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly TaskCompletionSource<ScanStateSnapshot> snapshot = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public Task SnapshotRequested => snapshotRequested.Task;

        public void ReleaseSnapshot(ScanStateSnapshot? value = null)
        {
            snapshot.TrySetResult(value ?? new ScanStateSnapshot(null, null, null));
        }

        public Task InitializeAsync() => inner.InitializeAsync();

        public Task TouchScanStateAsync(string key, DateTimeOffset value) =>
            inner.TouchScanStateAsync(key, value);

        public Task<DateTimeOffset?> GetScanStateAsync(string key) => inner.GetScanStateAsync(key);

        public Task UpsertMessageAsync(string entryId, string? storeId, MessageDto message) =>
            inner.UpsertMessageAsync(entryId, storeId, message);

        public Task<IReadOnlyList<MessageDto>> ListRecentMessagesAsync(
            DateTimeOffset sinceUtc,
            int limit
        ) => inner.ListRecentMessagesAsync(sinceUtc, limit);

        public Task<IReadOnlyList<MessageDto>> ListRecentMeetingRequestsAsync(
            DateTimeOffset sinceUtc,
            int limit
        ) => inner.ListRecentMeetingRequestsAsync(sinceUtc, limit);

        public Task<MessageDto?> GetMessageAsync(string bridgeId) =>
            inner.GetMessageAsync(bridgeId);

        public Task UpsertEventAsync(
            string entryId,
            string? storeId,
            string? globalAppointmentId,
            EventDto evt
        ) => inner.UpsertEventAsync(entryId, storeId, globalAppointmentId, evt);

        public Task<IReadOnlyList<EventDto>> ListCalendarWindowAsync(
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            int limit
        ) => inner.ListCalendarWindowAsync(startUtc, endUtc, limit);

        public Task<EventDto?> GetEventAsync(string bridgeId) => inner.GetEventAsync(bridgeId);

        public Task<ScanStateSnapshot> GetScanStateSnapshotAsync()
        {
            snapshotRequested.TrySetResult(true);
            return snapshot.Task;
        }
    }
}
