namespace OpenClaw.MailBridge;

/// <summary>
/// COM data-type adapter that maps a late-bound Outlook <c>MailItem</c>/<c>MeetingItem</c> onto the
/// model-agnostic <see cref="IMessageSource"/> contract (locked decision D-D). This is the only place
/// that reads concrete COM members for the issue-#73 resolved fields, keeping Outlook COM confined to
/// <c>OpenClaw.MailBridge</c> (architecture-boundaries rule 1).
///
/// All COM access is fail-soft: a member that cannot be read degrades to <see langword="null"/> (or
/// to the documented fallback for the sender), and every COM wrapper obtained during resolution is
/// released deterministically via <see cref="ComActiveObject.ReleaseAll"/> following the
/// <c>GetStoreId</c>/<c>ReadAttendees</c> idiom. Resolution never throws out of the adapter.
/// </summary>
internal sealed class ComMessageSource : IMessageSource
{
    /// <summary>
    /// The <c>PR_SMTP_ADDRESS</c> MAPI property tag used by the <c>PropertyAccessor</c> path to read
    /// the true SMTP address of an address entry (locked decision D-C).
    /// </summary>
    private const string SmtpAddressPropertyTag =
        "http://schemas.microsoft.com/mapi/proptag/0x39FE001E";

    private readonly object _item;
    private readonly ComActiveObject _com;
    private readonly bool _isMeeting;

    private IReadOnlyList<OutlookScanner.Attendee>? _toRecipients;
    private IReadOnlyList<OutlookScanner.Attendee>? _ccRecipients;

    /// <summary>
    /// Creates an adapter over the supplied COM message <paramref name="item"/> using
    /// <paramref name="com"/> for deterministic COM release.
    /// </summary>
    /// <param name="item">The late-bound COM <c>MailItem</c> or <c>MeetingItem</c>.</param>
    /// <param name="com">The active-object helper used to release COM wrappers.</param>
    /// <param name="isMeeting">
    /// True when the item is a meeting message, so <see cref="MeetingMessageType"/> reads the raw
    /// <c>OlMeetingType</c>; false for ordinary mail (yields null).
    /// </param>
    internal ComMessageSource(object item, ComActiveObject com, bool isMeeting)
    {
        _item = item;
        _com = com;
        _isMeeting = isMeeting;
    }

    /// <inheritdoc />
    public string? SenderEmailResolved => ResolveSenderSmtp();

    /// <inheritdoc />
    public string? FromEmailAddress => ResolveFromSmtp();

    /// <inheritdoc />
    public string? ConversationId => OutlookComHelpers.GetOptionalString(_item, "ConversationID");

    /// <inheritdoc />
    public int? MeetingMessageType =>
        _isMeeting ? OutlookComHelpers.GetOptionalInt(_item, "MeetingType") : null;

    /// <inheritdoc />
    public string? LinkedGlobalAppointmentId => ResolveLinkedGlobalAppointmentId();

