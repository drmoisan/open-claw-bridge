using System.Diagnostics;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace OpenClaw.MailBridge.Tests;

public class CodexWebSetupScriptTests
{
    [Test]
    public async Task Setup_script_should_create_a_valid_minimal_project_config()
    {
        using var harness = new CodexWebSetupScriptHarness();

        var result = await harness.RunAsync("y\nn\nn\n");

        result.ExitCode.Should().Be(0, result.CombinedOutput);

        var configPath = Path.Combine(harness.RepositoryRoot, ".codex", "config.toml");
        File.Exists(configPath).Should().BeTrue();
        File.ReadAllText(configPath).ReplaceLineEndings("\n").Should().Be(ExpectedProjectConfig);
        result.CombinedOutput.Should().Contain("Created .codex/config.toml");
    }

    [Test]
    public async Task Setup_script_should_preserve_an_existing_project_config()
    {
        using var harness = new CodexWebSetupScriptHarness();
        var codexDirectory = Directory.CreateDirectory(Path.Combine(harness.RepositoryRoot, ".codex"));
        var configPath = Path.Combine(codexDirectory.FullName, "config.toml");
        const string existingConfig = "model = \"gpt-5.4\"\n";

        await File.WriteAllTextAsync(configPath, existingConfig);

        var result = await harness.RunAsync("y\nn\nn\n");

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        File.ReadAllText(configPath).Should().Be(existingConfig);
        result.CombinedOutput.Should().Contain("Found existing .codex/config.toml");
    }

    [Test]
    public async Task Setup_script_should_fail_when_no_github_remote_exists()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["FAKE_GIT_REMOTE_URL"] = "https://example.com/example/repo.git"
        });

        var result = await harness.RunAsync(string.Empty);

        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("ERROR: No GitHub remote found.");
    }

    [Test]
    public async Task Setup_script_should_normalize_ssh_remote_urls_in_its_summary()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["FAKE_GIT_BRANCH"] = "feature/codex-web",
            ["FAKE_GIT_REMOTE_URL"] = "git@github.com:octo/example.git"
        });

        var result = await harness.RunAsync("n\nn\nn\n");

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("URL:     https://github.com/octo/example");
        result.StdOut.Should().Contain("Branch:  https://github.com/octo/example/tree/feature/codex-web");
    }

    private const string ExpectedProjectConfig = """
# Project-scoped Codex configuration
# See:
#   https://developers.openai.com/codex/config-basic/
#   https://developers.openai.com/codex/config-reference/

# Keep this intentionally minimal and conservative.
# Adjust only if your team has a defined Codex policy.

# Example:
# model = "gpt-5.4"
# approval_policy = "on-request"
# sandbox_mode = "workspace-write"

# Trust is configured in ~/.codex/config.toml under
# [projects."/absolute/path/to/project"], because Codex only loads this
# project-scoped file after the project is already trusted.
""".ReplaceLineEndings("\n");
}

internal sealed class CodexWebSetupScriptHarness : IDisposable
{
    private readonly Dictionary<string, string?> _environment;

