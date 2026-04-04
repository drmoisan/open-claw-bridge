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
    public async Task Setup_script_should_warn_and_skip_remote_steps_when_no_github_remote_exists()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["FAKE_GIT_REMOTE_URL"] = "https://example.com/example/repo.git"
        });

        var result = await harness.RunAsync(string.Empty);

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.CombinedOutput.Should().Contain("WARN: No GitHub remote found. Skipping fetch/push steps.");
        result.StdOut.Should().Contain("Remote:  (not configured)");
        result.StdOut.Should().Contain("No GitHub remote was configured in this checkout, so fetch/push steps were skipped.");
        harness.ReadGitLog().Should().NotContain(line => line.StartsWith("fetch", StringComparison.Ordinal));
        harness.ReadGitLog().Should().NotContain(line => line.StartsWith("push", StringComparison.Ordinal));
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

    [Test]
    public async Task Setup_script_should_guess_default_branch_from_the_selected_remote_name()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["FAKE_GIT_REMOTES"] = "upstream",
            ["FAKE_GIT_REMOTE_NAME"] = "upstream",
            ["FAKE_GIT_REMOTE_URL"] = "https://github.com/octo/example.git",
            ["FAKE_GIT_DEFAULT_BRANCH"] = "trunk"
        });

        var result = await harness.RunAsync("n\nn\nn\n");

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> Remote default branch guess: trunk");
    }

    private static readonly string ExpectedProjectConfig = """
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
""".ReplaceLineEndings("\n") + "\n";
}

internal sealed class CodexWebSetupScriptHarness : IDisposable
{
    private readonly Dictionary<string, string?> _environment;
    private static readonly string BashExecutablePath = ResolveBashExecutablePath();
    private string FakeGitScriptPath => Path.Combine(BinDirectory, "git");
    private string BashEnvironmentPath => Path.Combine(BinDirectory, "bash-env.sh");

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
        CreateBashEnvironmentScript();

        _environment = new Dictionary<string, string?>
        {
            ["FAKE_GIT_ROOT"] = RepositoryRoot,
            ["FAKE_GIT_LOG"] = GitLogPath,
            ["FAKE_GIT_BRANCH"] = "main",
            ["FAKE_GIT_DEFAULT_BRANCH"] = "main",
            ["FAKE_GIT_REMOTE_NAME"] = "origin",
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

    public IReadOnlyList<string> ReadGitLog() =>
        File.Exists(GitLogPath)
            ? File.ReadAllLines(GitLogPath)
            : Array.Empty<string>();

    public async Task<ProcessResult> RunAsync(string standardInput)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = BashExecutablePath,
                WorkingDirectory = RepositoryRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("--noprofile");
        process.StartInfo.ArgumentList.Add("--norc");
        process.StartInfo.ArgumentList.Add(GetScriptPathForShell());
        process.StartInfo.Environment["BASH_ENV"] = GetBashEnvironmentPathForShell();
        process.StartInfo.Environment["FAKE_GIT_SCRIPT_PATH"] = GetFakeGitScriptPathForShell();

        foreach (var entry in _environment)
        {
            process.StartInfo.Environment[entry.Key] = ConvertEnvironmentValueForShell(entry.Key, entry.Value);
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
    if [[ "${2:-}" == "refs/remotes/${FAKE_GIT_REMOTE_NAME:-origin}/HEAD" && -n "${FAKE_GIT_DEFAULT_BRANCH:-}" ]]; then
      printf 'refs/remotes/%s/%s\n' "${FAKE_GIT_REMOTE_NAME:-origin}" "${FAKE_GIT_DEFAULT_BRANCH}"
      exit 0
    fi

    exit 1
    ;;

  remote)
    if [[ $# -eq 1 ]]; then
      if [[ -n "${FAKE_GIT_REMOTES:-}" ]]; then
        printf '%b\n' "${FAKE_GIT_REMOTES}"
      fi

      exit 0
    fi

    if [[ "${2:-}" == "get-url" ]]; then
      case "${3:-}" in
        "${FAKE_GIT_REMOTE_NAME:-origin}")
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

        File.WriteAllText(FakeGitScriptPath, script);
    }

    private void CreateBashEnvironmentScript()
    {
        const string script = """
git() {
  bash "$FAKE_GIT_SCRIPT_PATH" "$@"
}
""";

        File.WriteAllText(BashEnvironmentPath, script);
    }

    private string GetScriptPathForShell() =>
        OperatingSystem.IsWindows() ? ToBashPath(ScriptPath) : ScriptPath;

    private string GetBashEnvironmentPathForShell() =>
        OperatingSystem.IsWindows() ? ToBashPath(BashEnvironmentPath) : BashEnvironmentPath;

    private string GetFakeGitScriptPathForShell() =>
        OperatingSystem.IsWindows() ? ToBashPath(FakeGitScriptPath) : FakeGitScriptPath;

    private static string? ConvertEnvironmentValueForShell(string key, string? value)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(value))
        {
            return value;
        }

        return key switch
        {
            "FAKE_GIT_ROOT" or "FAKE_GIT_LOG" => ToBashPath(value),
            _ => value
        };
    }

    private static string ResolveBashExecutablePath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        string[] candidates =
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files\Git\usr\bin\bash.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "bash";
    }

    private static string ToBashPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Could not determine path root for '{path}'.");

        if (root.Length < 2 || root[1] != ':')
        {
            return fullPath.Replace('\\', '/');
        }

        var driveLetter = char.ToLowerInvariant(root[0]);
        var relativePath = fullPath[root.Length..].Replace('\\', '/');
        return string.IsNullOrEmpty(relativePath)
            ? $"/{driveLetter}"
            : $"/{driveLetter}/{relativePath}";
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
