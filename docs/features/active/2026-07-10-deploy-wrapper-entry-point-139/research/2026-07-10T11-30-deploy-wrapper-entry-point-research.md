
# Research: `scripts/Deploy.ps1` wrapper entry point + `Publish.ps1` output-leakage fix

- **Issue:** #139
- **Date:** 2026-07-10
- **Scope:** `scripts/Publish.ps1` (bug fix, output suppression only), new `scripts/Deploy.ps1`, plus tests for both.

## 1. Current State Analysis

### 1.1 `Publish.ps1` output-leakage defect — confirmed

`scripts/Publish.ps1` (repo root: `C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\agent-a8a9a59589481ca11`) runs a single top-level `if ($MyInvocation.InvocationName -ne '.')` main block (lines 106–249) and ends with `return $BundleRoot` (line 248). Three call sites inside that block invoke helper functions without suppressing their return values:

| Line | Call | Return type / value | Defining module |
|---|---|---|---|
| 221 | `Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version` | `[string]` — `$destManifest` (staged `AppxManifest.xml` path), `Publish.Msix.psm1` line 119 | `scripts/Publish.Msix.psm1` |
| 227 | `Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath` | `[string]` — `$OutputMsixPath` (the `.msix` path); function is decorated `[OutputType([string])]`, `Publish.Msix.psm1` line 230 | `scripts/Publish.Msix.psm1` |
| 245 | `Write-PublishManifest -BundleRoot $BundleRoot -Version $Version` | `[string]` — `$manifestPath` (`manifest.json` path), `Publish.Helpers.psm1` line 246 | `scripts/Publish.Helpers.psm1` |

None of these three calls assigns to `$null` or pipes to `Out-Null`, so each return value is emitted to the success-output stream. Because the script has no other output filtering, a captured invocation (`$result = & .\scripts\Publish.ps1 ...`) currently yields `$result` as a 4-element array: `AppxManifest.xml` path, `.msix` path, `manifest.json` path, then the bundle root (`return $BundleRoot`) — confirming the defect description in `issue.md`/`spec.md` exactly.

Other stage-4/stage-6 calls in `Publish.ps1` do **not** leak: `Invoke-LayoutAssembly` (line 218, no `return` in `Publish.Msix.psm1`), `Invoke-MakePri` (line 224, no `return`), `Invoke-SignTool` (line 231, no `return`), `Copy-InstallScriptsIntoBundle` (line 240, `Publish.Helpers.psm1` line 160–200, no `return`) all end without a `return` statement and their internal `Copy-Item`/`New-Item` calls are already captured with `$null =` or lack `-PassThru`, so they emit nothing. `Invoke-DotnetPublish` (Stage 2) and `Copy-DockerArtifact` (Stage 3) are likewise clean. The defect is isolated to exactly the three calls above.

**Fix approach — confirmed as correct and minimal.** Assign each of the three offending call results to `$null` at the `Publish.ps1` call site:

```powershell
$null = Invoke-VersionStamp -ManifestSourcePath $ManifestSource -StagingDir $StagingDir -Version $Version
...
$null = Invoke-MakeAppx -StagingDir $StagingDir -OutputMsixPath $MsixPath
...
$null = Write-PublishManifest -BundleRoot $BundleRoot -Version $Version
```

This changes only `scripts/Publish.ps1`. It does **not** touch `Publish.Msix.psm1` or `Publish.Helpers.psm1` — the three helper functions keep returning their paths unchanged, which matters because:
- `Publish.Tests.ps1`'s mock for `Invoke-MakeAppx` (line 101–105) itself returns `$OutputMsixPath` and other tests assert on `Args` captured from mock closures, not on the real function's return value, so mock behavior is unaffected by this fix.
- The spec's out-of-scope note ("Out of scope: any change to ... `Publish.Helpers.psm1` return behavior") and the issue text ("helper return behavior is unchanged") both confirm the fix must be call-site-only. `Invoke-VersionStamp`'s and `Invoke-MakeAppx`'s return values live in `Publish.Msix.psm1` (a separate module from `Publish.Helpers.psm1`) — both modules' returns are otherwise consumed by their own dedicated Pester suites (`Publish.Msix.Tests.ps1`, `Publish.Helpers.Tests.ps1`), which call the functions directly and assert on the returned string, so nothing in this fix can touch those return statements without breaking those suites.

