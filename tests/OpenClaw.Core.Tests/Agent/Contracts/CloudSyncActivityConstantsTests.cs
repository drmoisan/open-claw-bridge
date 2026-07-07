using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent.Contracts;

/// <summary>
/// Tests for the new CloudSync activity constant classes (issue #124):
/// <see cref="CloudSyncActivityType"/>, <see cref="CloudSyncActivityResultCode"/>, and
/// <see cref="CloudSyncActingFlags"/>. Verifies each constant is non-empty, that the sets
/// are internally distinct, and that the fixed acting-flags value matches spec.md decision 1
/// verbatim.
/// </summary>
[TestClass]
public sealed class CloudSyncActivityConstantsTests
{
    [TestMethod]
    public void CloudSyncActivityType_AllConstants_AreNonEmptyAndDistinct()
    {
        var values = new List<string>
        {
            CloudSyncActivityType.SubscriptionCreated,
            CloudSyncActivityType.SubscriptionRenewed,
            CloudSyncActivityType.SubscriptionExpired,
            CloudSyncActivityType.SubscriptionRemoved,
            CloudSyncActivityType.WebhookReceived,
            CloudSyncActivityType.WebhookRejected,
            CloudSyncActivityType.DeltaReconciliationRun,
        };

        values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
        values.Should().OnlyHaveUniqueItems();
        values.Should().HaveCount(7);
    }

    [TestMethod]
    public void CloudSyncActivityResultCode_AllConstants_AreNonEmptyAndDistinct()
    {
        var values = new List<string>
        {
            CloudSyncActivityResultCode.Success,
            CloudSyncActivityResultCode.Failure,
            CloudSyncActivityResultCode.UnknownSubscription,
            CloudSyncActivityResultCode.ClientStateMismatch,
            CloudSyncActivityResultCode.MissingResourceId,
        };

        values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
        values.Should().OnlyHaveUniqueItems();
        values.Should().HaveCount(5);
    }

    [TestMethod]
    public void CloudSyncActingFlags_NotApplicable_MatchesFixedDecisionOneValue()
    {
        CloudSyncActingFlags.NotApplicable.Should().Be("N/A:CloudSyncActivity");
    }
}
