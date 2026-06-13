using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterEndpointTests
{
    [TestMethod]
    public void HostAdapter_should_deserialize_list_payloads_from_bridge_items_objects()
    {
        var element = JsonSerializer.SerializeToElement(
            new
            {
                items = new[]
                {
                    new MessageDto(
                        "msg-1",
                        "mail",
                        "Subject",
                        DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T12:55:00Z"),
                        1,
                        0,
                        true,
                        false,
                        "IPM.Note",
                        null,
                        null,
                        null,
                        null,
                        null,
                        true,
                        true
                    ),
                },
            }
        );

        var response = Program.DeserializeItemsResponse<MessageDto>(element);

        response
            .Should()
            .BeEquivalentTo(
                new ItemsResponse<MessageDto>([
                    new MessageDto(
                        "msg-1",
                        "mail",
                        "Subject",
                        DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                        DateTimeOffset.Parse("2026-04-12T12:55:00Z"),
                        1,
                        0,
                        true,
                        false,
                        "IPM.Note",
                        null,
                        null,
                        null,
                        null,
                        null,
                        true,
                        true
                    ),
                ])
            );
    }

    [TestMethod]
    public async Task HostAdapter_should_pass_url_decoded_bridge_id_through_unchanged_for_message_and_event_detail_routes()
    {
        using var factory = new HostAdapterTestWebApplicationFactory();
        var readyBridge = new BridgeStatusDto(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );
        var expectedBridgeId = "bridge id+value";
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(readyBridge, "status-request", "test-version", readyBridge)
        );
        factory.ProcessRunner.EnqueueResponse(
            "get-message",
            HostAdapterResponses.Success(
                new MessageDto(
                    expectedBridgeId,
                    "message",
                    "Subject",
                    null,
                    null,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    true
                ),
                "message-request",
                "test-version",
                readyBridge
            )
        );
        factory.ProcessRunner.EnqueueResponse(
            "get-event",
            HostAdapterResponses.Success(
                new EventDto(
                    expectedBridgeId,
                    null,
                    "Event",
                    DateTimeOffset.Parse("2026-04-12T13:00:00Z"),
                    DateTimeOffset.Parse("2026-04-12T14:00:00Z"),
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
                    true
                ),
                "event-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        using var messageResponse = await client.GetAsync("/users/me/messages/bridge%20id%2Bvalue");
        using var eventResponse = await client.GetAsync("/users/me/events/bridge%20id%2Bvalue");

        messageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        eventResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "get-message", "get-event");
        factory
            .ProcessRunner.Invocations[1]
            .Arguments.Should()
            .ContainInOrder("get-message", "--id", expectedBridgeId);
        factory
            .ProcessRunner.Invocations[2]
            .Arguments.Should()
            .ContainInOrder("get-event", "--id", expectedBridgeId);
    }

    [TestMethod]
    public async Task MailboxSettings_returns_configured_values()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory(
            new Dictionary<string, string?>
            {
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:TimeZoneId"] =
                    "Pacific Standard Time",
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingDaysOfWeek:0"] =
                    "Tuesday",
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingDaysOfWeek:1"] =
                    "Thursday",
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingHoursStart"] = "08:30",
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingHoursEnd"] = "16:45",
            }
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/mailboxSettings");
        var envelope = await ReadEnvelopeAsync<MailboxSettingsDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().NotBeNull();
        envelope.Data!.TimeZoneId.Should().Be("Pacific Standard Time");
        envelope.Data.WorkingDays.Should().Equal(DayOfWeek.Tuesday, DayOfWeek.Thursday);
        envelope.Data.WorkingHoursStart.Should().Be(new TimeOnly(8, 30));
        envelope.Data.WorkingHoursEnd.Should().Be(new TimeOnly(16, 45));
        // This route is config-sourced and must not invoke the CLI process runner.
        factory.ProcessRunner.InvocationCount.Should().Be(0);
    }

    [TestMethod]
    public async Task MailboxSettings_returns_documented_defaults_when_section_absent()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/mailboxSettings");
        var envelope = await ReadEnvelopeAsync<MailboxSettingsDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Data.Should().NotBeNull();
        envelope.Data!.TimeZoneId.Should().Be("UTC");
        envelope
            .Data.WorkingDays.Should()
            .Equal(
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            );
        envelope.Data.WorkingHoursStart.Should().Be(new TimeOnly(9, 0));
        envelope.Data.WorkingHoursEnd.Should().Be(new TimeOnly(17, 0));
        factory.ProcessRunner.InvocationCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetSchedule_returns_busy_intervals_for_non_free_events()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var readyBridge = ReadyBridge();
        EnqueueStatus(factory, readyBridge);
        var busyStart = DateTimeOffset.Parse("2026-06-15T09:00:00Z");
        var busyEnd = DateTimeOffset.Parse("2026-06-15T10:00:00Z");
        var nullStatusStart = DateTimeOffset.Parse("2026-06-15T11:00:00Z");
        var nullStatusEnd = DateTimeOffset.Parse("2026-06-15T12:00:00Z");
        var freeStart = DateTimeOffset.Parse("2026-06-15T13:00:00Z");
        var freeEnd = DateTimeOffset.Parse("2026-06-15T14:00:00Z");
        factory.ProcessRunner.EnqueueResponse(
            "list-calendar",
            HostAdapterResponses.Success(
                new ItemsResponse<EventDto>([
                    Event(busyStart, busyEnd, busyStatus: 2),
                    Event(nullStatusStart, nullStatusEnd, busyStatus: null),
                    Event(freeStart, freeEnd, busyStatus: 0),
                ]),
                "schedule-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync(
            "/users/me/calendar/getSchedule?startDateTime=2026-06-15T00:00:00Z&endDateTime=2026-06-20T00:00:00Z"
        );
        var envelope = await ReadEnvelopeAsync<FreeBusyScheduleDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().NotBeNull();
        envelope.Data!.MailboxUpn.Should().Be("me");
        // The busy (2) and null-status (treated as busy) events are included; the free (0)
        // event is excluded.
        envelope
            .Data.BusyIntervals.Should()
            .Equal(
                new BusyIntervalDto(busyStart, busyEnd),
                new BusyIntervalDto(nullStatusStart, nullStatusEnd)
            );
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-calendar");
    }

    [TestMethod]
    public async Task GetSchedule_returns_empty_intervals_for_empty_window()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var readyBridge = ReadyBridge();
        EnqueueStatus(factory, readyBridge);
        factory.ProcessRunner.EnqueueResponse(
            "list-calendar",
            HostAdapterResponses.Success(
                new ItemsResponse<EventDto>([]),
                "schedule-request",
                "test-version",
                readyBridge
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync(
            "/users/me/calendar/getSchedule?startDateTime=2026-06-15T00:00:00Z&endDateTime=2026-06-20T00:00:00Z"
        );
        var envelope = await ReadEnvelopeAsync<FreeBusyScheduleDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().NotBeNull();
        envelope.Data!.BusyIntervals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MailboxSettings_returns_configuration_error_for_unrecognized_day_name()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory(
            new Dictionary<string, string?>
            {
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingDaysOfWeek:0"] =
                    "Funday",
            }
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/mailboxSettings");
        var envelope = await ReadEnvelopeAsync<MailboxSettingsDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        envelope.Ok.Should().BeFalse();
        envelope.Error.Should().NotBeNull();
        envelope.Error!.Code.Should().Be("CONFIGURATION_ERROR");
        envelope.Error.Message.Should().Contain("WorkingDaysOfWeek");
        factory.ProcessRunner.InvocationCount.Should().Be(0);
    }

    [TestMethod]
    public async Task MailboxSettings_returns_configuration_error_for_malformed_working_hours()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory(
            new Dictionary<string, string?>
            {
                [$"{HostAdapterOptions.SectionName}:MailboxSettings:WorkingHoursStart"] =
                    "not-a-time",
            }
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync("/users/me/mailboxSettings");
        var envelope = await ReadEnvelopeAsync<MailboxSettingsDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        envelope.Ok.Should().BeFalse();
        envelope.Error.Should().NotBeNull();
        envelope.Error!.Code.Should().Be("CONFIGURATION_ERROR");
        envelope.Error.Message.Should().Contain("WorkingHours");
        factory.ProcessRunner.InvocationCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GetSchedule_propagates_downstream_failure_from_calendar_fetch()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var readyBridge = ReadyBridge();
        EnqueueStatus(factory, readyBridge);
        factory.ProcessRunner.EnqueueResponse(
            "list-calendar",
            HostAdapterResponses.Failure<ItemsResponse<EventDto>>(
                502,
                "schedule-request",
                "test-version",
                "DOWNSTREAM_FAILURE",
                "The bridge calendar fetch failed.",
                readyBridge,
                retryable: true,
                cliExitCode: 3
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.GetAsync(
            "/users/me/calendar/getSchedule?startDateTime=2026-06-15T00:00:00Z&endDateTime=2026-06-20T00:00:00Z"
        );
        var envelope = await ReadEnvelopeAsync<FreeBusyScheduleDto>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        envelope.Ok.Should().BeFalse();
        envelope.Error.Should().NotBeNull();
        envelope.Error!.Code.Should().Be("DOWNSTREAM_FAILURE");
        envelope.Data.Should().BeNull();
        factory
            .ProcessRunner.Invocations.Select(invocation => invocation.Verb)
            .Should()
            .Equal("status", "list-calendar");
    }

    private static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiEnvelope<T>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        )!;
    }

    private static BridgeStatusDto ReadyBridge() =>
        new(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );

    private static void EnqueueStatus(
        HostAdapterTestWebApplicationFactory factory,
        BridgeStatusDto readyBridge
    ) =>
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(readyBridge, "status-request", "test-version", readyBridge)
        );

    private static EventDto Event(DateTimeOffset start, DateTimeOffset end, int? busyStatus) =>
        new(
            BridgeId: "evt",
            GlobalAppointmentId: null,
            Subject: "Event",
            StartUtc: start,
            EndUtc: end,
            Location: null,
            BusyStatus: busyStatus,
            MeetingStatus: null,
            IsRecurring: false,
            Sensitivity: null,
            Organizer: null,
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: null,
            ProtectedFieldsAvailable: false,
            IsRedacted: true
        );
}
