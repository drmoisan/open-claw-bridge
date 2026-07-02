using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Enforces the D2 boundary contract introduced with the Azure.Identity dependency:
/// (a) the deterministic agent surface (namespace <c>OpenClaw.Core.Agent</c>, including
/// <c>Runtime</c>) stays Azure/MSAL-free so the dependency cannot spread, and (b) the
/// CloudAuth public contract surface exposes no <c>Azure.*</c> or
/// <c>Microsoft.Identity*</c> type, so consumers depend only on host-neutral types.
/// The reflection walk covers public members only; the internal test constructor (D4)
/// is exempt by design.
/// </summary>
[TestClass]
public sealed class CloudAuthArchitectureBoundaryTests
{
    private const string AgentNamespace = "OpenClaw.Core.Agent";

    /// <summary>Prefixes banned for the agent partition and the public contract surface.</summary>
    private static readonly string[] BannedNamespacePrefixes = ["Azure", "Microsoft.Identity"];

    /// <summary>The CloudAuth public contract surface (D2 assertion 2).</summary>
    private static readonly System.Type[] ContractSurface =
    [
        typeof(IAppTokenProvider),
        typeof(AppAccessToken),
        typeof(CloudAuthOptions),
        typeof(TokenAcquisitionException),
        typeof(ClientCredentialsTokenProvider),
        typeof(CloudAuthServiceCollectionExtensions),
    ];

    [TestMethod]
    public void AgentNamespaceIncludingRuntime_DoesNotDependOnAzureOrMsal()
    {
        // Arrange: the whole OpenClaw.Core.Agent partition, Runtime included.
        var partition = Types
            .InAssembly(typeof(AgentPolicyOptions).Assembly)
            .That()
            .ResideInNamespaceStartingWith(AgentNamespace);

        // Act
        var result = partition.ShouldNot().HaveDependencyOnAny(BannedNamespacePrefixes).GetResult();

        // Assert
        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the deterministic agent surface must stay Azure/MSAL-free; only "
                    + "OpenClaw.Core.CloudAuth may reference Azure types; offending types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void CloudAuthPublicContractSurface_ExposesNoAzureOrMsalType()
    {
        // Arrange + Act: walk every public member of the contract surface and collect
        // the namespaces of all referenced types (return, parameter, property, field,
        // and constructor-parameter types, generic arguments included).
        var offending = ContractSurface
            .SelectMany(type =>
                GetPublicSurfaceNamespaces(type).Select(ns => (Type: type.Name, Namespace: ns))
            )
            .Where(pair =>
                BannedNamespacePrefixes.Any(banned =>
                    pair.Namespace.StartsWith(banned, System.StringComparison.Ordinal)
                )
            )
            .Distinct()
            .ToList();

        // Assert
        offending
            .Should()
            .BeEmpty(
                "no Azure.* or Microsoft.Identity* type may appear on the CloudAuth public "
                    + "contract surface (D2); offending pairs: "
                    + string.Join(", ", offending.Select(p => $"{p.Type} -> {p.Namespace}"))
            );
    }

    private const System.Reflection.BindingFlags PublicMembers =
        System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.DeclaredOnly;

    private static IEnumerable<string> GetPublicSurfaceNamespaces(System.Type type)
    {
        var namespaces = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var constructor in type.GetConstructors(PublicMembers))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                AddNamespace(namespaces, parameter.ParameterType);
            }
        }

        foreach (var method in type.GetMethods(PublicMembers))
        {
            AddNamespace(namespaces, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddNamespace(namespaces, parameter.ParameterType);
            }
        }

        foreach (var property in type.GetProperties(PublicMembers))
        {
            AddNamespace(namespaces, property.PropertyType);
        }

        foreach (var field in type.GetFields(PublicMembers))
        {
            AddNamespace(namespaces, field.FieldType);
        }

        return namespaces;
    }

    private static void AddNamespace(HashSet<string> namespaces, System.Type type)
    {
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                AddNamespace(namespaces, argument);
            }
        }

        var ns = type.Namespace;
        if (!string.IsNullOrEmpty(ns))
        {
            namespaces.Add(ns);
        }
    }
}
