using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

namespace OpenClaw.MailBridge.Tests;

public class CodexWebSetupScriptTests
{
    [Test]
    public async Task Setup_script_should_restore_the_solution_with_the_available_dotnet_sdk()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["OS"] = "Linux"
        });
        harness.WriteFile("global.json", """
{
  "sdk": {
    "version": "10.0.201"
  }
}
""");
        harness.WriteFile("OpenClaw.MailBridge.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        harness.WriteFile("src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj", """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
  </PropertyGroup>
</Project>
""");
        harness.WriteExecutable(
            "dotnet",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_DOTNET_LOG}"

case "${1:-}" in
  --version)
    printf '10.0.201\n'
    ;;
  --list-sdks)
    printf '10.0.201 [%s/sdk]\n' "$(dirname "$0")"
    ;;
  restore)
    exit 0
    ;;
  tool)
    exit 0
    ;;
  *)
    exit 0
    ;;
esac
""");

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> .NET repository detected");
        result.StdOut.Should().Contain("==> global.json SDK: 10.0.201");
        result.StdOut.Should().Contain("==> dotnet SDK: 10.0.201");
        result.StdOut.Should().Contain("==> Enabling Windows targeting for non-Windows restore");
        result.StdOut.Should().Contain("==> dotnet restore -p:EnableWindowsTargeting=true OpenClaw.MailBridge.sln");
        harness.ReadDotnetLog().Should().Contain(line => line == "--version");
        harness.ReadDotnetLog().Should().Contain(line => line == "--list-sdks");
        harness.ReadDotnetLog().Should().Contain(line => line == "restore -p:EnableWindowsTargeting=true OpenClaw.MailBridge.sln");
    }

    [Test]
    public async Task Setup_script_should_restore_local_dotnet_tools_when_manifest_exists()
    {
        using var harness = new CodexWebSetupScriptHarness();
        harness.WriteFile("global.json", """
{
  "sdk": {
    "version": "10.0.201"
  }
}
""");
        harness.WriteFile("OpenClaw.MailBridge.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        harness.WriteFile(".config/dotnet-tools.json", """
{
  "version": 1,
  "isRoot": true,
  "tools": {}
}
""");
        harness.WriteExecutable(
            "dotnet",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_DOTNET_LOG}"

case "${1:-}" in
  --version)
    printf '10.0.201\n'
    ;;
  --list-sdks)
    printf '10.0.201 [%s/sdk]\n' "$(dirname "$0")"
    ;;
  tool)
    exit 0
    ;;
  restore)
    exit 0
    ;;
  *)
    exit 0
    ;;
esac
""");

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> dotnet tool restore");
        harness.ReadDotnetLog().Should().Contain(line => line == "tool restore");
    }

    [Test]
    public async Task Setup_script_should_install_the_pinned_dotnet_sdk_when_dotnet_is_missing()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["OS"] = "Linux",
            ["HARNESS_BASH_ENV"] = """
curl() {
  local output_path=""

  while (($#)); do
    case "$1" in
      -o)
        output_path="$2"
        shift 2
        ;;
      *)
        shift
        ;;
    esac
  done

  cat >"$output_path" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

install_dir=""

while (($#)); do
  case "$1" in
    --install-dir)
      install_dir="$2"
      shift 2
      ;;
    *)
      shift
      ;;
  esac
done

mkdir -p "$install_dir"

cat >"$install_dir/dotnet" <<'EOS'
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_DOTNET_LOG}"

case "${1:-}" in
  --version)
    printf '10.0.201\n'
    ;;
  --list-sdks)
    printf '10.0.201 [%s/sdk]\n' "$(dirname "$0")"
    ;;
  restore)
    exit 0
    ;;
  tool)
    exit 0
    ;;
  *)
    exit 0
    ;;
esac
EOS

chmod +x "$install_dir/dotnet"
EOF
}
"""
        });
        harness.WriteFile("global.json", """
{
  "sdk": {
    "version": "10.0.201"
  }
}
""");
        harness.WriteFile("OpenClaw.MailBridge.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        harness.WriteFile("src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj", """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
  </PropertyGroup>
</Project>
""");

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> dotnet not found on PATH; installing SDK 10.0.201");
        result.StdOut.Should().Contain("==> dotnet SDK: 10.0.201");
        harness.ReadDotnetLog().Should().Contain(line => line == "--version");
        harness.ReadDotnetLog().Should().Contain(line => line == "restore -p:EnableWindowsTargeting=true OpenClaw.MailBridge.sln");
    }

    [Test]
    public async Task Setup_script_should_run_the_repo_bootstrap_hook_when_present()
    {
        using var harness = new CodexWebSetupScriptHarness();
        harness.WriteFile(".codex/setup.sh", """
#!/usr/bin/env bash
set -Eeuo pipefail

printf 'hook-ran\n' > .codex/hook-result.txt
""");
        harness.MakeRepositoryFileExecutable(".codex/setup.sh");

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> Running repo bootstrap hook: .codex/setup.sh");
        harness.ReadFile(".codex/hook-result.txt").Should().Contain("hook-ran");
    }

    [Test]
    public async Task Setup_script_should_report_git_metadata_and_status()
    {
        using var harness = new CodexWebSetupScriptHarness(new Dictionary<string, string?>
        {
            ["FAKE_GIT_BRANCH"] = "feature/work",
            ["FAKE_GIT_SHA"] = "abc1234",
            ["FAKE_GIT_STATUS"] = " M .codex/codex-web-setup.sh\n?? temp.txt"
        });

        var result = await harness.RunAsync();

        result.ExitCode.Should().Be(0, result.CombinedOutput);
        result.StdOut.Should().Contain("==> HEAD ref: feature/work");
        result.StdOut.Should().Contain("==> HEAD sha: abc1234");
        result.StdOut.Should().Contain(" M .codex/codex-web-setup.sh");
        result.StdOut.Should().Contain("?? temp.txt");
        harness.ReadGitLog().Should().Contain(line => line == "status --short");
    }
}

internal sealed class CodexWebSetupScriptHarness : IDisposable
{
    private readonly Dictionary<string, string?> _environment;
    private readonly string? _bashEnvContent;
    private static readonly string BashExecutablePath = ResolveBashExecutablePath();
    private string BashEnvPath => Path.Combine(RootDirectory, "bash-env.sh");
    private string FakeGitScriptPath => Path.Combine(BinDirectory, "git");

    public CodexWebSetupScriptHarness(Dictionary<string, string?>? environmentOverrides = null)
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), $"codex-web-setup-tests-{Guid.NewGuid():N}");
        RepositoryRoot = Path.Combine(RootDirectory, "repo");
        BinDirectory = Path.Combine(RootDirectory, "bin");
        HomeDirectory = Path.Combine(RootDirectory, "home");
        GitLogPath = Path.Combine(RootDirectory, "fake-git.log");
        DotnetLogPath = Path.Combine(RootDirectory, "fake-dotnet.log");
        DotnetInstallDirectory = Path.Combine(RootDirectory, "dotnet");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(RepositoryRoot);
        Directory.CreateDirectory(BinDirectory);
        Directory.CreateDirectory(HomeDirectory);

        _environment = new Dictionary<string, string?>
        {
            ["FAKE_GIT_ROOT"] = RepositoryRoot,
            ["FAKE_GIT_LOG"] = GitLogPath,
            ["FAKE_GIT_BRANCH"] = "main",
            ["FAKE_GIT_SHA"] = "c832959",
            ["FAKE_GIT_STATUS"] = string.Empty,
            ["FAKE_DOTNET_LOG"] = DotnetLogPath,
            ["DOTNET_INSTALL_DIR"] = DotnetInstallDirectory
        };

        if (environmentOverrides is not null)
        {
            foreach (var entry in environmentOverrides)
            {
                _environment[entry.Key] = entry.Value;
            }
        }

        _environment.TryGetValue("HARNESS_BASH_ENV", out _bashEnvContent);
        _environment.Remove("HARNESS_BASH_ENV");

        CreateFakeGitScript();
        CreateBashEnvironmentFile();
    }

    public string RootDirectory { get; }

    public string RepositoryRoot { get; }

    public string BinDirectory { get; }

    public string HomeDirectory { get; }

    public string GitLogPath { get; }

    public string DotnetLogPath { get; }

    public string DotnetInstallDirectory { get; }

    public void WriteFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, contents.ReplaceLineEndings("\n"));
    }

    public string ReadFile(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(fullPath);
    }

    public void WriteExecutable(string name, string contents)
    {
        var fullPath = Path.Combine(BinDirectory, name);
        File.WriteAllText(fullPath, contents.ReplaceLineEndings("\n"));
        MakeExecutable(fullPath);
    }

    public void MakeRepositoryFileExecutable(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        MakeExecutable(fullPath);
    }

    public IReadOnlyList<string> ReadGitLog() =>
        File.Exists(GitLogPath)
            ? File.ReadAllLines(GitLogPath)
            : Array.Empty<string>();

    public IReadOnlyList<string> ReadDotnetLog() =>
        File.Exists(DotnetLogPath)
            ? File.ReadAllLines(DotnetLogPath)
            : Array.Empty<string>();

    public async Task<ProcessResult> RunAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = BashExecutablePath,
                WorkingDirectory = RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("--noprofile");
        process.StartInfo.ArgumentList.Add("--norc");
        process.StartInfo.ArgumentList.Add(GetPathForShell(ScriptPath));
        process.StartInfo.Environment["PATH"] = BuildProcessPath();
        process.StartInfo.Environment["HOME"] = HomeDirectory;
        process.StartInfo.Environment["BASH_ENV"] = GetPathForShell(BashEnvPath);

        foreach (var entry in _environment)
        {
            process.StartInfo.Environment[entry.Key] = ConvertEnvironmentValue(entry.Key, entry.Value);
        }

        process.Start();

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
        WriteExecutable(
            "git",
            """
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
      --abbrev-ref)
        if [[ "${3:-}" == "HEAD" ]]; then
          printf '%s\n' "${FAKE_GIT_BRANCH:-main}"
          exit 0
        fi
        ;;
      --short)
        if [[ "${3:-}" == "HEAD" ]]; then
          printf '%s\n' "${FAKE_GIT_SHA:-c832959}"
          exit 0
        fi
        ;;
    esac
    ;;
  status)
    if [[ "${2:-}" == "--short" ]]; then
      printf '%b\n' "${FAKE_GIT_STATUS:-}"
      exit 0
    fi
    ;;
