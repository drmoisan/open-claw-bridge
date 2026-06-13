using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Unit tests for <see cref="EventSensitivityLabel.FromSensitivity"/> (issue #72, AC1/AC6).
/// Covers the four recognized Outlook sensitivity integers, the null input, and an
/// out-of-range value, asserting the documented 0=normal/1=personal/2=private/3=confidential
/// mapping with null for anything else.
/// </summary>
[TestClass]
public sealed class EventSensitivityLabelTests
{
    [DataTestMethod]
    [DataRow(0, "normal")]
    [DataRow(1, "personal")]
    [DataRow(2, "private")]
    [DataRow(3, "confidential")]
    public void FromSensitivity_should_map_recognized_values(int sensitivity, string expected)
    {
        // Arrange / Act
        var label = EventSensitivityLabel.FromSensitivity(sensitivity);

        // Assert
        label.Should().Be(expected);
    }

    [TestMethod]
    public void FromSensitivity_should_return_null_for_null_input()
    {
        // Arrange / Act
        var label = EventSensitivityLabel.FromSensitivity(null);

        // Assert
        label.Should().BeNull("a null sensitivity has no label");
    }

    [TestMethod]
    public void FromSensitivity_should_return_null_for_out_of_range_value()
    {
        // Arrange / Act
        var label = EventSensitivityLabel.FromSensitivity(99);

        // Assert
        label.Should().BeNull("unrecognized sensitivity integers map to null");
    }
}
