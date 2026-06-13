using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterVersionTests
{
    [TestMethod]
    public void DefaultAdapterVersion_should_report_1_0_0_from_the_assembly_version()
    {
        // The in-process test web factory overrides AdapterVersion to "test-version", so the
        // envelope meta value cannot assert the real version. The assembly-derived
        // DefaultAdapterVersion is asserted directly. The HostAdapter csproj declares
        // <Version>1.0.0</Version>, which is the major bump signalling the breaking route change.
        HostAdapterOptions.DefaultAdapterVersion.Should().Be("1.0.0");
    }
}