esac

printf 'unexpected git invocation: %s\n' "$*" >&2
exit 99
""");
    }

    private void CreateBashEnvironmentFile()
    {
        var fakeGitPath = GetPathForShell(FakeGitScriptPath).Replace("'", "'\"'\"'");
        var content = $"git(){Environment.NewLine}{{{Environment.NewLine}  bash '{fakeGitPath}' \"$@\"{Environment.NewLine}}}{Environment.NewLine}";

        if (!string.IsNullOrWhiteSpace(_bashEnvContent))
        {
            content = string.Concat(content, "\n", _bashEnvContent.ReplaceLineEndings("\n"), "\n");
        }

        File.WriteAllText(BashEnvPath, content.ReplaceLineEndings("\n"));
    }

    private string BuildProcessPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Join(
                Path.PathSeparator,
                BinDirectory,
                "/usr/bin",
                "/bin");
        }

        return string.Join(
            Path.PathSeparator,
            BinDirectory,
            @"C:\Program Files\Git\bin",
            @"C:\Program Files\Git\usr\bin");
    }

    private string ConvertEnvironmentValue(string key, string? value)
    {
        if (string.IsNullOrEmpty(value) || !OperatingSystem.IsWindows())
        {
            return value ?? string.Empty;
        }

        return key switch
        {
            "FAKE_GIT_ROOT" or "FAKE_GIT_LOG" or "FAKE_DOTNET_LOG" or "DOTNET_INSTALL_DIR" => GetPathForShell(value),
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

    private void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = BashExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("-lc");
        process.StartInfo.ArgumentList.Add($"chmod +x '{EscapeForBash(GetPathForShell(path))}'");
        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to mark '{path}' executable. Output: {process.StandardOutput.ReadToEnd()}{process.StandardError.ReadToEnd()}");
        }
    }

    private static string EscapeForBash(string value) =>
        value.Replace("'", "'\"'\"'");

    private string GetPathForShell(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return path;
        }

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
