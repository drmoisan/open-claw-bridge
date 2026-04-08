using System.Diagnostics;

namespace OpenClaw.MailBridge.Tests;

internal sealed class CodexWebSetupScriptHarness : IDisposable
{
    private readonly Dictionary<string, string?> _environment;
    private readonly string? _bashEnvContent;
    private static readonly string BashExecutablePath = ResolveBashExecutablePath();
    private string BashEnvPath => Path.Combine(RootDirectory, "bash-env.sh");
    private string FakeGitScriptPath => Path.Combine(BinDirectory, "git");

    public CodexWebSetupScriptHarness(Dictionary<string, string?>? environmentOverrides = null)
    {
        RootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"codex-web-setup-tests-{Guid.NewGuid():N}"
        );
        RepositoryRoot = Path.Combine(RootDirectory, "repo");
        BinDirectory = Path.Combine(RootDirectory, "bin");
        HomeDirectory = Path.Combine(RootDirectory, "home");
        GitLogPath = Path.Combine(RootDirectory, "fake-git.log");
        PoetryLogPath = Path.Combine(RootDirectory, "fake-poetry.log");
        CurlLogPath = Path.Combine(RootDirectory, "fake-curl.log");
        PwshLogPath = Path.Combine(RootDirectory, "fake-pwsh.log");

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
            ["FAKE_POETRY_LOG"] = PoetryLogPath,
            ["FAKE_POETRY_VERSION"] = "2.2.1",
            ["FAKE_CURL_LOG"] = CurlLogPath,
            ["FAKE_PWSH_LOG"] = PwshLogPath,
            ["FAKE_PWSH_VERSION"] = "7.4.13",
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

        CreateFakeTooling();
        CreateBashEnvironmentFile();
        CreatePlaceholderPoshQcModule();
    }

    public string RootDirectory { get; }

    public string RepositoryRoot { get; }

    public string BinDirectory { get; }

    public string HomeDirectory { get; }

    public string GitLogPath { get; }

    public string PoetryLogPath { get; }

    public string CurlLogPath { get; }

    public string PwshLogPath { get; }

    public void WriteFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(
            RepositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, contents.ReplaceLineEndings("\n"));
    }

    public IReadOnlyList<string> ReadGitLog() =>
        File.Exists(GitLogPath) ? File.ReadAllLines(GitLogPath) : Array.Empty<string>();

    public IReadOnlyList<string> ReadPoetryLog() =>
        File.Exists(PoetryLogPath) ? File.ReadAllLines(PoetryLogPath) : Array.Empty<string>();

    public IReadOnlyList<string> ReadCurlLog() =>
        File.Exists(CurlLogPath) ? File.ReadAllLines(CurlLogPath) : Array.Empty<string>();

    public IReadOnlyList<string> ReadPwshLog() =>
        File.Exists(PwshLogPath) ? File.ReadAllLines(PwshLogPath) : Array.Empty<string>();

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
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("--noprofile");
        process.StartInfo.ArgumentList.Add("--norc");
        process.StartInfo.ArgumentList.Add(GetPathForShell(ScriptPath));
        process.StartInfo.Environment["PATH"] = BuildProcessPath();
        process.StartInfo.Environment["HOME"] = HomeDirectory;
        process.StartInfo.Environment["BASH_ENV"] = GetPathForShell(BashEnvPath);

        foreach (var entry in _environment)
        {
            process.StartInfo.Environment[entry.Key] = ConvertEnvironmentValue(
                entry.Key,
                entry.Value
            );
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

    private static string ScriptPath =>
        FindRepositoryRoot() is { } repositoryRoot
            ? Path.Combine(repositoryRoot, ".codex", "codex-web-setup.sh")
            : throw new DirectoryNotFoundException(
                "Could not locate repository root for codex-web-setup.sh tests."
            );

    private void CreateFakeTooling()
    {
        WriteExecutable(
            "git",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_GIT_LOG}"

case "${1:-}" in
  rev-parse)
    if [[ "${2:-}" == "--show-toplevel" ]]; then
      printf '%s\n' "${FAKE_GIT_ROOT}"
      exit 0
    fi
    ;;
esac

printf 'unexpected git invocation: %s\n' "$*" >&2
exit 99
"""
        );
        WriteExecutable(
            "poetry",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_POETRY_LOG}"

case "${1:-}" in
  --version)
    printf 'Poetry (version %s)\n' "${FAKE_POETRY_VERSION:-2.2.1}"
    ;;
esac
"""
        );
        WriteExecutable(
            "curl",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_CURL_LOG}"
