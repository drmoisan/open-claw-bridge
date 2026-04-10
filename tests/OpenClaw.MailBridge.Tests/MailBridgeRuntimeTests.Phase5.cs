using System.Security.Principal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

public partial class MailBridgeRuntimeTests
{
    [TestMethod]
    public void EnsureOutlook_should_set_waiting_for_outlook_when_AutostartOutlook_is_false()
    {
        var settings = BridgeSettings.Default with { AutostartOutlook = false };
        var state = new BridgeStateStore(settings);
        var scanner = new OutlookScanner(
            settings,
            state,
            NullLogger<OutlookScanner>.Instance,
            new FakeComActiveObject(),
            _ => 0,
            () => DateTimeOffset.UtcNow
        );

        scanner.EnsureOutlook();

        state.State.Should().Be(BridgeState.waiting_for_outlook);
        state.OutlookConnected.Should().BeFalse();
    }

    [TestMethod]
    public async Task OutlookScanner_ScanAsync_should_set_CacheStale_and_StaleReason_after_scan_failure()
    {
        var settings = BridgeSettings.Default;
        var state = new BridgeStateStore(settings);
        var scanner = new OutlookScanner(
            settings,
            state,
            NullLogger<OutlookScanner>.Instance,
            new FakeComActiveObject { ThrowOnCreate = true },
            _ => 0,
            () => DateTimeOffset.UtcNow
        );

        await scanner.ScanAsync(new FakeScanStateRepository());

        state.CacheStale.Should().BeTrue();
        state.StaleReason.Should().Be("scan_failure");
    }

    [TestMethod]
    public async Task ScanWorker_ExecuteAsync_should_honor_both_poll_settings_and_invoke_the_executor()
    {
        var repo = new FakeScanStateRepository();
        var scanner = new FakeOutlookScanner();
        var sta = new FakeStaExecutor();
        var worker = new ScanWorker(
            sta,
            scanner,
            repo,
            BridgeSettings.Default with
            {
                InboxPollSeconds = 1,
                CalendarPollSeconds = 5,
            }
        );

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        await worker.StopAsync(CancellationToken.None);

        scanner.InboxCalls.Should().BeGreaterThan(scanner.CalendarCalls);
        sta.Calls.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task CacheRepository_should_upsert_a_message_row_by_stable_item_identity()
    {
        using var repo = new CacheRepository(
            $"Data Source=upsert-msg-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var bridgeId = BridgeIdCodec.MessageId("entry-1", false);

        await repo.UpsertMessageAsync(
            "entry-1",
            "store-1",
            new MessageDto(
                bridgeId,
                "mail",
                "Before",
                DateTimeOffset.Parse("2026-04-07T00:00:00Z"),
                null,
                null,
                null,
                false,
                false,
                "IPM.Note",
                null,
                null,
                null,
                null,
                null,
                false,
                false
            )
        );

        await repo.UpsertMessageAsync(
            "entry-1",
            "store-1",
            new MessageDto(
                bridgeId,
                "mail",
                "After",
                DateTimeOffset.Parse("2026-04-07T00:00:00Z"),
                null,
                null,
                null,
                true,
                false,
                "IPM.Note",
                null,
                null,
                null,
                null,
                null,
                false,
                false
            )
        );

        var rows = await repo.ListRecentMessagesAsync(
            DateTimeOffset.Parse("2026-04-06T00:00:00Z"),
            10
        );

        rows.Should().ContainSingle();
        rows[0].Subject.Should().Be("After");
    }

    [TestMethod]
    public async Task CacheRepository_should_return_calendar_window_rows_in_deterministic_order_with_limit_enforcement()
    {
        using var repo = new CacheRepository(
            $"Data Source=cal-window-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
        );
        await repo.InitializeAsync();
        var baseStart = DateTimeOffset.Parse("2026-04-07T08:00:00Z");

        foreach (var offset in new[] { 2, 0, 1 })
        {
            var start = baseStart.AddHours(offset);
            await repo.UpsertEventAsync(
                $"entry-{offset}",
                "store-1",
                $"gid-{offset}",
                new EventDto(
                    BridgeIdCodec.EventId($"gid-{offset}", $"entry-{offset}", start),
                    $"gid-{offset}",
                    $"Event {offset}",
                    start,
                    start.AddHours(1),
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    false
                )
            );
        }

        var rows = await repo.ListCalendarWindowAsync(baseStart, baseStart.AddHours(4), 2);

        rows.Select(x => x.Subject).Should().Equal("Event 0", "Event 1");
    }

    [TestMethod]
    public async Task PipeRpcWorker_BuildResponseAsync_should_return_INVALID_REQUEST_for_malformed_json()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        var response = await worker.BuildResponseAsync("{ definitely not json }");

        response.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
    }

    [TestMethod]
    public async Task PipeRpcWorker_ExecuteAsync_should_keep_an_accepted_client_alive_through_response_write()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This test requires Windows named pipes.");
        }

