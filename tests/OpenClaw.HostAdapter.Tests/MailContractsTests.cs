using System.Text.Json;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class MailContractsTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void SendMailRequest_should_serialize_to_graph_shaped_camelCase()
    {
        // Arrange
        var request = new SendMailRequest(
            new SendMailMessageDto(
                "Hello",
                new SendMailBodyDto("HTML", "<p>hi</p>"),
                [
                    new SendMailRecipientDto(
                        new SendMailEmailAddressDto("to@example.com", "To Person")
                    ),
                ],
                [new SendMailRecipientDto(new SendMailEmailAddressDto("cc@example.com"))]
            ),
            SaveToSentItems: false
        );

        // Act
        var json = JsonSerializer.Serialize(request, WebOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        var message = root.GetProperty("message");
        message.GetProperty("subject").GetString().Should().Be("Hello");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
        message.GetProperty("body").GetProperty("content").GetString().Should().Be("<p>hi</p>");
        var to = message.GetProperty("toRecipients");
        to[0]
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("to@example.com");
        to[0].GetProperty("emailAddress").GetProperty("name").GetString().Should().Be("To Person");
        message
            .GetProperty("ccRecipients")[0]
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("cc@example.com");
        root.GetProperty("saveToSentItems").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    public void SendMailRequest_should_round_trip_preserving_values()
    {
        // Arrange
        var original = new SendMailRequest(
            new SendMailMessageDto(
                "Subject",
                new SendMailBodyDto("Text", "body text"),
                [new SendMailRecipientDto(new SendMailEmailAddressDto("a@b.c"))],
                CcRecipients: [new SendMailRecipientDto(new SendMailEmailAddressDto("cc@b.c"))],
                BccRecipients: [new SendMailRecipientDto(new SendMailEmailAddressDto("bcc@b.c"))]
            )
        );

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<SendMailRequest>(json, WebOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Should().BeEquivalentTo(original);
        deserialized!.SaveToSentItems.Should().BeTrue();
    }

    [TestMethod]
    public void SendMailRequest_with_bcc_only_should_round_trip()
    {
        // Arrange
        var original = new SendMailRequest(
            new SendMailMessageDto(
                string.Empty,
                new SendMailBodyDto("Text", "x"),
                ToRecipients: [],
                BccRecipients: [new SendMailRecipientDto(new SendMailEmailAddressDto("bcc@b.c"))]
            )
        );

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<SendMailRequest>(json, WebOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Message.ToRecipients.Should().BeEmpty();
        deserialized.Message.BccRecipients.Should().ContainSingle();
        deserialized.Message.BccRecipients![0].EmailAddress.Address.Should().Be("bcc@b.c");
    }

    [TestMethod]
    public void SaveToSentItems_should_default_to_true_when_absent_from_json()
    {
        // Arrange
        const string json = """
            {
              "message": {
                "subject": "s",
                "body": { "contentType": "Text", "content": "c" },
                "toRecipients": [ { "emailAddress": { "address": "a@b.c" } } ]
              }
            }
            """;

        // Act
        var deserialized = JsonSerializer.Deserialize<SendMailRequest>(json, WebOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SaveToSentItems.Should().BeTrue();
        deserialized.Message.Subject.Should().Be("s");
        deserialized.Message.CcRecipients.Should().BeNull();
        deserialized.Message.BccRecipients.Should().BeNull();
    }
}
