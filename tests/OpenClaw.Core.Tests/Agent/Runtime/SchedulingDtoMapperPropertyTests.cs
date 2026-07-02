using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.Agent.Runtime;
using OpenClaw.HostAdapter.Contracts;
using SendMailRequest = OpenClaw.Core.Agent.SendMailRequest;

namespace OpenClaw.Core.Tests.Agent.Runtime;

/// <summary>
/// Property-based tests for <see cref="SchedulingDtoMapper.MapSendMailRequest"/> (T1
/// obligation: at least one property test per new pure function). Over a seeded CsCheck
/// sample of arbitrary valid agent requests, the outbound (agent-to-wire) mapping
/// preserves the To and Cc recipient counts and address multisets, always sets
/// <c>SaveToSentItems</c>, and never mutates the input record. CsCheck prints the failing
/// seed on a <c>Sample</c> failure, satisfying the determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class SchedulingDtoMapperPropertyTests
{
    private readonly SchedulingDtoMapper mapper = new();

    // Names include empty and whitespace-only values so the sample also exercises the
    // empty-or-whitespace-name-maps-to-null rule without breaking address preservation.
    private static readonly Gen<string> GenName = Gen.OneOfConst(
        "",
        "   ",
        "Alice",
        "Bob Smith",
        "Carol"
    );

    private static readonly Gen<string> GenEmail = Gen.OneOfConst(
        "alice@contoso.com",
        "bob@contoso.com",
        "carol@contoso.com",
        "dave@contoso.com"
    );

    private static readonly Gen<AttendeeDto> GenAttendee = Gen.Select(GenName, GenEmail)
        .Select(t =>
        {
            var (name, email) = t;
            return new AttendeeDto(name, email);
        });

    /// <summary>
    /// Generates arbitrary valid agent send requests: any subject/body text, both body
    /// content types, 0-5 To and Cc recipients (0-length Cc exercises the empty-list-to-null
    /// rule), and a present or absent reply linkage.
    /// </summary>
    private static readonly Gen<SendMailRequest> GenRequest = Gen.Select(
            Gen.String[0, 30],
            Gen.String[0, 60],
            Gen.OneOfConst("text", "html"),
            GenAttendee.List[0, 5],
            GenAttendee.List[0, 5],
            Gen.OneOfConst<string?>(null, "msg-1")
        )
        .Select(t =>
        {
            var (subject, body, contentType, to, cc, inReplyTo) = t;
            return new SendMailRequest(subject, body, contentType, to, cc, inReplyTo);
        });

    [TestMethod]
    public void MapSendMailRequest_PreservesRecipientsSetsSaveAndNeverMutatesInput()
    {
        GenRequest.Sample(
            request =>
            {
                // Snapshot the input's observable state to detect mutation.
                var subjectBefore = request.Subject;
                var bodyBefore = request.BodyContent;
                var contentTypeBefore = request.BodyContentType;
                var toBefore = request.ToRecipients.ToList();
                var ccBefore = request.CcRecipients.ToList();

                var wire = mapper.MapSendMailRequest(request);

                // Recipient count and address multiset (here: exact sequence) are
                // preserved for To and Cc; a null wire CC list is the mapped form of an
                // empty agent CC list.
                var wireTo = wire.Message.ToRecipients.Select(r => r.EmailAddress.Address);
                var wireCc = (
                    wire.Message.CcRecipients ?? Array.Empty<SendMailRecipientDto>()
                ).Select(r => r.EmailAddress.Address);
                wireTo.Should().Equal(toBefore.Select(a => a.Email));
                wireCc.Should().Equal(ccBefore.Select(a => a.Email));

                wire.SaveToSentItems.Should().BeTrue();

                // The input record is never mutated.
                request.Subject.Should().Be(subjectBefore);
                request.BodyContent.Should().Be(bodyBefore);
                request.BodyContentType.Should().Be(contentTypeBefore);
                request.ToRecipients.Should().Equal(toBefore);
                request.CcRecipients.Should().Equal(ccBefore);
            },
            iter: 1000
        );
    }
}
