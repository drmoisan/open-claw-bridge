using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class BridgeContractsCoverageTests
{
    [TestMethod]
    public void Bridge_methods_all_should_include_every_declared_method_constant()
    {
        BridgeMethods
            .All.Should()
            .Contain([
                BridgeMethods.GetStatus,
                BridgeMethods.ListRecentMessages,
                BridgeMethods.GetMessage,
                BridgeMethods.ListRecentMeetingRequests,
                BridgeMethods.ListCalendarWindow,
                BridgeMethods.GetEvent,
                BridgeMethods.GetEventForMessage,
                BridgeMethods.SendMail,
            ]);
    }

    [TestMethod]
    public void Bridge_methods_all_should_contain_send_mail_verb()
    {
        BridgeMethods.SendMail.Should().Be("send_mail");
        BridgeMethods.All.Contains(BridgeMethods.SendMail).Should().BeTrue();
    }

    [TestMethod]
    public void Bridge_methods_all_should_contain_get_event_for_message_verb()
    {
        BridgeMethods.GetEventForMessage.Should().Be("get_event_for_message");
        BridgeMethods.All.Contains(BridgeMethods.GetEventForMessage).Should().BeTrue();
    }

    [TestMethod]
    public void Rpc_response_success_and_failure_should_set_expected_shape()
    {
        var success = RpcResponse.Success("id-1", new { value = 7 });
        success.Id.Should().Be("id-1");
        success.Ok.Should().BeTrue();
        success.Error.Should().BeNull();

        var failure = RpcResponse.Failure("id-2", BridgeErrorCodes.InvalidRequest, "bad");
        failure.Id.Should().Be("id-2");
        failure.Ok.Should().BeFalse();
        failure.Error.Should().NotBeNull();
        failure.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        failure.Error.Message.Should().Be("bad");
    }

    [TestMethod]
    public void Contracts_records_should_hold_expected_values()
    {
        var req = new RpcRequest(
            "abc",
            BridgeMethods.GetStatus,
            new Dictionary<string, string> { ["k"] = "v" }
        );
        req.Id.Should().Be("abc");
        req.Method.Should().Be(BridgeMethods.GetStatus);
        req.Params!["k"].Should().Be("v");

        var now = DateTimeOffset.UtcNow;
        var status = new BridgeStatusDto("ready", "safe", true, false, "ok", now, now);
        status.State.Should().Be("ready");
        status.Mode.Should().Be("safe");
        status.OutlookConnected.Should().BeTrue();
        status.CacheStale.Should().BeFalse();
        status.StaleReason.Should().Be("ok");
        status.LastInboxScanUtc.Should().Be(now);
        status.LastCalendarScanUtc.Should().Be(now);

        var message = new MessageDto(
            "m1",
            "message",
            "subj",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            2,
            true,
            false,
            "IPM.Note",
            "sender",
            "sender@example.com",
            "[]",
            "[]",
            "preview",
            true,
            false
        );
        message.BridgeId.Should().Be("m1");
        message.ItemKind.Should().Be("message");
        message.Subject.Should().Be("subj");
        message.Importance.Should().Be(1);
        message.Sensitivity.Should().Be(2);
        message.Unread.Should().BeTrue();
        message.HasAttachments.Should().BeFalse();
        message.MessageClass.Should().Be("IPM.Note");
        message.SenderName.Should().Be("sender");
        message.SenderEmail.Should().Be("sender@example.com");
        message.ToJson.Should().Be("[]");
        message.CcJson.Should().Be("[]");
        message.BodyPreview.Should().Be("preview");
        message.ProtectedFieldsAvailable.Should().BeTrue();
        message.IsRedacted.Should().BeFalse();
        message.LinkedGlobalAppointmentId.Should().BeNull();

        var linkedMessage = message with { LinkedGlobalAppointmentId = "clean-goid" };
        linkedMessage.LinkedGlobalAppointmentId.Should().Be("clean-goid");

        var evt = new EventDto(
            "e1",
            "gid",
            "Meeting",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            "Room",
            1,
            1,
            false,
            1,
            "org",
            "[]",
            "[]",
            "[]",
            "preview",
            true,
            false
        );
        evt.BridgeId.Should().Be("e1");
        evt.GlobalAppointmentId.Should().Be("gid");
        evt.Subject.Should().Be("Meeting");
        evt.Location.Should().Be("Room");
        evt.BusyStatus.Should().Be(1);
        evt.MeetingStatus.Should().Be(1);
        evt.IsRecurring.Should().BeFalse();
        evt.Sensitivity.Should().Be(1);
        evt.Organizer.Should().Be("org");
        evt.RequiredAttendeesJson.Should().Be("[]");
        evt.OptionalAttendeesJson.Should().Be("[]");
        evt.ResourcesJson.Should().Be("[]");
        evt.BodyPreview.Should().Be("preview");
        evt.ProtectedFieldsAvailable.Should().BeTrue();
        evt.IsRedacted.Should().BeFalse();
    }

    [TestMethod]
    public void Bridge_settings_default_should_satisfy_validator()
    {
        var settings = BridgeSettings.Default;
        settings.Mode.Should().Be("safe");
        settings.PipeName.Should().NotBeNullOrWhiteSpace();
        BridgeSettingsValidator.Validate(settings).Should().BeEmpty();
    }

    [TestMethod]
    public void Bridge_error_codes_should_be_stable()
    {
        BridgeErrorCodes.InvalidRequest.Should().Be("INVALID_REQUEST");
        BridgeErrorCodes.Unauthorized.Should().Be("UNAUTHORIZED");
        BridgeErrorCodes.OutlookUnavailable.Should().Be("OUTLOOK_UNAVAILABLE");
        BridgeErrorCodes.NotFound.Should().Be("NOT_FOUND");
        BridgeErrorCodes.InternalError.Should().Be("INTERNAL_ERROR");
        BridgeErrorCodes.PayloadTooLarge.Should().Be("PAYLOAD_TOO_LARGE");
    }

    [TestMethod]
    public void Bridge_enums_should_expose_expected_members()
    {
        Enum.GetNames<BridgeState>()
            .Should()
            .Contain(["starting", "waiting_for_outlook", "ready", "degraded", "error"]);
        Enum.GetNames<BridgeMode>().Should().Contain(["safe", "enhanced"]);
    }
}
