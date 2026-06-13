---
name: unify-com-vs-modern-behind-adapter
description: When normalization/resolution logic depends on the concrete mail data model (Outlook COM vs Modern/Graph), put it behind a unifying interface + data-type adapter so only the adapter swaps.
metadata:
  type: feedback
---

When code must extract or resolve fields from a model-specific source object (Outlook COM `MailItem`/`MeetingItem`/`Recipients`, or a future Modern/Microsoft Graph payload), do not bind the core normalization logic directly to that concrete type. Introduce a unifying interface that expresses the data the normalizer needs, and a data-type adapter that maps the concrete model (COM today, Graph/Modern later) onto that interface.

**Why:** Operator directive on Issue #73 (2026-06-13). The goal is that switching the underlying mail model (COM vs Modern) requires changing only the data-type adapter, never a rewrite of core elements (scanner normalization, DTO mapping, SMTP/conversation/meeting-type resolution). This extends the existing COM-confinement boundary (`.claude/rules/architecture-boundaries.md`: Outlook COM lives only in `OpenClaw.MailBridge`) by making even the in-host normalization model-agnostic behind a seam.

**How to apply:** When a feature touches `OutlookScanner`/normalization or any field-resolution that reads COM members (e.g. `Sender.AddressEntry.Address`, `ConversationID`, `MeetingType`, recipient enumeration), check whether the resolution couples core logic to COM. If so, plan a unifying interface + COM adapter rather than inlining COM access into the normalizer. Keep the interface and adapter inside `OpenClaw.MailBridge` (COM stays confined). The adapter is the single swap point for a future Modern/Graph implementation. Relates to the C# DI-seam guidance (adapter seam for static/COM APIs) in `.claude/rules/csharp.md`.