        var previousResolver = PipeRpcWorker.AccountSidResolver;

        try
        {
            PipeRpcWorker.AccountSidResolver = _ =>
                WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("Current user SID is unavailable.");

            var settings = BridgeSettings.Default with
            {
                PipeName = $"mailbridge-runtime-{Guid.NewGuid():N}",
            };
            var repo = new DelayedStatusBridgeRepository();
            var worker = new PipeRpcWorker(
                settings,
                new BridgeStateStore(settings),
                repo,
                NullLogger<PipeRpcWorker>.Instance
            );
            var requestTask = InvokeHandlePayloadThroughBackgroundWorkerAsync(
                worker,
                settings.PipeName,
                $"{{\"id\":\"1\",\"method\":\"{BridgeMethods.GetStatus}\",\"params\":null}}"
            );

            await repo.SnapshotRequested.WaitAsync(TimeSpan.FromSeconds(5));
            repo.ReleaseSnapshot();

            var response = await requestTask;

            response.Should().Contain("\"ok\":true");
            response.Should().Contain("\"state\"");
        }
        finally
        {
            PipeRpcWorker.AccountSidResolver = previousResolver;
        }
    }

    [TestMethod]
    public async Task PipeRpcWorker_Handle_should_return_INVALID_REQUEST_for_invalid_calendar_ranges_or_limits()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        var response = await worker.Handle(
            new RpcRequest(
                "1",
                BridgeMethods.ListCalendarWindow,
                new Dictionary<string, string>
                {
                    ["start"] = "2026-04-08T00:00:00Z",
                    ["end"] = "2026-04-07T00:00:00Z",
                    ["limit"] = "99999",
                }
            )
        );

        response.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
    }

    [TestMethod]
    public async Task PipeRpcWorker_Handle_should_return_NOT_FOUND_for_unknown_bridge_id()
    {
        var worker = new PipeRpcWorker(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance
        );

        var response = await worker.Handle(
            new RpcRequest(
                "1",
                BridgeMethods.GetMessage,
                new Dictionary<string, string>
                {
                    ["id"] = BridgeIdCodec.MessageId("missing-entry", false),
                }
            )
        );

        response.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be(BridgeErrorCodes.NotFound);
    }

    [TestMethod]
    public void PipeRpcWorker_BuildPipeSecurity_should_fail_when_openclaw_svc_resolution_cannot_complete()
    {
        var previousResolver = PipeRpcWorker.AccountSidResolver;
        PipeRpcWorker.AccountSidResolver = _ => throw new IdentityNotMappedException();

        try
        {
            var worker = new PipeRpcWorker(
                BridgeSettings.Default,
                new BridgeStateStore(BridgeSettings.Default),
                new FakeScanStateRepository(),
                NullLogger<PipeRpcWorker>.Instance
            );

            if (!OperatingSystem.IsWindows())
            {
                Action act = () => worker.BuildPipeSecurity();
                act.Should().Throw<PlatformNotSupportedException>();
                return;
            }

            Action buildSecurity = () => worker.BuildPipeSecurity();
            buildSecurity.Should().Throw<IdentityNotMappedException>();
        }
        finally
        {
            PipeRpcWorker.AccountSidResolver = previousResolver;
        }
    }
}
