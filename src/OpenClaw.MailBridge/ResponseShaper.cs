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

        return enhancedMode
            ? evt with
            {
                BodyPreview = preview,
                IsRedacted = false,
            }
            : evt with
            {
                BodyPreview = null,
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
