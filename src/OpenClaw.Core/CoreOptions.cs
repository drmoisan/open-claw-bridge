namespace OpenClaw.Core;

public sealed class OpenClawOptions
{
    public HostAdapterOptions HostAdapter { get; set; } = new();

    public PollingOptions Polling { get; set; } = new();

    public DefaultOptions Defaults { get; set; } = new();

    public StorageOptions Storage { get; set; } = new();
}

public sealed class HostAdapterOptions
{
    public string BaseUrl { get; set; } = "http://host.docker.internal:4319/v1/";

    public string TokenFile { get; set; } = "/run/openclaw/hostadapter.token";
}

public sealed class PollingOptions
{
    public int MessagesIntervalSeconds { get; set; } = 60;

    public int MeetingRequestsIntervalSeconds { get; set; } = 60;

    public int CalendarIntervalSeconds { get; set; } = 300;

    public int MessageLookbackHours { get; set; } = 48;

    public int CalendarPastDays { get; set; } = 14;

    public int CalendarFutureDays { get; set; } = 30;
}

public sealed class DefaultOptions
{
    public int Limit { get; set; } = 100;

    public int MaxLimit { get; set; } = 250;
}

public sealed class StorageOptions
{
    public string DbPath { get; set; } = "/data/openclaw.db";
}
