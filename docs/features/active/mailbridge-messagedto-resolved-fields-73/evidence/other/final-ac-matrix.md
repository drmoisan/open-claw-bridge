Timestamp: 2026-06-15T08-59

# AC Verification Matrix — Issue #73 Remediation Cycle 1

| AC | Status | Verifying Evidence |
|----|--------|-------------------|
| AC-01 | PASS | MessageDto declared in MailBridge.Contracts with SenderEmailResolved, FromEmailAddress, ConversationId, MeetingMessageType as trailing optional params. Verified in plan.md Phase 1-2 (original feature) and confirmed unchanged by RF-2 extraction. |
| AC-02 | PASS | ComMessageSource.ResolveSenderSmtp covers PropertyAccessor, ExchangeUser, Address fallback, and raw SenderEmailAddress. Tests: ResolveSenderSmtp_should_return_smtp_from_property_accessor, ResolveViaExchangeUser_should_return_primary_smtp_address, Adapter_should_fall_back_to_raw_sender_address_when_no_sender_object. |
| AC-03 | PASS | ComMessageSource.ResolveFromSmtp: empty on-behalf-of → ResolveSenderSmtp; SMTP-shaped → direct; non-SMTP → chain; chain empty → normalized raw. Tests: ResolveFromSmtp_should_delegate_to_sender, _should_return_on_behalf_of_value_directly_when_smtp, _should_resolve_non_smtp_via_address_entry, _should_return_normalized_raw. |
| AC-04 | PASS | To=type 1, Cc=type 2 recipient projection. Tests: Adapter_should_project_to_and_cc_recipients_by_type. Existing OutlookScannerAttendeesTests cover JSON shape (pre-existing; unchanged by RF). |
| AC-05 | PASS | ConversationId pass-through. Test: Adapter_should_pass_conversation_id_through_unmodified. |
| AC-06 | PASS | MeetingMessageType: null for ordinary mail, raw OlMeetingType for meeting items. Tests: Adapter_should_yield_null_meeting_type_for_ordinary_mail, Adapter_should_expose_raw_meeting_type_for_meeting_items (5 DataRow values). |
| AC-07 | PASS | Meeting-request MessageDto has SenderEmailResolved + ToJson + ConversationId simultaneously. Covered by existing CacheRepositoryMessageFieldsTests and OutlookScannerMessageFieldsTests (pre-existing; confirmed passing at 263/266 in P3-T5). |
| AC-08 | PASS | ComMessageSourceTests + ComMessageSourceResolutionTests cover both meeting-message path (isMeeting=true) and ordinary-mail path (isMeeting=false). |
| AC-09 | PASS | IMessageSource interface and ComMessageSource adapter reside only in OpenClaw.MailBridge. COM confinement verified in P3-T4 (final-architecture.md): 0 boundary violations, no new ProjectReference edges. |
| AC-10 | PASS | RF-2 extraction preserves all SQL/binding/reader logic for both SQLite caches. Core.Tests Passed=206 (0 failed) in P3-T5 confirms read-back fidelity. CoreCacheRepository schema migrations remain in Schema.cs (not moved). |
| AC-11 | PASS | Seven-stage toolchain passes (P3-T1..P3-T8 evidence): format=PASS, lint=0 warnings/errors, nullable=0 warnings, architecture=0 violations, tests=0 failed (558 passed), ComMessageSource.cs line=94.7%>=85%/branch=93.5%>=75%, Core.Tests line=89.6%>=85%/branch=78.4%>=75%, MailBridge.Tests line=93.9%>=85%/branch=87.0%>=75%, no new suppressions, all files <=500 lines. |

All AC-01..AC-11: PASS.
