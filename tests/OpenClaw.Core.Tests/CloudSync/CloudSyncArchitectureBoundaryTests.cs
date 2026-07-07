using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Enforces the CloudSync architecture boundary (issue #117, AC-4), following
/// <c>CloudGraphArchitectureBoundaryTests</c>: (1) <c>OpenClaw.Core.CloudSync</c>
/// types depend only on CloudSync itself, the CloudGraph/CloudAuth seams, the
/// contracts namespaces, the <c>OpenClaw.Core</c> repository/ingest surface, and
/// BCL/hosting abstractions — in particular, never on the Agent partition; (2) no
/// CloudSync dependency on COM interop namespaces; (3) no type outside CloudSync
/// depends on CloudSync internals, excepting the composition root (<c>Program</c>,
/// global namespace) and the <c>CoreCacheRepository</c> store partials that implement
/// the CloudSync persistence seams named by the spec.
/// </summary>
[TestClass]
public sealed class CloudSyncArchitectureBoundaryTests
{
    private const string CloudSyncNamespace = "OpenClaw.Core.CloudSync";

    /// <summary>The OpenClaw namespaces CloudSync may depend on (allowlist, prefix-matched).</summary>
    private static readonly string[] AllowedOpenClawPrefixes =
    [
        "OpenClaw.Core.CloudSync",
        "OpenClaw.Core.CloudGraph",
        "OpenClaw.Core.CloudAuth",
        "OpenClaw.HostAdapter.Contracts",
        "OpenClaw.MailBridge.Contracts",
    ];

    private static readonly string[] ComBannedDependencies =
    [
        "Microsoft.Office.Interop.Outlook",
        "System.Runtime.InteropServices",
    ];

    [TestMethod]
    public void CloudSync_DependsOnlyOnTheAllowedOpenClawSurfaces()
    {
        // NetArchTest matches dependencies by prefix and cannot express "OpenClaw.Core
        // itself but no other OpenClaw.Core sub-namespace", so this test inspects the
        // dependency namespaces of every CloudSync type (the CloudGraph precedent):
        // each OpenClaw* dependency must be an allowed prefix or exactly the
        // OpenClaw.Core repository/ingest surface.
        var cloudSyncTypes = Types
            .InAssembly(typeof(CloudSyncOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(CloudSyncNamespace)
            .GetTypes();

        var offendingDependencies = cloudSyncTypes
            .SelectMany(GetDependencyNamespaces)
            .Where(d => d.StartsWith("OpenClaw", StringComparison.Ordinal))
            .Where(d =>
                !string.Equals(d, "OpenClaw.Core", StringComparison.Ordinal)
                && !AllowedOpenClawPrefixes.Any(allowed =>
                    d.StartsWith(allowed, StringComparison.Ordinal)
                )
            )
            .Distinct()
            .ToList();

        offendingDependencies
            .Should()
            .BeEmpty(
                "OpenClaw.Core.CloudSync may depend only on CloudSync, CloudGraph, CloudAuth, "
                    + "the contracts namespaces, and the OpenClaw.Core repository/ingest "
                    + "surface; offending dependencies: "
                    + string.Join(", ", offendingDependencies)
            );
    }

    [TestMethod]
    public void CloudSync_DoesNotDependOnTheAgentPartition()
    {
        var result = Types
            .InAssembly(typeof(CloudSyncOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(CloudSyncNamespace)
            .ShouldNot()
            .HaveDependencyOn("OpenClaw.Core.Agent")
            .GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "CloudSync consumes the repository/ingest surface only and must not depend "
                    + "on the Agent partition; offending types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void CloudSync_DoesNotDependOnComInterop()
    {
        var result = Types
            .InAssembly(typeof(CloudSyncOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(CloudSyncNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ComBannedDependencies)
            .GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "OpenClaw.Core.CloudSync must not depend on Microsoft.Office.Interop.Outlook "
                    + "or System.Runtime.InteropServices (COM stays in MailBridge); offending "
                    + "types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void NothingOutsideCloudSync_DependsOnCloudSyncInternals()
    {
        // The composition root (Program, global namespace) is outside the
        // OpenClaw.Core filter and therefore excepted by construction; the
        // CoreCacheRepository partials implement the ISubscriptionStore/IDeltaLinkStore
        // persistence seams the spec places on the repository, so the repository type
        // is the one named exception inside OpenClaw.Core.
        var result = Types
            .InAssembly(typeof(CloudSyncOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith("OpenClaw.Core")
            .And()
            .DoNotResideInNamespaceStartingWith(CloudSyncNamespace)
            .And()
            .DoNotHaveName("CoreCacheRepository")
            .ShouldNot()
            .HaveDependencyOn(CloudSyncNamespace)
            .GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "backend wiring happens solely in the composition root; no other type may "
                    + "depend on CloudSync internals; offending types: "
                    + string.Join(", ", failing)
            );
    }

    private static IEnumerable<string> GetDependencyNamespaces(Type type)
    {
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in type.GetMethods(AllMembers))
        {
            AddNamespace(dependencies, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddNamespace(dependencies, parameter.ParameterType);
            }
        }

        foreach (var constructor in type.GetConstructors(AllMembers))
        {
            foreach (var parameter in constructor.GetParameters())
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

    private static void AddNamespace(HashSet<string> dependencies, Type type)
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
