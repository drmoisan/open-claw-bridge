namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Recorded Microsoft Graph v1.0-shaped JSON payloads held as in-repo raw-string
/// constants (no file I/O, no temp files, no live Graph calls). The shapes mirror the
/// spec <c>$select</c> lists: message pages with and without <c>@odata.nextLink</c>,
/// an <c>eventMessage</c> meeting request, a sparse message, private/recurring and
/// single-instance events, mailbox settings, a <c>getSchedule</c> response covering
/// all five statuses, and Graph error bodies.
/// </summary>
internal static class GraphPayloadFixtures
{
    /// <summary>A fully populated plain mail message (no <c>@odata.type</c> discriminator).</summary>
    internal const string MessageFull = """
        {
          "id": "msg-001",
          "subject": "Quarterly budget review",
          "bodyPreview": "Please review the attached figures",
          "receivedDateTime": "2026-07-01T12:00:00Z",
          "sentDateTime": "2026-07-01T11:59:00Z",
          "importance": "high",
          "sensitivity": "confidential",
          "isRead": false,
          "hasAttachments": true,
          "conversationId": "conv-1",
          "from": { "emailAddress": { "name": "Frank From", "address": "frank@contoso.com" } },
          "sender": { "emailAddress": { "name": "Sally Sender", "address": "sally@contoso.com" } },
          "toRecipients": [
            { "emailAddress": { "name": "Alice A", "address": "alice@contoso.com" } },
            { "emailAddress": { "name": "Bob B", "address": "bob@contoso.com" } }
          ],
          "ccRecipients": [
            { "emailAddress": { "name": "Carol C", "address": "carol@contoso.com" } }
          ]
        }
        """;

    /// <summary>An <c>eventMessage</c> meeting request with <c>meetingMessageType</c>.</summary>
    internal const string MeetingRequestMessage = """
        {
          "@odata.type": "#microsoft.graph.eventMessage",
          "id": "msg-mtg-001",
          "subject": "Invite: architecture sync",
          "bodyPreview": "You are invited",
          "receivedDateTime": "2026-07-01T13:00:00Z",
          "sentDateTime": "2026-07-01T12:59:00Z",
          "importance": "normal",
          "sensitivity": "normal",
          "isRead": true,
          "hasAttachments": false,
          "conversationId": "conv-2",
          "from": { "emailAddress": { "name": "Olive Organizer", "address": "olive@contoso.com" } },
          "sender": { "emailAddress": { "name": "Olive Organizer", "address": "olive@contoso.com" } },
          "toRecipients": [
            { "emailAddress": { "name": "Paula Principal", "address": "paula@contoso.com" } }
          ],
          "ccRecipients": [],
          "meetingMessageType": "meetingRequest"
        }
        """;

    /// <summary>A message with every optional field absent; only the required <c>id</c>.</summary>
    internal const string MessageSparse = """
        { "id": "msg-sparse-001" }
        """;

    /// <summary>Message list page 1: two items plus an <c>@odata.nextLink</c>.</summary>
    internal const string MessageListPage1 =
        "{ \"value\": ["
        + MessageFull
        + ","
        + MeetingRequestMessage
        + "], \"@odata.nextLink\": \"https://graph.example.test/v1.0/users/p%40contoso.com/messages?$skip=2\" }";

    /// <summary>Message list page 2: one sparse item and no <c>@odata.nextLink</c>.</summary>
    internal const string MessageListPage2 = "{ \"value\": [" + MessageSparse + "] }";

    /// <summary>
    /// A private, recurring (<c>occurrence</c>) event with <c>seriesMasterId</c> and
    /// attendees of all three types.
    /// </summary>
    internal const string EventPrivateOccurrence = """
        {
          "id": "evt-001",
          "iCalUId": "ical-001",
          "seriesMasterId": "master-001",
          "subject": "Private 1:1",
          "bodyPreview": "Weekly private sync",
          "body": { "contentType": "text", "content": "Full agenda text" },
          "organizer": { "emailAddress": { "name": "Olive Organizer", "address": "olive@contoso.com" } },
          "attendees": [
            { "type": "required", "emailAddress": { "name": "Alice A", "address": "alice@contoso.com" } },
            { "type": "optional", "emailAddress": { "name": "Bob B", "address": "bob@contoso.com" } },
            { "type": "resource", "emailAddress": { "name": "Room 4", "address": "room4@contoso.com" } }
          ],
          "categories": ["Focus", "OneOnOne"],
          "isOrganizer": true,
          "isOnlineMeeting": true,
          "allowNewTimeProposals": true,
          "sensitivity": "private",
          "showAs": "busy",
          "responseStatus": { "response": "accepted" },
          "location": { "displayName": "Room 4" },
          "start": { "dateTime": "2026-07-06T10:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-07-06T11:00:00.0000000", "timeZone": "UTC" },
          "type": "occurrence",
          "lastModifiedDateTime": "2026-07-01T09:00:00Z"
        }
        """;

