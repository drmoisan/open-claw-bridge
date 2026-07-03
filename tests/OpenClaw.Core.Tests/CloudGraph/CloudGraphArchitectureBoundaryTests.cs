using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Enforces the D12 architecture boundary for the Graph-backed adapter (issue #115):
/// (1) <c>OpenClaw.Core.CloudGraph</c> depends on no <c>OpenClaw.MailBridge.*</c>
/// namespace except <c>OpenClaw.MailBridge.Contracts</c> (the wire DTOs live there);
/// (2) <c>OpenClaw.Core.CloudGraph</c> has no COM interop dependency; and
/// (3) the whole <c>OpenClaw.Core.Agent</c> partition (including <c>Runtime</c>) does
/// not depend on <c>OpenClaw.Core.CloudGraph</c> — backend wiring happens solely in
/// the composition root.
/// </summary>
[TestClass]
public sealed class CloudGraphArchitectureBoundaryTests
{
    private const string CloudGraphNamespace = "OpenClaw.Core.CloudGraph";
    private const string AgentNamespace = "OpenClaw.Core.Agent";
    private const string MailBridgeContractsNamespace = "OpenClaw.MailBridge.Contracts";

    /// <summary>
    /// D12 rule 2: COM stays in MailBridge — CloudGraph must not reference Outlook
    /// interop or <c>System.Runtime.InteropServices</c>.
    /// </summary>
    private static readonly string[] ComBannedDependencies =
    [
        "Microsoft.Office.Interop.Outlook",
        "System.Runtime.InteropServices",
    ];

    [TestMethod]
    public void CloudGraph_DependsOnNoMailBridgeNamespaceExceptContracts()
    {
        // NetArchTest matches dependencies by prefix and cannot ban
        // "OpenClaw.MailBridge" while allowing "OpenClaw.MailBridge.Contracts", so this
        // test inspects the actual dependency namespaces of every CloudGraph type: each
        // OpenClaw.MailBridge* dependency must be under OpenClaw.MailBridge.Contracts.
        var cloudGraphTypes = Types
            .InAssembly(typeof(GraphAdapterOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(CloudGraphNamespace)
            .GetTypes();

        var offendingDependencies = cloudGraphTypes
            .SelectMany(GetDependencyNamespaces)
            .Where(d =>
                d.StartsWith("OpenClaw.MailBridge", System.StringComparison.Ordinal)
                && !d.StartsWith(MailBridgeContractsNamespace, System.StringComparison.Ordinal)
            )
            .Distinct()
            .ToList();

        offendingDependencies
            .Should()
            .BeEmpty(
                "OpenClaw.Core.CloudGraph may depend on OpenClaw.MailBridge.Contracts (the "
                    + "wire DTOs) but on no other OpenClaw.MailBridge namespace; offending "
                    + "dependencies: "
                    + string.Join(", ", offendingDependencies)
            );
    }

    [TestMethod]
    public void CloudGraph_DoesNotDependOnComInterop()
    {
        var partition = Types
            .InAssembly(typeof(GraphAdapterOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(CloudGraphNamespace);

        var result = partition.ShouldNot().HaveDependencyOnAny(ComBannedDependencies).GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "OpenClaw.Core.CloudGraph must not depend on Microsoft.Office.Interop.Outlook "
                    + "or System.Runtime.InteropServices (COM stays in MailBridge); offending "
                    + "types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void AgentPartitionIncludingRuntime_DoesNotDependOnCloudGraph()
    {
        var partition = Types
            .InAssembly(typeof(GraphAdapterOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(AgentNamespace);

        var result = partition.ShouldNot().HaveDependencyOn(CloudGraphNamespace).GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the whole OpenClaw.Core.Agent partition (including Runtime) consumes only "
                    + "IHostAdapterClient and must not depend on OpenClaw.Core.CloudGraph; "
                    + "offending types: "
                    + string.Join(", ", failing)
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
