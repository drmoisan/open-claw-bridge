namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Access-recording COM item doubles for the issue-#18 never-ingest assertions (spec Group A).
/// Modeled on the reflection-readable fakes in <c>MailBridgeRuntimeTestDoubles.cs</c>: the scanner
/// reads members late-bound by name, so each protected member getter records (and can optionally
/// fail) the access, letting a test prove that normalization never touched protected content for a
/// sensitive item. Mechanical members (ids, dates, flags, <c>Sensitivity</c>) are plain properties
/// because the scanner is expected to read them for every item.
/// </summary>
internal sealed class AccessRecordingSensitiveMailItem
{
    private readonly List<string> _protectedAccesses = new();

    private readonly string? _body = "Secret body content";
    private readonly string? _senderName = "Secret Sender";
    private readonly string? _senderEmailAddress = "secret.sender@example.com";
    private readonly string? _senderEmailType = "SMTP";
    private readonly string? _sentOnBehalfOfEmailAddress;
    private readonly object? _sender;
    private readonly object? _recipients;

    /// <summary>Ordered log of every protected member access, by COM member name.</summary>
    public IReadOnlyList<string> ProtectedAccesses => _protectedAccesses;

    /// <summary>True when any protected member was accessed.</summary>
    public bool ProtectedMemberWasAccessed => _protectedAccesses.Count > 0;

    /// <summary>
    /// When true, any protected member access throws in addition to being recorded, modeling a
    /// hard never-ingest guarantee (the scanner's fail-soft helpers may swallow the throw; the
    /// recorded access remains the assertable signal).
    /// </summary>
    public bool ThrowOnProtectedAccess { get; init; }

    // Mechanical members — safe to read for any sensitivity value.
    public string EntryID { get; init; } = "entry-sensitive-mail";
    public string MessageClass { get; init; } = "IPM.Note";
    public string? Subject { get; init; } = "Secret subject";
    public DateTimeOffset ReceivedTime { get; init; }
    public DateTimeOffset SentOn { get; init; }
    public int Importance { get; init; }
    public int? Sensitivity { get; init; }
    public bool Unread { get; init; }
    public bool HasAttachments { get; init; }
    public string? ConversationID { get; init; } = "conv-sensitive";
    public FakeOutlookParent Parent { get; init; } = new();

    // Protected members — every read is recorded.
    public string? Body
    {
        get => Record("Body", _body);
        init => _body = value;
    }

    public string? SenderName
    {
        get => Record("SenderName", _senderName);
        init => _senderName = value;
    }

    public string? SenderEmailAddress
    {
        get => Record("SenderEmailAddress", _senderEmailAddress);
        init => _senderEmailAddress = value;
    }

    public string? SenderEmailType
    {
        get => Record("SenderEmailType", _senderEmailType);
        init => _senderEmailType = value;
    }

    public string? SentOnBehalfOfEmailAddress
    {
        get => Record("SentOnBehalfOfEmailAddress", _sentOnBehalfOfEmailAddress);
        init => _sentOnBehalfOfEmailAddress = value;
    }

    public object? Sender
    {
        get => Record("Sender", _sender);
        init => _sender = value;
    }

    public object? Recipients
    {
        get => Record("Recipients", _recipients);
        init => _recipients = value;
    }

    private T Record<T>(string member, T value)
    {
        _protectedAccesses.Add(member);
        if (ThrowOnProtectedAccess)
        {
            throw new InvalidOperationException(
                $"Protected member '{member}' was accessed on a sensitive mail item."
            );
        }

        return value;
    }
}

/// <summary>
/// Access-recording appointment-item double for the issue-#18 never-ingest assertions: protected
/// event members (<c>Body</c>, <c>Organizer</c>, <c>Recipients</c>, <c>Location</c>,
/// <c>Categories</c>) record any access; scheduling-mechanical members are plain properties.
/// </summary>
internal sealed class AccessRecordingSensitiveAppointmentItem
{
    private readonly List<string> _protectedAccesses = new();

    private readonly string? _body = "Secret agenda";
    private readonly string? _organizer = "Secret Organizer";
    private readonly string? _location = "Secret Room";
    private readonly string? _categories = "Secret Category";
    private readonly object? _recipients;

    /// <summary>Ordered log of every protected member access, by COM member name.</summary>
    public IReadOnlyList<string> ProtectedAccesses => _protectedAccesses;

    /// <summary>True when any protected member was accessed.</summary>
    public bool ProtectedMemberWasAccessed => _protectedAccesses.Count > 0;

    /// <summary>See <see cref="AccessRecordingSensitiveMailItem.ThrowOnProtectedAccess"/>.</summary>
    public bool ThrowOnProtectedAccess { get; init; }

    // Mechanical members — safe to read for any sensitivity value.
    public string EntryID { get; init; } = "entry-sensitive-appt";
    public string? GlobalAppointmentID { get; init; } = "gid-sensitive-appt";
    public string? Subject { get; init; } = "Secret meeting subject";
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public bool IsRecurring { get; init; }
    public int? Sensitivity { get; init; }
    public int? BusyStatus { get; init; }
    public int? MeetingStatus { get; init; }
    public int? ResponseStatus { get; init; }
    public int? RecurrenceState { get; init; }
    public bool IsOnlineMeeting { get; init; }
    public bool AllowNewTimeProposal { get; init; }
    public DateTime? LastModificationTime { get; init; }
    public FakeOutlookParent Parent { get; init; } = new();

    // Protected members — every read is recorded.
    public string? Body
    {
        get => Record("Body", _body);
        init => _body = value;
    }

    public string? Organizer
    {
        get => Record("Organizer", _organizer);
        init => _organizer = value;
    }

    public string? Location
    {
        get => Record("Location", _location);
        init => _location = value;
    }

    public string? Categories
    {
        get => Record("Categories", _categories);
        init => _categories = value;
    }

    public object? Recipients
    {
        get => Record("Recipients", _recipients);
        init => _recipients = value;
    }

    private T Record<T>(string member, T value)
    {
        _protectedAccesses.Add(member);
        if (ThrowOnProtectedAccess)
        {
            throw new InvalidOperationException(
                $"Protected member '{member}' was accessed on a sensitive appointment item."
            );
        }

        return value;
    }
}
