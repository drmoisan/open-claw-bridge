using System.Collections.Generic;
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
/// not depend on <c>OpenClaw.MailBridge</c>, the <c>OpenClaw.HostAdapter</c> host
/// implementation, Outlook COM interop, or <c>System.Runtime.InteropServices</c>.
/// <para>
/// The <c>OpenClaw.HostAdapter.Contracts</c> contracts package is an allowed dependency
/// for the whole <c>OpenClaw.Core.Agent</c> surface (not only the runtime seam): per
/// the locked Design A for issue #74 the D6 scheduling DTOs (<c>MailboxSettingsDto</c>,
/// <c>FreeBusyScheduleDto</c>, <c>BusyIntervalDto</c>) live in
/// <c>OpenClaw.HostAdapter.Contracts</c>, and the <c>ISchedulingService</c> contract
/// returns them. This is consistent with the project-graph boundary
/// (<c>.claude/rules/architecture-boundaries.md</c> Rule 6), which permits
/// <c>OpenClaw.Core -&gt; OpenClaw.HostAdapter.Contracts</c>. The runtime seam under
/// <c>OpenClaw.Core.Agent.Runtime</c> additionally implements the HostAdapter-backed
/// adapter.
/// </para>
/// </summary>
[TestClass]
public sealed class AgentArchitectureBoundaryTests
{
    private const string AgentNamespace = "OpenClaw.Core.Agent";
    private const string RuntimeNamespace = "OpenClaw.Core.Agent.Runtime";
    private const string HostAdapterContractsNamespace = "OpenClaw.HostAdapter.Contracts";

    /// <summary>
    /// Dependencies that are banned outright for the deterministic surface. Note that
    /// <c>OpenClaw.HostAdapter.Contracts</c> is deliberately NOT in this list: the host
    /// implementation namespace <c>OpenClaw.HostAdapter</c> is banned separately so that
    /// the contracts package remains an allowed dependency.
    /// </summary>
    private static readonly string[] BannedDependencies =
    [
        "OpenClaw.MailBridge",
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

        // Act: assert the partition does not depend on any outright-banned namespace.
        var result = partition.ShouldNot().HaveDependencyOnAny(BannedDependencies).GetResult();

        // Assert: no type in the partition violates the boundary.
        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the D1-D4 deterministic surface and D5/D6 contracts must not depend on "
                    + "OpenClaw.MailBridge, Outlook COM, or System.Runtime.InteropServices; "
                    + "offending types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation()
    {
        // The deterministic surface may reference OpenClaw.HostAdapter.Contracts (the
        // relocated D6 DTOs returned by ISchedulingService) but must not reference the
        // OpenClaw.HostAdapter host implementation. NetArchTest 1.3.2 matches dependencies
        // by prefix and cannot ban "OpenClaw.HostAdapter" while allowing
        // "OpenClaw.HostAdapter.Contracts", so this test inspects the actual dependency
        // names: every OpenClaw.HostAdapter* dependency of the non-runtime partition must
        // be under OpenClaw.HostAdapter.Contracts.
        var partition = Types
            .InAssembly(typeof(AgentPolicyOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(AgentNamespace)
            .And()
            .DoNotResideInNamespaceStartingWith(RuntimeNamespace)
            .GetTypes();

        var offendingDependencies = partition
            .SelectMany(GetDependencyNamespaces)
            .Where(d =>
                d.StartsWith("OpenClaw.HostAdapter", System.StringComparison.Ordinal)
                && !d.StartsWith(HostAdapterContractsNamespace, System.StringComparison.Ordinal)
            )
            .Distinct()
            .ToList();

        offendingDependencies
            .Should()
            .BeEmpty(
                "the deterministic surface may depend on OpenClaw.HostAdapter.Contracts but "
                    + "must not depend on the OpenClaw.HostAdapter host implementation; "
                    + "offending dependencies: "
                    + string.Join(", ", offendingDependencies)
            );
    }

    private static IEnumerable<string> GetDependencyNamespaces(System.Type type)
    {
        var dependencies = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var method in type.GetMethods(AllMembers))
        {
            AddNamespace(dependencies, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddNamespace(dependencies, parameter.ParameterType);
            }
        }

        foreach (var property in type.GetProperties(AllMembers))
        {
            AddNamespace(dependencies, property.PropertyType);
        }

        foreach (var field in type.GetFields(AllMembers))
        {
            AddNamespace(dependencies, field.FieldType);
        }

        return dependencies;
    }

    private const System.Reflection.BindingFlags AllMembers =
        System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.DeclaredOnly;

    private static void AddNamespace(HashSet<string> dependencies, System.Type type)
    {
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                AddNamespace(dependencies, argument);
            }
        }

        var ns = type.Namespace;
        if (!string.IsNullOrEmpty(ns))
        {
            dependencies.Add(ns);
        }
    }
}
