namespace OpenClaw.Core.Agent;

/// <summary>
/// A single meeting attendee in the Graph-shaped scheduling contract (D6). Mirrors
/// the <c>name</c>/<c>email</c> pair of a Graph attendee address.
/// </summary>
/// <param name="Name">The attendee display name, or empty when unknown.</param>
/// <param name="Email">The attendee email address (normalized downstream).</param>
public sealed record AttendeeDto(string Name, string Email);
