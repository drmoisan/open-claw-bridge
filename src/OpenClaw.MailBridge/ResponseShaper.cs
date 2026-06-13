using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Applies safe-mode redaction and enhanced-mode preview shaping to cached bridge DTOs.
/// </summary>
internal static class ResponseShaper
{
    public static MessageDto ShapeMessage(MessageDto message, BridgeSettings settings)
    {
        var enhancedMode = string.Equals(
            settings.Mode,
            "enhanced",
            StringComparison.OrdinalIgnoreCase
        );
        var preview = ShapePreview(message.BodyPreview, settings);

        return enhancedMode
            ? message with
            {
                BodyPreview = preview,
                IsRedacted = false,
            }
            : message with
            {
                BodyPreview = null,
                SenderName = null,
                SenderEmail = null,
                IsRedacted = true,
            };
    }

    public static EventDto ShapeEvent(EventDto evt, BridgeSettings settings)
    {
        var enhancedMode = string.Equals(
            settings.Mode,
            "enhanced",
            StringComparison.OrdinalIgnoreCase
        );
        var preview = ShapePreview(evt.BodyPreview, settings);

        // Enhanced mode returns the full untruncated COM Body verbatim in BodyFull; it is NOT
        // routed through BodySanitizer.NormalizePreview (which truncates/collapses whitespace).
        // Safe mode nulls BodyFull alongside BodyPreview to preserve redaction parity, otherwise
        // the full body text would leak in safe mode. Issue #71: safe mode also nulls the three
        // attendee JSON fields (RequiredAttendeesJson/OptionalAttendeesJson/ResourcesJson) because
        // attendee names and emails are PII; this matches the message-path redaction of
        // SenderName/SenderEmail in ShapeMessage. Null here is the redaction signal, distinct from
        // the empty array "[]" the scanner emits for a type with no attendees.
        return enhancedMode
            ? evt with
            {
                BodyPreview = preview,
                IsRedacted = false,
            }
            : evt with
            {
                BodyPreview = null,
                BodyFull = null,
                RequiredAttendeesJson = null,
                OptionalAttendeesJson = null,
                ResourcesJson = null,
                IsRedacted = true,
            };
    }

    public static string? ShapePreview(string? preview, BridgeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return null;
        }

        return BodySanitizer.NormalizePreview(preview, settings.BodyPreviewMaxChars);
    }
}
