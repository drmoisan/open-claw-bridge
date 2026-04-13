namespace OpenClaw.HostAdapter.Contracts;

/// <summary>
/// Wraps a collection payload returned by a HostAdapter list endpoint.
/// </summary>
/// <typeparam name="T">The item type contained in the response.</typeparam>
public sealed record ItemsResponse<T>(IReadOnlyList<T> Items);
