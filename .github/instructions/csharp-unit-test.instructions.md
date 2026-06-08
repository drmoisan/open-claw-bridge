---
applyTo: "**/*.cs"
name: csharp-unit-test-policy
description: "C#-specific unit test rules, layered on top of the general unit test policy"
---

# C# Unit Test Policy

This policy **extends** `general-unit-test.instructions.md` and applies to all C# unit tests in this repo.

You must follow **both**:

- The general unit test policy, and
- The C#-specific rules below.

If there is any conflict between these documents, halt and notify the user.

---

## 1. Framework Selection

- **Testing framework**
  - Use **MSTest** (`Microsoft.VisualStudio.TestTools.UnitTesting`) for C# unit tests in this repository.
  - Do not introduce xUnit or NUnit into existing test projects.

---

## 2. C#-Specific Libraries and Conventions

- **Mocking library**
  - Use **Moq** for mocks/stubs in C# unit tests.

- **Assertion library**
  - Prefer **FluentAssertions** for new and updated assertions.
  - Use MSTest `Assert` APIs only when FluentAssertions is not practical for a specific assertion shape.

- **MSTest style**
  - Use `[TestClass]`, `[TestMethod]`, and other MSTest attributes from `Microsoft.VisualStudio.TestTools.UnitTesting`.

---

## 3. C# Toolchain Command Selection

- For C# work, use these concrete commands for the general policy toolchain loop:
  1. `csharpier .`
  2. `dotnet build OpenClaw.MailBridge.sln`
  3. `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

- The loop behavior (restart rules, must-pass requirements, and audit expectations) is defined by `general-code-change.instructions.md` and is intentionally not repeated here.

This file is intentionally limited to C#-specific framework/library/tool selection. Cross-language testing principles and policy requirements are defined in `general-unit-test.instructions.md` and `general-code-change.instructions.md`.
