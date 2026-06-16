using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class OutlookApplicationProviderTests
{
    [TestMethod]
    public void Application_should_default_to_null()
    {
        // Arrange
        var provider = new OutlookApplicationProvider();

        // Act / Assert
        provider.Application.Should().BeNull();
    }

    [TestMethod]
    public void Set_should_store_the_same_reference()
    {
        // Arrange
        var provider = new OutlookApplicationProvider();
        var sentinel = new object();

        // Act
        provider.Set(sentinel);

        // Assert
        provider.Application.Should().BeSameAs(sentinel);
    }

    [TestMethod]
    public void Set_null_should_clear_the_reference()
    {
        // Arrange
        var provider = new OutlookApplicationProvider();
        provider.Set(new object());

        // Act
        provider.Set(null);

        // Assert
        provider.Application.Should().BeNull();
    }
}