    /// <inheritdoc />
    public IReadOnlyList<OutlookScanner.Attendee> ToRecipients
    {
        get
        {
            EnsureRecipients();
            return _toRecipients!;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<OutlookScanner.Attendee> CcRecipients
    {
        get
        {
            EnsureRecipients();
            return _ccRecipients!;
        }
    }

    private void EnsureRecipients()
    {
        if (_toRecipients is not null && _ccRecipients is not null)
        {
            return;
        }

        var (to, cc) = OutlookScanner.ReadMessageRecipients(_item, _com);
        _toRecipients = to;
        _ccRecipients = cc;
    }

    /// <summary>
    /// Fail-soft SMTP resolution of the sender (locked decision D-C): attempt the true SMTP address
    /// via the <c>Sender.AddressEntry</c> (<c>PropertyAccessor PR_SMTP_ADDRESS</c> or
    /// <c>GetExchangeUser().PrimarySmtpAddress</c>) inside try/catch, then fall back to
    /// <c>Sender.Address</c> -> <c>Sender.AddressEntry.Address</c> -> raw <c>SenderEmailAddress</c>.
    /// Every COM wrapper obtained is released in <c>finally</c>; never throws.
    /// </summary>
    private string? ResolveSenderSmtp()
    {
        object? sender = null;
        try
        {
            sender = OutlookComHelpers.GetOptionalMemberValue(_item, "Sender");
            var resolved = ResolveAddressEntrySmtp(sender);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }
        catch
        {
            // Fail soft: fall through to the raw value.
        }
        finally
        {
            _com.ReleaseAll(sender);
        }

        return NormalizeAddress(OutlookComHelpers.GetOptionalString(_item, "SenderEmailAddress"));
    }

    /// <summary>
    /// Fail-soft SMTP resolution of the on-behalf-of identity (locked decision D-A): SMTP-resolve the
    /// <c>SentOnBehalfOf</c> address-entry surface when a delegate identity is present, otherwise fall
    /// back to the resolved sender. Never throws.
    /// </summary>
    private string? ResolveFromSmtp()
    {
        var onBehalfOfRaw = OutlookComHelpers.GetOptionalString(
            _item,
            "SentOnBehalfOfEmailAddress"
        );
        if (string.IsNullOrWhiteSpace(onBehalfOfRaw))
        {
            return ResolveSenderSmtp();
        }

        object? sender = null;
        try
        {
            // The on-behalf-of identity shares the Sender address-entry surface in the COM model;
            // attempt the same true-SMTP resolution before degrading to the raw on-behalf-of value.
            sender = OutlookComHelpers.GetOptionalMemberValue(_item, "Sender");
            var resolved = ResolveOnBehalfOfSmtp(sender, onBehalfOfRaw);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }
        catch
        {
            // Fail soft: fall through to the raw on-behalf-of value.
        }
        finally
        {
            _com.ReleaseAll(sender);
        }

        return NormalizeAddress(onBehalfOfRaw);
    }

    /// <summary>
    /// Resolves the on-behalf-of SMTP address. When the raw on-behalf-of value already looks like an
    /// SMTP address it is used directly; otherwise the sender address-entry true-SMTP path is
    /// attempted, then the raw value is returned. Fail-soft.
    /// </summary>
    private string? ResolveOnBehalfOfSmtp(object? sender, string onBehalfOfRaw)
    {
        if (LooksLikeSmtp(onBehalfOfRaw))
        {
            return onBehalfOfRaw;
        }

        var resolved = ResolveAddressEntrySmtp(sender);
        return string.IsNullOrWhiteSpace(resolved) ? NormalizeAddress(onBehalfOfRaw) : resolved;
    }

    /// <summary>
    /// Attempts the true-SMTP resolution chain for a sender object: <c>PropertyAccessor</c>
    /// <c>PR_SMTP_ADDRESS</c> on the address entry, then <c>GetExchangeUser().PrimarySmtpAddress</c>,
    /// then <c>Sender.Address</c>/<c>AddressEntry.Address</c>. Returns null on any failure (caller
    /// falls back to the raw value). Releases every COM wrapper it obtains.
    /// </summary>
    private string? ResolveAddressEntrySmtp(object? sender)
    {
        if (sender is null)
        {
            return null;
        }

        object? addressEntry = null;
        try
        {
            addressEntry = OutlookComHelpers.GetOptionalMemberValue(sender, "AddressEntry");

            var viaPropertyAccessor = ResolveViaPropertyAccessor(addressEntry);
            if (!string.IsNullOrWhiteSpace(viaPropertyAccessor))
            {
                return viaPropertyAccessor;
            }

            var viaExchangeUser = ResolveViaExchangeUser(addressEntry);
            if (!string.IsNullOrWhiteSpace(viaExchangeUser))
            {
                return viaExchangeUser;
            }

            var senderAddress = OutlookComHelpers.GetOptionalString(sender, "Address");
            if (LooksLikeSmtp(senderAddress))
            {
                return senderAddress;
            }

            var entryAddress = addressEntry is null
                ? null
                : OutlookComHelpers.GetOptionalString(addressEntry, "Address");
            return LooksLikeSmtp(entryAddress) ? entryAddress : (entryAddress ?? senderAddress);
        }
        finally
        {
            _com.ReleaseAll(addressEntry);
        }
    }

    /// <summary>
    /// Reads the true SMTP address from the address entry via its <c>PropertyAccessor</c>
    /// (<c>PR_SMTP_ADDRESS</c>). Fail-soft: returns null on any COM error.
    /// </summary>
    private string? ResolveViaPropertyAccessor(object? addressEntry)
    {
        if (addressEntry is null)
        {
            return null;
        }

        object? propertyAccessor = null;
        try
        {
            propertyAccessor = OutlookComHelpers.GetOptionalMemberValue(
                addressEntry,
                "PropertyAccessor"
            );
            if (propertyAccessor is null)
            {
                return null;
            }

            var value = OutlookComHelpers.InvokeMember(
                propertyAccessor,
                "GetProperty",
                SmtpAddressPropertyTag
            );
            return NormalizeAddress(value?.ToString());
        }
        catch
        {
            return null;
        }
        finally
        {
            _com.ReleaseAll(propertyAccessor);
        }
    }

    /// <summary>
    /// Reads the true SMTP address via <c>AddressEntry.GetExchangeUser().PrimarySmtpAddress</c>.
    /// Fail-soft: returns null for non-Exchange entries or any COM error.
    /// </summary>
    private string? ResolveViaExchangeUser(object? addressEntry)
    {
        if (addressEntry is null)
        {
            return null;
        }

        object? exchangeUser = null;
        try
        {
            exchangeUser = OutlookComHelpers.InvokeMember(addressEntry, "GetExchangeUser");
            if (exchangeUser is null)
            {
                return null;
            }

            return NormalizeAddress(
                OutlookComHelpers.GetOptionalString(exchangeUser, "PrimarySmtpAddress")
            );
        }
        catch
        {
            return null;
        }
        finally
        {
            _com.ReleaseAll(exchangeUser);
        }
    }

    /// <summary>
    /// Fail-soft resolution of the linked appointment's Clean Global Object ID (issue #146). For a
    /// meeting item the associated appointment is obtained via <c>GetAssociatedAppointment(false)</c>
    /// and its <c>GlobalAppointmentID</c> is read; the appointment wrapper is released in
    /// <c>finally</c>. Returns <see langword="null"/> for ordinary (non-meeting) mail, when the
    /// appointment cannot be obtained, or on any COM error. Never throws.
    /// </summary>
    private string? ResolveLinkedGlobalAppointmentId()
    {
        if (!_isMeeting)
        {
            return null;
        }

        object? appointment = null;
        try
        {
            appointment = OutlookComHelpers.InvokeMember(_item, "GetAssociatedAppointment", false);
            if (appointment is null)
            {
                return null;
            }

            return NormalizeAddress(
                OutlookComHelpers.GetOptionalString(appointment, "GlobalAppointmentID")
            );
        }
        catch
        {
            // Fail soft: a non-meeting item, a request without a stored appointment, or any COM
            // fault yields the clean unlinked result.
            return null;
        }
        finally
        {
            _com.ReleaseAll(appointment);
        }
    }

    /// <summary>
    /// Heuristic SMTP-shape check: a value containing an <c>@</c> and no leading legacy-Exchange DN
    /// marker (<c>/o=</c> or <c>/O=</c>). Used to decide whether a fallback address is already SMTP.
    /// </summary>
    private static bool LooksLikeSmtp(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains('@', StringComparison.Ordinal)
        && !value.StartsWith("/o=", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes a candidate address to null when empty/whitespace, otherwise the trimmed value.
    /// </summary>
    private static string? NormalizeAddress(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
