using System.Text.Json;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Parses and validates <c>send_mail</c> RPC parameters into a plain-data
/// <see cref="SendMailComRequest"/> for <see cref="IOutlookMailSender"/>. Extracted from
/// <see cref="PipeRpcWorker"/> to keep both files under the 500-line repo limit. Validation
/// applies the locked decisions: empty subject allowed (D-F); at least one recipient across
/// To/CC/BCC (D-G); <c>contentType</c> in {Text, HTML} case-insensitive; <c>save-to-sent-items</c>
/// defaults to <see langword="true"/> when absent (AC-08). Recipients arrive as JSON arrays (D-C).
/// </summary>
internal static class SendMailRpcHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parses the flat RPC params into a validated <see cref="SendMailComRequest"/>.
    /// </summary>
    /// <exception cref="SendMailValidationException">Thrown for any validation failure (-> INVALID_REQUEST).</exception>
    public static SendMailComRequest Parse(RpcRequest req)
    {
        var subject = GetOptional(req, "subject") ?? string.Empty; // empty subject allowed (D-F)
        var bodyContentType = GetRequired(req, "body-content-type");
        if (
            !string.Equals(bodyContentType, "Text", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(bodyContentType, "HTML", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new SendMailValidationException(
                "Parameter 'body-content-type' must be 'Text' or 'HTML'."
            );
        }

        var bodyContent = GetOptional(req, "body-content") ?? string.Empty;
        var to = ParseRecipients(req, "to-recipients");
        var cc = ParseRecipients(req, "cc-recipients");
        var bcc = ParseRecipients(req, "bcc-recipients");

        if (to.Count + cc.Count + bcc.Count == 0)
        {
            throw new SendMailValidationException(
                "At least one recipient across to/cc/bcc is required."
            );
        }

        var saveToSentItems = ParseSaveToSentItems(req);

        return new SendMailComRequest(
            subject,
            bodyContentType,
            bodyContent,
            to,
            cc,
            bcc,
            saveToSentItems
        );
    }

    private static bool ParseSaveToSentItems(RpcRequest req)
    {
        var raw = GetOptional(req, "save-to-sent-items");
        if (raw is null)
        {
            return true; // default true (AC-08)
        }

        if (!bool.TryParse(raw, out var parsed))
        {
            throw new SendMailValidationException(
                "Parameter 'save-to-sent-items' must be 'true' or 'false'."
            );
        }

        return parsed;
    }

    private static IReadOnlyList<string> ParseRecipients(RpcRequest req, string key)
    {
        var raw = GetOptional(req, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        List<RecipientJson>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<RecipientJson>>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SendMailValidationException(
                $"Parameter '{key}' is not a valid JSON array: {ex.Message}"
            );
        }

        if (parsed is null)
        {
            return [];
        }

        var addresses = new List<string>(parsed.Count);
        foreach (var recipient in parsed)
        {
            var address = recipient.EmailAddress?.Address;
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new SendMailValidationException(
                    $"Parameter '{key}' contains a recipient with no email address."
                );
            }

            addresses.Add(address);
        }

        return addresses;
    }

    private static string GetRequired(RpcRequest req, string key)
    {
        var value = GetOptional(req, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SendMailValidationException($"Missing required parameter '{key}'.");
        }

        return value;
    }

    private static string? GetOptional(RpcRequest req, string key) =>
        req.Params is not null && req.Params.TryGetValue(key, out var value) ? value : null;

    private sealed record RecipientJson(EmailAddressJson? EmailAddress);

    private sealed record EmailAddressJson(string? Address, string? Name);
}

/// <summary>
/// Raised when <c>send_mail</c> parameters fail validation; mapped to
/// <see cref="BridgeErrorCodes.InvalidRequest"/> by <see cref="PipeRpcWorker"/>.
/// </summary>
internal sealed class SendMailValidationException(string message) : Exception(message);