    public CodexWebSetupScriptHarness(Dictionary<string, string?>? environmentOverrides = null)
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), $"codex-web-setup-tests-{Guid.NewGuid():N}");
        RepositoryRoot = Path.Combine(RootDirectory, "repo");
        BinDirectory = Path.Combine(RootDirectory, "bin");
        GitLogPath = Path.Combine(RootDirectory, "fake-git.log");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(RepositoryRoot);
        Directory.CreateDirectory(BinDirectory);

        CreateFakeGitScript();

        _environment = new Dictionary<string, string?>
        {
            ["FAKE_GIT_ROOT"] = RepositoryRoot,
            ["FAKE_GIT_LOG"] = GitLogPath,
            ["FAKE_GIT_BRANCH"] = "main",
            ["FAKE_GIT_DEFAULT_BRANCH"] = "main",
            ["FAKE_GIT_REMOTE_URL"] = "https://github.com/example/repo.git",
            ["FAKE_GIT_REMOTES"] = "origin",
            ["FAKE_GIT_HAS_INITIAL_COMMIT"] = "1",
            ["FAKE_GIT_DIRTY"] = "0",
            ["FAKE_GIT_REMOTE_BRANCH_EXISTS"] = "0"
        };

        if (environmentOverrides is not null)
        {
            foreach (var entry in environmentOverrides)
            {
                _environment[entry.Key] = entry.Value;
            }
        }
    }

    public string RootDirectory { get; }

    public string RepositoryRoot { get; }

    public string BinDirectory { get; }

    public string GitLogPath { get; }

    public async Task<ProcessResult> RunAsync(string standardInput)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                WorkingDirectory = RepositoryRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(ScriptPath);
        process.StartInfo.Environment["PATH"] = $"{BinDirectory}:{Environment.GetEnvironmentVariable("PATH")}";

        foreach (var entry in _environment)
        {
            process.StartInfo.Environment[entry.Key] = entry.Value;
        }

        process.Start();

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput);
        }

        process.StandardInput.Close();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, await stdOutTask, await stdErrTask);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }

    private static string ScriptPath => FindRepositoryRoot() is { } repositoryRoot
        ? Path.Combine(repositoryRoot, ".codex", "codex-web-setup.sh")
        : throw new DirectoryNotFoundException("Could not locate repository root for codex-web-setup.sh tests.");

    private void CreateFakeGitScript()
    {
        var scriptPath = Path.Combine(BinDirectory, "git");
        const string script = """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_GIT_LOG}"

case "${1:-}" in
  rev-parse)
    case "${2:-}" in
      --show-toplevel)
        printf '%s\n' "${FAKE_GIT_ROOT}"
        exit 0
        ;;
      --verify)
        [[ "${FAKE_GIT_HAS_INITIAL_COMMIT:-1}" == "1" ]]
        exit $?
        ;;
      --abbrev-ref)
        printf '%s\n' "${FAKE_GIT_BRANCH:-main}"
        exit 0
        ;;
    esac
    ;;

  symbolic-ref)
    if [[ "${2:-}" == "refs/remotes/origin/HEAD" && -n "${FAKE_GIT_DEFAULT_BRANCH:-}" ]]; then
      printf 'refs/remotes/origin/%s\n' "${FAKE_GIT_DEFAULT_BRANCH}"
      exit 0
    fi

    exit 1
    ;;

  remote)
    if [[ $# -eq 0 ]]; then
      if [[ -n "${FAKE_GIT_REMOTES:-}" ]]; then
        printf '%b\n' "${FAKE_GIT_REMOTES}"
      fi

      exit 0
    fi

    if [[ "${2:-}" == "get-url" ]]; then
      case "${3:-}" in
        origin)
          printf '%s\n' "${FAKE_GIT_REMOTE_URL:-https://github.com/example/repo.git}"
          exit 0
          ;;
        *)
          exit 1
          ;;
      esac
    fi
    ;;

  diff)
    if [[ "${FAKE_GIT_DIRTY:-0}" == "1" ]]; then
      exit 1
    fi

    exit 0
    ;;

  fetch)
    exit 0
    ;;

  ls-remote)
    if [[ "${FAKE_GIT_REMOTE_BRANCH_EXISTS:-0}" == "1" ]]; then
      exit 0
    fi

    exit 2
    ;;

  push)
    exit 0
    ;;
esac

printf 'unexpected git invocation: %s\n' "$*" >&2
exit 99
""";

        File.WriteAllText(scriptPath, script);
        Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            ArgumentList = { "+x", scriptPath },
            UseShellExecute = false
        })!.WaitForExit();
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenClaw.MailBridge.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public string CombinedOutput => string.Concat(StdOut, StdErr);
}