### 1.2 `Install.ps1` parameter surface — confirmed

`scripts/Install.ps1` param block (lines 76–89):

```powershell
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourcePath = $PSScriptRoot,
    [switch]$AllowUnsigned,
    [switch]$SkipDocker,
    [string]$DockerEnvFilePath,
    [string]$AnthropicEnvFilePath,
    [switch]$Force
)
```

- `-SourcePath` defaults to `$PSScriptRoot` — the directory the running `Install.ps1` file lives in. This is exactly why `Deploy.ps1` must invoke the **staged copy** inside the bundle (`<bundleRoot>\Install.ps1`) rather than `scripts\Install.ps1`: the staged copy's `$PSScriptRoot` resolves to the bundle root, giving correct self-location without needing `-SourcePath` forwarding at all.
- `-AllowUnsigned` triggers the Stage 0 administrator precheck (lines 279–285): if `$AllowUnsigned` is set and `Test-IsElevatedAdmin` returns `$false`, the script throws before any filesystem side effect. This confirms the admin precheck is real and unconditional whenever `-AllowUnsigned` is passed — `Deploy.ps1` inherits this precheck by forwarding `-AllowUnsigned`; it does not need to duplicate the check.
- `-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force` behave exactly as documented in the script's comment-based help (lines 40–59) and match the spec's forwarding list.

### 1.3 `Deploy.ps1` design — confirmed against spec

Per `spec.md`/`user-story.md`/`issue.md` (all consistent, no re-scoping needed):

- Runs `scripts/Publish.ps1`, captures the returned bundle root (now a single scalar after the 1.1 fix).
- Invokes `<bundleRoot>\Install.ps1` — the staged copy inside the bundle produced by `Copy-InstallScriptsIntoBundle`, **not** `scripts/Install.ps1`.
- Must not change the caller's working directory (no `Set-Location`/`Push-Location` into the bundle dir; invoke via full path `& (Join-Path $bundleRoot 'Install.ps1') @installParams`).
- Publish-side pass-through: `-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`.
- Install-side pass-through: `-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`.
- `-SkipSign` on `Deploy.ps1` maps to `-AllowUnsigned` on the invoked `Install.ps1` (an unsigned MSIX bundle requires `-AllowUnsigned` at install time; this mirrors `Publish.ps1 -SkipSign` producing an unsigned `.msix`).
- `[CmdletBinding(SupportsShouldProcess = $true)]`; `-WhatIf` propagates to both child invocations (`Publish.ps1 -WhatIf`, then `Install.ps1 -WhatIf` if publish is not itself gated by `ShouldProcess` under `-WhatIf`, note below).
- Fail-fast: if `Publish.ps1` throws, or returns a null/empty bundle root, `Deploy.ps1` must not attempt install.
- Returns the bundle root on success (same contract as `Publish.ps1` itself, so `Deploy.ps1` is drop-in composable with any caller that already expects a scalar bundle-root string).

**`-WhatIf` propagation note.** `Publish.ps1`'s own main block is not itself wrapped in a `ShouldProcess` check (only individual internal stages like directory removal/creation use `$PSCmdlet.ShouldProcess`), so `Publish.ps1 -WhatIf` still executes and returns a bundle root under `-WhatIf` (its state-changing sub-steps individually no-op). `Deploy.ps1` should forward `-WhatIf` to the `Publish.ps1` call directly (PowerShell auto-propagates common parameters when splatting `@PSBoundParameters`-derived hashtables that include `WhatIf`), then gate its own `Install.ps1` invocation behind `$PSCmdlet.ShouldProcess($bundleRoot, 'Install bundle')` so the install stage is skippable under `-WhatIf` even though publish's own internal stages run in reduced (no-op) form.

