using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// Pins the spec D1 dependency direction of the pure scope-validation core: the pure
/// types (<see cref="ScopeBoundaryEvaluator"/>, <see cref="MailboxProbeOutcome"/>,
/// <see cref="ScopeBoundaryValidationResult"/>, and <see cref="IMailboxScopeProbe"/>) must
/// not depend on <c>OpenClaw.Core.CloudGraph</c> types, HTTP types, or logging. The
/// <c>CloudGraph</c> partition implements the port; the pure core defines it. Introducing
/// a CloudGraph/HTTP/logging dependency into any of these four types fails these
/// assertions.
/// </summary>
[TestClass]
public sealed class ScopeValidationArchitectureBoundaryTests
{
    private const string CloudGraphNamespace = "OpenClaw.Core.CloudGraph";
    private const string HttpNamespace = "System.Net.Http";
    private const string LoggingNamespace = "Microsoft.Extensions.Logging";

    /// <summary>Selects exactly the four host-neutral pure-core types.</summary>
    private static PredicateList PureCoreTypes() =>
        Types
            .InAssembly(typeof(ScopeBoundaryEvaluator).Assembly)
            .That()
            .HaveName(nameof(ScopeBoundaryEvaluator))
            .Or()
            .HaveName(nameof(MailboxProbeOutcome))
            .Or()
            .HaveName(nameof(ScopeBoundaryValidationResult))
            .Or()
            .HaveName(nameof(IMailboxScopeProbe));

    [TestMethod]
    public void PureCoreTypes_DoNotDependOnCloudGraph()
    {
        var result = PureCoreTypes().ShouldNot().HaveDependencyOn(CloudGraphNamespace).GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the pure scope-validation core must not depend on OpenClaw.Core.CloudGraph "
                    + "(the CloudGraph partition implements the port; the core defines it); "
                    + "offending types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void PureCoreTypes_DoNotDependOnHttp()
    {
        var result = PureCoreTypes().ShouldNot().HaveDependencyOn(HttpNamespace).GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the pure scope-validation core must not depend on System.Net.Http; offending "
                    + "types: "
                    + string.Join(", ", failing)
            );
    }

    [TestMethod]
    public void PureCoreTypes_DoNotDependOnLogging()
    {
        var result = PureCoreTypes().ShouldNot().HaveDependencyOn(LoggingNamespace).GetResult();

        var failing = result.FailingTypeNames ?? Enumerable.Empty<string>();
        result
            .IsSuccessful.Should()
            .BeTrue(
                "the pure scope-validation core must not depend on Microsoft.Extensions.Logging; "
                    + "offending types: "
                    + string.Join(", ", failing)
            );
    }
}
