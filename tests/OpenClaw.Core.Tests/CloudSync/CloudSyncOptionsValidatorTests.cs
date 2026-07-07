using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Unit tests for the pure D-7 fail-closed rules in
/// <see cref="CloudSyncOptionsValidator"/>: the defaults with a valid https
/// notification URL pass, each rule fails individually, and the documented boundary
/// values (lifetime 10080, lead 1) pass.
/// </summary>
[TestClass]
public sealed class CloudSyncOptionsValidatorTests
{
    private static CloudSyncOptions ValidOptions() =>
        new() { Enabled = true, NotificationUrl = "https://webhook.contoso.com/graph" };

    [TestMethod]
    public void Defaults_with_valid_https_url_and_graph_backend_pass()
    {
        // Arrange
        var options = ValidOptions();

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations.Should().BeEmpty("the documented defaults with a valid https URL are valid");
    }

    [TestMethod]
    public void Disabled_options_are_always_valid()
    {
        // Arrange: everything else invalid, but Enabled=false gates all rules off.
        var options = new CloudSyncOptions
        {
            Enabled = false,
            NotificationUrl = null,
            SubscriptionLifetimeMinutes = 0,
            RenewalLeadMinutes = 0,
            ReconcileIntervalMinutes = 0,
            QueueCapacity = 0,
        };

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: false);

        // Assert
        violations.Should().BeEmpty("a disabled CloudSync block is always valid");
    }

    [TestMethod]
    public void Graph_backend_disabled_fails_closed()
    {
        // Arrange
        var options = ValidOptions();

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: false);

        // Assert
        violations
            .Should()
            .ContainSingle("only the D-7 Graph-backend cross-check fails")
            .Which.Should()
            .Contain("GraphAdapter:Enabled");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "missing NotificationUrl")]
    [DataRow("", DisplayName = "empty NotificationUrl")]
    [DataRow("/relative/path", DisplayName = "relative NotificationUrl")]
    [DataRow("http://webhook.contoso.com/graph", DisplayName = "http NotificationUrl")]
    public void Invalid_notification_url_fails(string? notificationUrl)
    {
        // Arrange
        var options = ValidOptions();
        options.NotificationUrl = notificationUrl;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("NotificationUrl must be an absolute https URI.");
    }

    [TestMethod]
    [DataRow(0, DisplayName = "lifetime 0")]
    [DataRow(10081, DisplayName = "lifetime above the 10,080 cap")]
    public void Out_of_range_lifetime_fails(int lifetimeMinutes)
    {
        // Arrange
        var options = ValidOptions();
        options.SubscriptionLifetimeMinutes = lifetimeMinutes;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert: lifetime 0 also violates the lead-below-lifetime rule, so assert on
        // the lifetime violation specifically rather than an exact violation count.
        violations
            .Should()
            .Contain(v => v.Contains("SubscriptionLifetimeMinutes must be between 1 and 10080"));
    }

    [TestMethod]
    public void Renewal_lead_zero_fails()
    {
        // Arrange
        var options = ValidOptions();
        options.RenewalLeadMinutes = 0;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("RenewalLeadMinutes must be at least 1.");
    }

    [TestMethod]
    public void Renewal_lead_equal_to_lifetime_fails()
    {
        // Arrange
        var options = ValidOptions();
        options.SubscriptionLifetimeMinutes = 30;
        options.RenewalLeadMinutes = 30;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("RenewalLeadMinutes must be strictly less than SubscriptionLifetimeMinutes.");
    }

    [TestMethod]
    public void Reconcile_interval_zero_fails()
    {
        // Arrange
        var options = ValidOptions();
        options.ReconcileIntervalMinutes = 0;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("ReconcileIntervalMinutes must be at least 1.");
    }

    [TestMethod]
    public void Queue_capacity_zero_fails()
    {
        // Arrange
        var options = ValidOptions();
        options.QueueCapacity = 0;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations.Should().ContainSingle().Which.Should().Be("QueueCapacity must be at least 1.");
    }

    [TestMethod]
    public void Boundary_lifetime_10080_and_lead_1_pass()
    {
        // Arrange
        var options = ValidOptions();
        options.SubscriptionLifetimeMinutes = 10080;
        options.RenewalLeadMinutes = 1;

        // Act
        var violations = CloudSyncOptionsValidator.Validate(options, graphAdapterEnabled: true);

        // Assert
        violations.Should().BeEmpty("10080 is the inclusive cap and 1 is the minimum lead");
    }
}
