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
            }
            : message with
            {
                BodyPreview = null,
                SenderName = null,
                SenderEmail = null,
                SenderEmailResolved = null,
                FromEmailAddress = null,
                ToJson = null,
                CcJson = null,
                ProtectedFieldsAvailable = false,
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
        // Safe mode nulls BodyFull alongside BodyPreview to preserve suppression parity, otherwise
        // the full body text would leak in safe mode. Issues #18 x #20: IsRedacted is exclusively
        // the sensitivity-redaction signal written at normalization time — neither branch touches
        // it — while ProtectedFieldsAvailable = false is the safe-mode suppression signal. Safe
        // mode suppresses the full protected field set (body, attendee JSON, Organizer) and
        // empties Categories; Location is retained (decided behavior, spec section B). Null is
        // the suppression signal for the JSON fields, distinct from the empty array "[]" the
        // scanner emits for a type with no attendees.
        return enhancedMode
            ? evt with
            {
                BodyPreview = preview,
            }
            : evt with
            {
                BodyPreview = null,
                BodyFull = null,
                RequiredAttendeesJson = null,
                OptionalAttendeesJson = null,
                ResourcesJson = null,
                Organizer = null,
                Categories = Array.Empty<string>(),
                ProtectedFieldsAvailable = false,
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