### 1.4 Design-seam recommendation (per `.claude/rules/powershell.md` — Design Seams)

The repo's Design Seams policy specifies, in order: (1) wrapper-function seam (preferred), (2) injectable delegate/ScriptBlock seam, (3) adapter seams for non-executable boundaries. `Deploy.ps1` is calling other **PowerShell scripts** (not external executables), so the closest-fit seam is a wrapper function per child script, mirroring the `Invoke-<Tool>Exe` pattern used elsewhere in the repo for external tools (for example `Invoke-DotnetExe` in `Publish.Helpers.psm1`, `Invoke-GitExe` cited in the rule file).

Recommended wrapper names and shapes:

```powershell
function Invoke-PublishScript {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishScriptPath,

        [Parameter(Mandatory = $true)]
        [hashtable]$PublishParams
    )
    if ($PSCmdlet.ShouldProcess($PublishScriptPath, 'Invoke Publish.ps1')) {
        return & $PublishScriptPath @PublishParams
    }
}

function Invoke-InstallScript {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallScriptPath,

        [Parameter(Mandatory = $true)]
        [hashtable]$InstallParams
    )
    if ($PSCmdlet.ShouldProcess($InstallScriptPath, 'Invoke Install.ps1')) {
        & $InstallScriptPath @InstallParams
    }
}
```

- Both take a resolved script path plus a `[hashtable]` of named parameters to splat (`@PublishParams` / `@InstallParams`), not a positional `[string[]]` — because these are PowerShell scripts with named parameters (`-Version`, `-SkipDocker`, etc.), not an external CLI taking a flat argument array. This differs from the `Invoke-<Tool>Exe -<Tool>Args <string[]>` shape (that shape is for external executables per rule 1; these two wrappers call PowerShell scripts, so a hashtable-splat parameter is the natural named-parameter equivalent and keeps mock assertions on individual named values rather than a positional string array).
- Neither parameter is named `Args` (rule: avoid the automatic-variable collision) — `PublishParams`/`InstallParams` are used instead.
- `Deploy.ps1` calls `Invoke-PublishScript` and captures its return as the bundle root; it calls `Invoke-InstallScript` for the staged bundle's `Install.ps1` and does not need its return value (the install script's own success path or a thrown error is the contract).

**Mocking rule application.** Tests for `Deploy.ps1` will `Mock Invoke-PublishScript { ... return '<fake-bundle-root>' }` and `Mock Invoke-InstallScript { ... }`, matching the named-parameter signatures exactly (`PublishScriptPath`, `PublishParams`, `InstallScriptPath`, `InstallParams`), and must register these mocks in `BeforeEach` before `& $script:DeployScriptPath ...` is invoked in each `It` — this is the same ordering already used by `Publish.Tests.ps1` (mocks registered in `BeforeEach`, script invoked via `&` inside each `It`).

Where the wrapper functions themselves should live: a new `Deploy.Helpers.psm1` sibling module (parallel to `Publish.Helpers.psm1`/`Install.Helpers.psm1`) is the pattern-consistent choice, keeping `Deploy.ps1` itself thin and under the 500-line cap, and giving the wrapper functions `Export-ModuleMember` visibility for `Mock` to intercept at the test-file scope (matching how `Publish.Tests.ps1` mocks `Publish.Helpers.psm1`/`Publish.Msix.psm1` exports without `-ModuleName`).

## 2. Candidate Approaches

### Approach A (recommended): Wrapper-function seam, hashtable-splat, dedicated `Deploy.Helpers.psm1`

