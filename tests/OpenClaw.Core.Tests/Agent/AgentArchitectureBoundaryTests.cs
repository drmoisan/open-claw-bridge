using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Enforces the AC-10 / AC-U1 contract-parity invariant by namespace partition. The
/// deterministic agent surface (D1-D4) and the D5/D6 contracts live under namespace
/// <c>OpenClaw.Core.Agent</c> (excluding <c>OpenClaw.Core.Agent.Runtime</c>) and must
/// not depend on <c>OpenClaw.MailBridge</c>, <c>OpenClaw.HostAdapter</c>, Outlook COM
/// interop, or <c>System.Runtime.InteropServices</c>. The runtime seam under
/// <c>OpenClaw.Core.Agent.Runtime</c> is explicitly exempt because it implements the
/// HostAdapter-backed adapter and may reference <c>OpenClaw.HostAdapter.Contracts</c>.
/// </summary>
[TestClass]
public sealed class AgentArchitectureBoundaryTests
{
    private const string AgentNamespace = "OpenClaw.Core.Agent";
    private const string RuntimeNamespace = "OpenClaw.Core.Agent.Runtime";

    private static readonly string[] BannedDependencies =
    [
        "OpenClaw.MailBridge",
        "OpenClaw.HostAdapter",
        "Microsoft.Office.Interop.Outlook",
        "System.Runtime.InteropServices",
    ];

    [TestMethod]
    public void DeterministicSurfaceAndContracts_DoNotDependOnBridgeHostAdapterOrCom()
    {
        // Arrange: the D1-D4 deterministic surface plus the D5/D6 contracts are the
        // types in OpenClaw.Core.Agent that are NOT in OpenClaw.Core.Agent.Runtime.
        var partition = Types
            .InAssembly(typeof(AgentPolicyOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(AgentNamespace)
            .And()
            .DoNotResideInNamespaceStartingWith(RuntimeNamespace);

        // Act: assert the partition does not depend on any banned namespace/assembly.
        var result = partition.ShouldNot().HaveDependencyOnAny(BannedDependencies).GetResult();

        // Assert: no type in the partition violates the boundary.
        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the D1-D4 deterministic surface and D5/D6 contracts must not depend on "
                    + "OpenClaw.MailBridge, OpenClaw.HostAdapter, Outlook COM, or "
                    + "System.Runtime.InteropServices; offending types: "
                    + string.Join(", ", failing)
            );
    }
}
