namespace OpenClaw.MailBridge.Contracts;

public sealed record MailBridgeRequest(
	string Operation,
	string Payload,
	DateTimeOffset TimestampUtc);

public sealed record MailBridgeResponse(
	bool Success,
	string Message,
	string? Payload,
	DateTimeOffset TimestampUtc);

public sealed record BridgeRuntimeInfo(
	string PipeName,
	string ApartmentState,
	bool OutlookComAvailable,
	string? OutlookComTypeName,
	string SqliteVersion);
