namespace OpenClaw.MailBridge.Contracts.Models;

/// <summary>
/// Maps the Outlook <c>Sensitivity</c> integer to the Graph-shaped sensitivity label string
/// carried by <see cref="EventDto.SensitivityLabel"/>. Mirrors the
/// <c>SchedulingDtoMapper.MapSensitivity</c> switch (0=normal, 1=personal, 2=private,
/// 3=confidential) so the bridge and the downstream agent agree on the label vocabulary.
/// </summary>
public static class EventSensitivityLabel
{
    /// <summary>The Outlook <c>olNormal</c> sensitivity.</summary>
    public const string Normal = "normal";

    /// <summary>The Outlook <c>olPersonal</c> sensitivity.</summary>
    public const string Personal = "personal";

    /// <summary>The Outlook <c>olPrivate</c> sensitivity.</summary>
    public const string Private = "private";

    /// <summary>The Outlook <c>olConfidential</c> sensitivity.</summary>
    public const string Confidential = "confidential";

    /// <summary>
    /// Maps an Outlook <c>Sensitivity</c> integer to its label string.
    /// </summary>
    /// <param name="sensitivity">
    /// The Outlook sensitivity value: 0=normal, 1=personal, 2=private, 3=confidential.
    /// </param>
    /// <returns>
    /// The matching label, or <c>null</c> for a null input or an unrecognized value.
    /// </returns>
    public static string? FromSensitivity(int? sensitivity) =>
        sensitivity switch
        {
            0 => Normal,
            1 => Personal,
            2 => Private,
            3 => Confidential,
            _ => null,
        };
}