- **Description:** As designed in section 1.4 — two thin wrapper functions in a new helper module; `Deploy.ps1` builds two parameter hashtables from its own bound parameters and splats them through the wrappers.
- **Advantages:** Matches the repo's stated Design Seams policy (wrapper-function seam is the first-listed, preferred option); keeps `Deploy.ps1` free of any direct `&`/`Invoke-Expression` call to a script path, so every child invocation is mockable without touching the filesystem or real scripts; consistent with the existing `Invoke-DotnetPublish`/`Invoke-DotnetExe` precedent in `Publish.Helpers.psm1`; keeps parameter-forwarding logic (the `-SkipSign` → `-AllowUnsigned` mapping) as plain hashtable construction in `Deploy.ps1`, which is easy to unit-test by asserting on the mock-captured hashtable contents.
- **Limitations:** Introduces one new module file (acceptable — the spec's scope estimate already counts `Deploy.ps1` as a new production file; a helper module supporting it is consistent with how `Publish.ps1`/`Install.ps1` each already have companion helper modules).
- **Alignment:** High — directly follows `.claude/rules/powershell.md` Design Seams and Mocking Rules sections, and follows the existing repo convention of orchestrator script + helper module pairs.

### Approach B (rejected): Direct `&` invocation of child scripts inside `Deploy.ps1`, mocked via `Mock` on `Get-Command`/script-path indirection or via injectable `[scriptblock]` parameters

- **Description:** `Deploy.ps1` calls `& $publishScriptPath @params` and `& $installScriptPath @params` directly, with test-time substitution achieved either by parameterizing the script paths (so tests point at fake scripts) or by injecting `[scriptblock]` delegates for the two invocations.
- **Limitations:** Pester cannot `Mock` a direct `&`-call to an external `.ps1` file path the way it mocks a function; the alternative (parameterizing script paths to point tests at fake/stub `.ps1` files) reintroduces a form of temp/fixture files that the repo's testing policy discourages ("Creation and use of temporary files in tests is strictly prohibited") and is more brittle than a function mock. The injectable-`[scriptblock]`-parameter option is the repo's second-choice seam (used "only when a wrapper is insufficient") — a wrapper function is not insufficient here, so this approach does not meet the ordering rule in Design Seams.
- **Rejected alternative — kept brief per instructions.**

**Recommendation:** Approach A (wrapper-function seam via `Deploy.Helpers.psm1`).

## 3. Behavior Semantics

- **Success:** `Invoke-PublishScript` returns a non-empty bundle-root string; `Deploy.ps1` then calls `Invoke-InstallScript` against `<bundleRoot>\Install.ps1`; on install success, `Deploy.ps1` returns the bundle root (mirroring `Publish.ps1`'s own return contract, so callers of `Deploy.ps1` and callers of `Publish.ps1` alone see the same shape).
- **Failure — publish throws:** any exception from `Invoke-PublishScript` propagates uncaught (fail fast, per repo error-handling policy: "raise or return clear, specific errors ... do not silently ignore"); `Invoke-InstallScript` must not be called. No `try`/`catch`-and-swallow around the publish call.
- **Failure — publish returns empty/null bundle root:** `Deploy.ps1` must explicitly `throw` a clear message (something like "Publish.ps1 did not return a bundle root; cannot proceed to install") rather than silently invoking `Install.ps1` with an empty/invalid path — this is an explicit guard, not an assumption that an exception would naturally occur, because `& $emptyPath` would produce a less diagnostic error.
- **Ordering:** publish must complete (or be `-WhatIf`-simulated) before install is attempted; no other ordering ambiguity exists since there are only two children.
- **`-WhatIf`:** propagates to `Invoke-PublishScript`'s inner `$PSCmdlet.ShouldProcess` gate and to `Invoke-InstallScript`'s inner gate; under `-WhatIf`, `Deploy.ps1` should still report what it would do (via `Write-Information` matching the existing `[publish]`/`[install]`-prefixed logging convention in `Publish.ps1`/`Install.ps1`) without performing real side effects.
- **`-SkipSign` → `-AllowUnsigned` mapping:** this is a one-way, conditional translation — `Deploy.ps1` does not expose its own `-AllowUnsigned` parameter; when `-SkipSign` is set, the install-side parameter hashtable must include `AllowUnsigned = $true` (a switch value), otherwise the install-side hashtable must omit it (default `$false`/absent), preserving `Install.ps1`'s own default behavior when signing was not skipped.
- **Working directory invariant:** `Deploy.ps1` must never call `Set-Location`/`Push-Location` toward the bundle directory; both child invocations use fully-resolved absolute paths (`$bundleRoot` from the publish return value, joined with `Install.ps1`), so the caller's `$PWD` is unaffected regardless of success or failure.

## 4. Requirements Mapping

| Acceptance criterion (issue.md) | Design element |
|---|---|
| `Publish.ps1` emits exactly one pipeline object | `$null =` at the three call sites in `Publish.ps1` (Stage 4/6), section 1.1 |
| `Deploy.ps1` runs publish, captures bundle root, invokes staged `Install.ps1`, no CWD change | `Invoke-PublishScript` return capture + absolute-path `Invoke-InstallScript` call, section 1.3/1.4 |
| Forwards publish params (`-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`) and install params (`-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`); `-SkipSign` → `-AllowUnsigned` | Two hashtables built from `Deploy.ps1`'s own bound parameters, splatted through the wrappers, section 1.4/3 |
| `CmdletBinding(SupportsShouldProcess = $true)`, `-WhatIf` to both children | `ShouldProcess` gates inside both wrapper functions, section 1.3/3 |
| Fail fast: no install on publish throw/empty return | Uncaught propagation + explicit empty-bundle-root guard, section 3 |
| Returns bundle root on success | `Deploy.ps1` final `return $bundleRoot` |

**State model:** `Deploy.ps1` is a linear two-stage sequence with no persisted state of its own (no new `.env`/manifest keys) — state ownership stays with `Publish.ps1` (`.env`, bundle root) and `Install.ps1` (install record at `%LOCALAPPDATA%\OpenClaw\install-record.json`). No new file changes are required beyond: `scripts/Publish.ps1` (3-line fix), new `scripts/Deploy.ps1`, new `scripts/Deploy.Helpers.psm1`.

## 5. Existing Test Patterns

`tests/scripts/Publish.Tests.ps1`:
- Imports `Publish.Helpers.psm1` and `Publish.Env.psm1` via `Import-Module ... -Force` in `BeforeAll` (not dot-sourcing `Publish.ps1` itself).
- `BeforeEach` registers `Mock` for every module-exported helper function (`Invoke-DotnetPublish`, `Copy-DockerArtifact`, `Invoke-VersionStamp`, `Invoke-LayoutAssembly`, `Invoke-MakePri`, `Invoke-MakeAppx`, `Invoke-SignTool`, `Copy-InstallScriptsIntoBundle`, `Write-PublishManifest`, `Resolve-CertThumbprint`) plus `New-Item`/`Remove-Item`/`Test-Path` shims, all appending to a `$global:PublishTestCalls` `ArrayList` — used because Pester `Mock` script blocks execute in the caller's (here, the orchestrator script's) scope, and `$script:` scope differs between the orchestrator and the test file, so `$global:` is the only reliable cross-scope call-log store (documented in the file's own header comment).
- Each `It` invokes the orchestrator via `& $script:ScriptPath -Version ... [-SkipSign|-CertThumbprint ...]`, most piped to `Out-Null` (existing tests already suppress pipeline output, which is why the leakage defect was not caught by the existing suite — no existing test asserts on the *shape* of the captured output, only on side-effect call logs).
- The script's main-block guard is `if ($MyInvocation.InvocationName -ne '.')`; invoking via `&` (the call operator) makes `$MyInvocation.InvocationName` equal the script path, not `'.'`, so the guarded main block executes. A small number of other test files (`Install.Preflight.Tests.ps1` line 40, `Install.HostAdapterStart.Tests.ps1`) instead dot-source (`. $script:ScriptPath ...`) specifically to load top-level helper functions defined *outside* the main guard without running the main block, relying on `$MyInvocation.InvocationName -eq '.'` in that mode.
- **Where to add the new regression test:** in `tests/scripts/Publish.Tests.ps1`, inside (or alongside) the existing `Context 'stage ordering'` block — add an `It` that invokes `& $script:ScriptPath -Version '1.2.3.0' -SkipSign` **without** piping to `Out-Null`, captures the result into a variable (e.g. `$result = & $script:ScriptPath ...`), and asserts `@($result).Count -eq 1` and `$result -eq $script:BundleRoot` (the expected bundle-root path for that test's fixture). This directly exercises the fix from section 1.1 and would have failed before the fix (returning a 4-element array) and pass after.

`tests/scripts/Install.Tests.ps1` (representative `Install.*.Tests.ps1`):
- Same overall pattern: imports `Install.Helpers.psm1` and `Install.Preflight.psm1` via `Import-Module -Force` in `BeforeAll`; uses `$global:InstallTestCalls` for the same cross-scope reason; mocks every helper (`Get-ManifestVersion`, `Test-ManifestIntegrity`, `Test-DockerAvailable`, `Copy-BundleContents`, `Initialize-DotEnv`, `Invoke-MsixInstall`, `Invoke-MsixCapture`, `Invoke-MsixAppActivate`, `Invoke-MsixRemove`, `Invoke-ComposeUp`, `Wait-ComposeHealthy`, `Invoke-ComposeDown`, `Write-InstallRecord`, `Read-InstallRecord`) in `BeforeEach`, overrides `$env:LOCALAPPDATA` for path stability, and defines `function global:Invoke-HostAdapterStart { ... }` to override a function that is conditionally self-defined inside `Install.ps1` itself (the `if (-not (Get-Command -Name ... -ErrorAction SilentlyContinue))` guard pattern at the top of `Install.ps1`, used for functions like `Test-IsElevatedAdmin`/`Invoke-HostAdapterStart` that are awkward to `Mock` normally because they wrap .NET static calls or process starts).
- `Install.Preflight.Tests.ps1` line 40 is the one file that dot-sources `Install.ps1` (`. $script:ScriptPath -SourcePath 'C:\dot-source-noop' -ErrorAction SilentlyContinue 2>&1 | Out-Null`) purely to bring the outer, always-defined functions into test scope without executing Stage 0–10, confirming the dot-source/guard relationship documented above.

**New test files for this feature** should follow this established pattern precisely: a new `tests/scripts/Deploy.Tests.ps1` that imports `Deploy.Helpers.psm1` via `Import-Module -Force`, mocks `Invoke-PublishScript`/`Invoke-InstallScript` (and any other exported helper) in `BeforeEach` with a `$global:DeployTestCalls` call log, then invokes `& $script:DeployScriptPath ...` per `It` to assert parameter forwarding, the `-SkipSign`→`-AllowUnsigned` mapping, publish-failure short-circuit, `-WhatIf` propagation, and the returned bundle root — matching the seeded test conditions already listed in `spec.md`/`issue.md`.

## Rejected Alternatives

- Direct `&`-invocation of child `.ps1` files inside `Deploy.ps1` without a wrapper-function seam (Approach B) — rejected because Pester cannot reliably mock a raw script-path invocation without either fixture scripts (prohibited: no temp files) or an injectable-delegate seam, which the repo's Design Seams ordering treats as a fallback, not the default, when a wrapper function is sufficient (it is, here).

## Automation Feasibility

This change is entirely local PowerShell + Pester work: a 3-line suppression fix in `scripts/Publish.ps1`, one new `scripts/Deploy.ps1` orchestrator, one new `scripts/Deploy.Helpers.psm1` wrapper module, and corresponding Pester test files. There is no third-party UI, web portal, external API onboarding, or other manual step involved at any point — every step (toolchain format/lint/test loop, mock registration, assertion writing) is automatable with no human interaction required.