    /// <summary>A non-recurring <c>singleInstance</c> event.</summary>
    internal const string EventSingleInstance = """
        {
          "id": "evt-002",
          "iCalUId": "ical-002",
          "subject": "Vendor call",
          "bodyPreview": "One-off call",
          "organizer": { "emailAddress": { "name": "Paula Principal", "address": "paula@contoso.com" } },
          "attendees": [
            { "type": "required", "emailAddress": { "name": "Vera Vendor", "address": "vera@example.com" } }
          ],
          "isOrganizer": false,
          "isOnlineMeeting": false,
          "allowNewTimeProposals": false,
          "sensitivity": "normal",
          "showAs": "tentative",
          "responseStatus": { "response": "notResponded" },
          "location": { "displayName": "Teams" },
          "start": { "dateTime": "2026-07-07T15:00:00.0000000", "timeZone": "UTC" },
          "end": { "dateTime": "2026-07-07T15:30:00.0000000", "timeZone": "UTC" },
          "type": "singleInstance",
          "lastModifiedDateTime": "2026-07-02T08:00:00Z"
        }
        """;

    /// <summary>A single calendarView page containing both events (no next link).</summary>
    internal const string EventListPage =
        "{ \"value\": [" + EventPrivateOccurrence + "," + EventSingleInstance + "] }";

    /// <summary>Mailbox settings with <c>timeZone</c> and <c>workingHours</c>.</summary>
    internal const string MailboxSettings = """
        {
          "timeZone": "Pacific Standard Time",
          "workingHours": {
            "daysOfWeek": ["monday", "tuesday", "wednesday", "thursday", "friday"],
            "startTime": "08:00:00.0000000",
            "endTime": "17:00:00.0000000"
          }
        }
        """;

    /// <summary>
    /// A <c>getSchedule</c> response whose schedule items cover all five statuses;
    /// only <c>busy</c>/<c>oof</c>/<c>tentative</c> count as busy (D11).
    /// </summary>
    internal const string GetScheduleResponse = """
        {
          "value": [
            {
              "scheduleId": "paula@contoso.com",
              "scheduleItems": [
                {
                  "status": "busy",
                  "start": { "dateTime": "2026-07-06T10:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-07-06T11:00:00.0000000", "timeZone": "UTC" }
                },
                {
                  "status": "oof",
                  "start": { "dateTime": "2026-07-06T12:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-07-06T13:00:00.0000000", "timeZone": "UTC" }
                },
                {
                  "status": "tentative",
                  "start": { "dateTime": "2026-07-06T14:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-07-06T14:30:00.0000000", "timeZone": "UTC" }
                },
                {
                  "status": "free",
                  "start": { "dateTime": "2026-07-06T15:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-07-06T16:00:00.0000000", "timeZone": "UTC" }
                },
                {
                  "status": "workingElsewhere",
                  "start": { "dateTime": "2026-07-06T16:00:00.0000000", "timeZone": "UTC" },
                  "end": { "dateTime": "2026-07-06T17:00:00.0000000", "timeZone": "UTC" }
                }
              ]
            }
          ]
        }
        """;

    /// <summary>A <c>getSchedule</c> response with an empty window (no items).</summary>
    internal const string GetScheduleEmptyResponse = """
        { "value": [ { "scheduleId": "paula@contoso.com", "scheduleItems": [] } ] }
        """;

    /// <summary>Graph 404 error body (<c>ErrorItemNotFound</c>).</summary>
    internal const string ErrorItemNotFoundBody = """
        {
          "error": {
            "code": "ErrorItemNotFound",
            "message": "The specified object was not found in the store."
          }
        }
        """;

    /// <summary>Graph 429 error body (<c>TooManyRequests</c>).</summary>
    internal const string TooManyRequestsBody = """
        {
          "error": {
            "code": "TooManyRequests",
            "message": "Too many requests. Please retry later."
          }
        }
        """;
}