exit 0
"""
        );
        WriteExecutable(
            "pwsh",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >> "${FAKE_PWSH_LOG}"

if [[ "${1:-}" == "--version" ]]; then
  printf 'PowerShell %s\n' "${FAKE_PWSH_VERSION:-7.4.13}"
fi
"""
        );
        WriteExecutable(
            "dpkg",
            """
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "${1:-}" == "--compare-versions" ]]; then
    left="${2:-}"
    operator="${3:-}"
    right="${4:-}"

    if [[ "$operator" == "ge" ]] && [[ "$(printf '%s\n%s\n' "$left" "$right" | sort -V | tail -n 1)" == "$left" ]]; then
        exit 0
    fi

    exit 1
fi

exit 0
"""
        );
        WriteExecutable(
            "actionlint",
            """
#!/usr/bin/env bash
set -Eeuo pipefail
exit 0
"""
        );
    }

    private void CreatePlaceholderPoshQcModule()
    {
        WriteFile(
            "scripts/powershell/PoshQC/PoshQC.psd1",
            """
@{
    RootModule = 'PoshQC.psm1'
    ModuleVersion = '0.0.1'
}
"""
        );
        WriteFile("scripts/powershell/PoshQC/PoshQC.psm1", string.Empty);
    }

    private void WriteExecutable(string name, string contents)
    {
        var fullPath = Path.Combine(BinDirectory, name);
        File.WriteAllText(fullPath, contents.ReplaceLineEndings("\n"));
        MakeExecutable(fullPath);
    }

    private void CreateBashEnvironmentFile()
    {
        var fakeGitPath = GetPathForShell(FakeGitScriptPath).Replace("'", "'\"'\"'");
        var content =
            $"git(){Environment.NewLine}{{{Environment.NewLine}  bash '{fakeGitPath}' \"$@\"{Environment.NewLine}}}{Environment.NewLine}";

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
            return string.Join(Path.PathSeparator, BinDirectory, "/usr/bin", "/bin");
        }

        return string.Join(
            Path.PathSeparator,
            BinDirectory,
            @"C:\Program Files\Git\bin",
            @"C:\Program Files\Git\usr\bin"
        );
    }

    private string ConvertEnvironmentValue(string key, string? value)
    {
        if (string.IsNullOrEmpty(value) || !OperatingSystem.IsWindows())
        {
            return value ?? string.Empty;
        }

        return key switch
        {
            "FAKE_GIT_ROOT"
            or "FAKE_GIT_LOG"
            or "FAKE_POETRY_LOG"
            or "FAKE_CURL_LOG"
            or "FAKE_PWSH_LOG" => GetPathForShell(value),
            _ => value,
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
            @"C:\Program Files\Git\usr\bin\bash.exe",
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
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute
            );
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = BashExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("-lc");
        process.StartInfo.ArgumentList.Add($"chmod +x '{EscapeForBash(GetPathForShell(path))}'");
        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to mark '{path}' executable. Output: {process.StandardOutput.ReadToEnd()}{process.StandardError.ReadToEnd()}"
            );
        }
    }

    private static string EscapeForBash(string value) => value.Replace("'", "'\"'\"'");

    private string GetPathForShell(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        var root =
            Path.GetPathRoot(fullPath)
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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

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
