using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class CodexWebSetupScriptTests
{
    [TestMethod]
    public async Task Setup_script_should_reuse_existing_tooling_and_skip_python_dependencies_without_a_manifest()
    {
        using var harness = new CodexWebSetupScriptHarness();

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("=== drm-copilot setup: start ===");
        result
            .StdOut.Should()
            .Contain("PYTHON_EXE not provided; falling back to python3 from PATH");
        result.StdOut.Should().Contain("Poetry present (");
        result
            .StdOut.Should()
            .Contain("No pyproject.toml found; skipping Python dependency installation.");
        result.StdOut.Should().Contain("pwsh 7.4.13 present; meets requirement (7.4.0).");
        result.StdOut.Should().Contain("PowerShell installed. Checking modules from PSGallery...");
        result.StdOut.Should().Contain("Importing PoshQC module (required for parity)...");
        result.StdOut.Should().Contain("actionlint already installed");
        result.StdOut.Should().Contain("=== drm-copilot setup: done ===");
        harness.ReadGitLog().Should().Contain(line => line == "rev-parse --show-toplevel");
        harness.ReadPoetryLog().Should().Contain(line => line == "--version");
        harness
            .ReadPoetryLog()
            .Should()
            .Contain(line => line == "config virtualenvs.in-project true --local");
        harness.ReadPwshLog().Should().Contain(line => line == "--version");
    }

    [TestMethod]
    public async Task Setup_script_should_configure_a_custom_poetry_feed_and_install_locked_dependencies()
    {
        using var harness = new CodexWebSetupScriptHarness(
            new Dictionary<string, string?>
            {
                ["POETRY_PYPI_URL"] = "https://packages.example.test/simple/",
            }
        );
        harness.WriteFile(
            "poetry.lock",
            """
[[package]]
name = "demo"
version = "1.0.0"
"""
        );

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result
            .StdOut.Should()
            .Contain("Using custom POETRY_PYPI_URL=https://packages.example.test/simple/");
        result
            .StdOut.Should()
            .Contain("poetry.lock found; installing locked dependencies with --with dev...");
        harness
            .ReadPoetryLog()
            .Should()
            .Contain(line =>
                line == "config repositories.main https://packages.example.test/simple/"
            );
        harness.ReadPoetryLog().Should().Contain(line => line == "config pypi-token.main ");
        harness
            .ReadPoetryLog()
            .Should()
            .Contain(line => line == "install --no-interaction --no-ansi --with dev");
        harness
            .ReadPoetryLog()
            .Should()
            .NotContain(line => line == "lock --no-interaction --no-ansi");
    }

    [TestMethod]
    public async Task Setup_script_should_lock_then_install_python_dependencies_when_only_pyproject_exists()
    {
        using var harness = new CodexWebSetupScriptHarness();
        harness.WriteFile(
            "pyproject.toml",
            """
name = "open-claw-bridge"
version = "0.1.0"
"""
        );

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result
            .StdOut.Should()
            .Contain("poetry.lock missing; locking and installing with --with dev...");
        harness.ReadPoetryLog().Should().Contain(line => line == "lock --no-interaction --no-ansi");
        harness
            .ReadPoetryLog()
            .Should()
            .Contain(line => line == "install --no-interaction --no-ansi --with dev");
    }

    [TestMethod]
    public async Task Setup_script_should_skip_pypi_connectivity_when_offline_install_is_allowed()
    {
        using var harness = new CodexWebSetupScriptHarness(
            new Dictionary<string, string?> { ["ALLOW_OFFLINE_INSTALL"] = "1" }
        );

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result
            .StdOut.Should()
            .Contain("ALLOW_OFFLINE_INSTALL=1 set; skipping PyPI connectivity check.");
        harness.ReadCurlLog().Should().BeEmpty();
    }
}
