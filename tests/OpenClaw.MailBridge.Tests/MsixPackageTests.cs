using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// MSTest tests that verify the structural integrity of the MSIX installer artifacts:
/// Package.appxmanifest, icon assets, publish profiles, and optional publish output directories.
/// </summary>
[TestClass]
public class MsixPackageTests
{
    // Namespace URIs used by Package.appxmanifest
    private static readonly XNamespace ManifestNs =
        "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap5Ns =
        "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";

    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Walks up the directory tree from <see cref="AppContext.BaseDirectory"/> until it finds
    /// the directory that contains <c>OpenClaw.MailBridge.sln</c>, which is the repo root.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenClaw.MailBridge.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Cannot locate repo root (directory containing OpenClaw.MailBridge.sln) "
                + $"starting from '{AppContext.BaseDirectory}'."
        );
    }

    // ─── Manifest: existence & well-formedness ──────────────────────────────────

    /// <summary>
    /// Verifies that the MSIX package manifest exists at the expected repo-relative path.
    /// </summary>
    [TestMethod]
    public void Manifest_ParsesAsValidXml()
    {
        var manifestPath = Path.Combine(RepoRoot, "installer", "Package.appxmanifest");
        var xml = XDocument.Load(manifestPath);
        xml.Should().NotBeNull("installer/Package.appxmanifest must exist and be valid XML");
    }

    // ─── startupTask extension ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the manifest declares a <c>windows.startupTask</c> extension with the
    /// correct <c>TaskId</c> and <c>Executable</c> attribute pointing at the bridge binary.
    /// </summary>
    [TestMethod]
    public void Manifest_ContainsStartupTaskExtension_WithCorrectExecutable()
    {
        var manifestPath = Path.Combine(RepoRoot, "installer", "Package.appxmanifest");
        var xml = XDocument.Load(manifestPath);

        // Locate the uap5:Extension element whose Category is "windows.startupTask"
        var extensionEl = xml.Descendants(Uap5Ns + "Extension")
            .FirstOrDefault(e => (string?)e.Attribute("Category") == "windows.startupTask");

        extensionEl.Should().NotBeNull("manifest must contain a windows.startupTask extension");

        // The Executable attribute on the Extension element must point to the bridge host
        var executable = (string?)extensionEl!.Attribute("Executable");
        executable
            .Should()
            .Be(
                @"bridge\OpenClaw.MailBridge.exe",
                "startupTask Executable must be bridge\\OpenClaw.MailBridge.exe"
            );

        // The nested uap5:StartupTask element must carry TaskId="OpenClawMailBridge"
        var startupTaskEl = extensionEl.Element(Uap5Ns + "StartupTask");
        startupTaskEl.Should().NotBeNull("uap5:StartupTask element must be present");
        var taskId = (string?)startupTaskEl!.Attribute("TaskId");
        taskId.Should().Be("OpenClawMailBridge", "StartupTask TaskId must be 'OpenClawMailBridge'");
    }

    // ─── No windows.service ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the manifest does NOT declare a <c>windows.service</c> extension.
    /// The bridge uses Outlook COM interop and must run in the interactive user session;
    /// a Windows Service (Session 0) is incompatible with COM-activation via Outlook.
    /// </summary>
    [TestMethod]
    public void Manifest_DoesNotDeclareWindowsService()
    {
        var manifestPath = Path.Combine(RepoRoot, "installer", "Package.appxmanifest");
        var rawText = File.ReadAllText(manifestPath);
        rawText
            .Should()
            .NotContain(
                "windows.service",
                "MSIX manifest must not declare a Windows Service extension"
            );
    }

    // ─── Identity Version ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the manifest <c>Identity</c> element carries a valid 4-part version number.
    /// </summary>
    [TestMethod]
    public void Manifest_IdentityVersion_IsValid4PartVersion()
    {
        var manifestPath = Path.Combine(RepoRoot, "installer", "Package.appxmanifest");
        var xml = XDocument.Load(manifestPath);

        var identityEl = xml.Root?.Element(ManifestNs + "Identity");
        identityEl.Should().NotBeNull("Package element must contain an Identity child");

        var version = (string?)identityEl!.Attribute("Version");
        version.Should().NotBeNullOrWhiteSpace("Identity Version attribute must be set");
        Regex
            .IsMatch(version!, @"^\d+\.\d+\.\d+\.\d+$")
            .Should()
            .BeTrue($"Identity Version '{version}' must match the 4-part format N.N.N.N");
    }

    // ─── Required icon assets ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that all four required MSIX icon assets exist in <c>installer/Assets/</c>.
    /// </summary>
    [TestMethod]
    public void RequiredIconAssets_AllExist()
    {
        var assetsDir = Path.Combine(RepoRoot, "installer", "Assets");
        var required = new[]
        {
            "Square44x44Logo.png",
            "Square150x150Logo.png",
            "Wide310x150Logo.png",
            "StoreLogo.png",
        };

        foreach (var fileName in required)
        {
            var fullPath = Path.Combine(assetsDir, fileName);
            File.Exists(fullPath).Should().BeTrue($"installer/Assets/{fileName} must exist");

            // Verify valid PNG magic bytes (89 50 4E 47)
            var header = new byte[4];
            using var fs = File.OpenRead(fullPath);
            fs.ReadExactly(header);
            header[0].Should().Be(0x89, $"{fileName} must start with PNG magic byte 0x89");
            header[1].Should().Be(0x50, $"{fileName} must start with PNG magic byte 0x50 ('P')");
            header[2].Should().Be(0x4E, $"{fileName} must start with PNG magic byte 0x4E ('N')");
            header[3].Should().Be(0x47, $"{fileName} must start with PNG magic byte 0x47 ('G')");
        }
    }

    // ─── Publish profiles ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the bridge publish profile exists and specifies directory-layout publish
    /// settings (<c>PublishSingleFile=false</c>, <c>SelfContained=true</c>, <c>RuntimeIdentifier=win-x64</c>).
    /// </summary>
    [TestMethod]
    public void BridgePublishProfile_HasDirectoryLayoutSettings()
    {
        var profilePath = Path.Combine(
            RepoRoot,
            "src",
            "OpenClaw.MailBridge",
            "Properties",
            "PublishProfiles",
            "msix.pubxml"
        );
        File.Exists(profilePath).Should().BeTrue("bridge msix.pubxml must exist");

        var content = File.ReadAllText(profilePath);
        content
            .Should()
            .Contain(
                "<PublishSingleFile>false</PublishSingleFile>",
                "bridge publish profile must set PublishSingleFile=false for directory layout"
            );
        content
            .Should()
            .Contain(
                "<SelfContained>true</SelfContained>",
                "bridge publish profile must set SelfContained=true"
            );
        content
            .Should()
            .Contain(
                "<RuntimeIdentifier>win-x64</RuntimeIdentifier>",
                "bridge publish profile must target win-x64"
            );
    }

    // ─── Optional publish-output assertions (skipped if env var absent) ──────────

    /// <summary>
    /// If <c>MSIX_PUBLISH_DIR</c> is set, verifies that the bridge executable was published
    /// to <c>MSIX_PUBLISH_DIR/bridge/OpenClaw.MailBridge.exe</c>.
    /// Marks the test Inconclusive when the environment variable is not present.
    /// </summary>
    [TestMethod]
    public void PublishOutput_BridgeDirectory_ContainsBridgeExecutable()
    {
        var msixPublishDir = Environment.GetEnvironmentVariable("MSIX_PUBLISH_DIR");
        if (string.IsNullOrWhiteSpace(msixPublishDir))
        {
            Assert.Inconclusive("MSIX_PUBLISH_DIR not set – skipping publish-output assertion");
        }

        var exePath = Path.Combine(msixPublishDir!, "bridge", "OpenClaw.MailBridge.exe");
        File.Exists(exePath).Should().BeTrue($"bridge executable must exist at '{exePath}'");
    }

    /// <summary>
    /// If <c>MSIX_PUBLISH_DIR</c> is set, verifies that the client executable was published
    /// to <c>MSIX_PUBLISH_DIR/client/OpenClaw.MailBridge.Client.exe</c>.
    /// Marks the test Inconclusive when the environment variable is not present.
    /// </summary>
    [TestMethod]
    public void PublishOutput_ClientDirectory_ContainsClientExecutable()
    {
        var msixPublishDir = Environment.GetEnvironmentVariable("MSIX_PUBLISH_DIR");
        if (string.IsNullOrWhiteSpace(msixPublishDir))
        {
            Assert.Inconclusive("MSIX_PUBLISH_DIR not set – skipping publish-output assertion");
        }

        var exePath = Path.Combine(msixPublishDir!, "client", "OpenClaw.MailBridge.Client.exe");
        File.Exists(exePath).Should().BeTrue($"client executable must exist at '{exePath}'");
    }
}
