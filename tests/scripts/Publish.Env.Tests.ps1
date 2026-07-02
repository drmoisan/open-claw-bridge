#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Publish.Env.psm1.

.DESCRIPTION
    Drives the pure .env helpers (Get-EnvFileMap, Set-EnvFileValue,
    Step-PackageVersion) with in-memory string[] content only. No temporary
    files are created and no disk is read or written. The file-I/O seam
    (Read-EnvFileContent / Write-EnvFileContent) is exercised via mocks so the
    pure-helper tests stay free of disk access (repo no-temp-files rule).
#>

Describe 'Publish.Env.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Env.psm1'
        Import-Module $script:ModulePath -Force
    }

    Context 'Module exports' {
        It 'exports the expected 5 helper functions' {
            $expected = @(
                'Get-EnvFileMap', 'Set-EnvFileValue', 'Step-PackageVersion',
                'Read-EnvFileContent', 'Write-EnvFileContent'
            ) | Sort-Object
            $actual = (Get-Command -Module Publish.Env).Name | Sort-Object
            ($actual -join ',') | Should -Be ($expected -join ',')
        }
    }

    Context 'Get-EnvFileMap' {
        It 'parses KEY=VALUE pairs ignoring blanks and comments' {
            $content = @(
                '# a comment',
                '',
                'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                '   # indented comment',
                'OPENCLAW_CERT_THUMBPRINT=ABC123'
            )
            $map = Get-EnvFileMap -Content $content
            $map['OPENCLAW_PACKAGE_VERSION'] | Should -Be '1.0.2.0'
            $map['OPENCLAW_CERT_THUMBPRINT'] | Should -Be 'ABC123'
            $map.Keys.Count | Should -Be 2
        }
        It 'preserves the value verbatim including = signs after the first' {
            $map = Get-EnvFileMap -Content @('KEY=a=b=c')
            $map['KEY'] | Should -Be 'a=b=c'
        }
        It 'keeps the first value on a duplicate key (first-wins)' {
            $map = Get-EnvFileMap -Content @('K=first', 'K=second')
            $map['K'] | Should -Be 'first'
        }
        It 'ignores lines without an = and lines with an empty key' {
            $map = Get-EnvFileMap -Content @('justtext', '=novalue', 'GOOD=1')
            $map.Contains('justtext') | Should -BeFalse
            $map.Keys.Count | Should -Be 1
            $map['GOOD'] | Should -Be '1'
        }
        It 'returns an empty map for empty content' {
            $map = Get-EnvFileMap -Content @()
            $map.Keys.Count | Should -Be 0
        }
        It 'trims whitespace around the key' {
            $map = Get-EnvFileMap -Content @('  SPACED  =value')
            $map['SPACED'] | Should -Be 'value'
        }
    }

    Context 'Set-EnvFileValue' {
        It 'updates a present key in place preserving order, comments, and unrelated keys' {
            $content = @(
                '# header comment',
                'FIRST=1',
                'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                '# trailing comment',
                'LAST=z'
            )
            $result = Set-EnvFileValue -Content $content -Key 'OPENCLAW_PACKAGE_VERSION' -Value '1.0.2.1'
            $result[0] | Should -Be '# header comment'
            $result[1] | Should -Be 'FIRST=1'
            $result[2] | Should -Be 'OPENCLAW_PACKAGE_VERSION=1.0.2.1'
            $result[3] | Should -Be '# trailing comment'
            $result[4] | Should -Be 'LAST=z'
            $result.Count | Should -Be 5
        }
        It 'appends the key when absent' {
            $content = @('EXISTING=1')
            $result = Set-EnvFileValue -Content $content -Key 'OPENCLAW_CERT_THUMBPRINT' -Value 'DEAD00'
            $result.Count | Should -Be 2
            $result[0] | Should -Be 'EXISTING=1'
            $result[1] | Should -Be 'OPENCLAW_CERT_THUMBPRINT=DEAD00'
        }
        It 'appends to empty content' {
            $result = Set-EnvFileValue -Content @() -Key 'K' -Value 'v'
            $result.Count | Should -Be 1
            $result[0] | Should -Be 'K=v'
        }
        It 'is idempotent: re-applying the same value does not duplicate the key' {
            $content = @('OPENCLAW_PACKAGE_VERSION=1.0.2.1', 'OTHER=x')
            $first = Set-EnvFileValue -Content $content -Key 'OPENCLAW_PACKAGE_VERSION' -Value '1.0.2.1'
            $second = Set-EnvFileValue -Content $first -Key 'OPENCLAW_PACKAGE_VERSION' -Value '1.0.2.1'
            ($second -join "`n") | Should -Be ($first -join "`n")
            (@($second | Where-Object { $_ -like 'OPENCLAW_PACKAGE_VERSION=*' })).Count | Should -Be 1
        }
        It 'does not treat a commented key line as the key' {
            $content = @('# OPENCLAW_PACKAGE_VERSION=note', 'OPENCLAW_PACKAGE_VERSION=1.0.0.0')
            $result = Set-EnvFileValue -Content $content -Key 'OPENCLAW_PACKAGE_VERSION' -Value '2.0.0.0'
            $result[0] | Should -Be '# OPENCLAW_PACKAGE_VERSION=note'
            $result[1] | Should -Be 'OPENCLAW_PACKAGE_VERSION=2.0.0.0'
        }
        It 'updates only the first occurrence of a duplicate key' {
            $content = @('K=a', 'K=b')
            $result = Set-EnvFileValue -Content $content -Key 'K' -Value 'c'
            $result[0] | Should -Be 'K=c'
            $result[1] | Should -Be 'K=b'
        }
        It 'accepts an empty value' {
            $result = Set-EnvFileValue -Content @('K=old') -Key 'K' -Value ''
            $result[0] | Should -Be 'K='
        }
    }

    Context 'Step-PackageVersion' {
        It 'increments the revision (4th) segment by one' {
            Step-PackageVersion -Version '1.0.2.0' | Should -Be '1.0.2.1'
        }
        It 'increments a non-zero revision' {
            Step-PackageVersion -Version '1.0.2.9' | Should -Be '1.0.2.10'
        }
        It 'leaves the first three segments unchanged' {
            Step-PackageVersion -Version '12.34.56.78' | Should -Be '12.34.56.79'
        }
        It 'throws on a 3-part version' {
            { Step-PackageVersion -Version '1.2.3' } |
                Should -Throw -ExpectedMessage '*not a valid 4-part version*'
        }
        It 'throws on a non-numeric segment' {
            { Step-PackageVersion -Version '1.2.3.x' } |
                Should -Throw -ExpectedMessage '*not a valid 4-part version*'
        }
        It 'throws on empty input' {
            { Step-PackageVersion -Version '' } |
                Should -Throw -ExpectedMessage '*not a valid 4-part version*'
        }
    }

    Context 'Read-EnvFileContent (file seam)' {
        It 'returns an empty array when the file does not exist' {
            Mock -ModuleName Publish.Env Test-Path { $false }
            $r = Read-EnvFileContent -Path 'C:\fake\.env'
            @($r).Count | Should -Be 0
        }
        It 'returns the file lines as a string array when present' {
            Mock -ModuleName Publish.Env Test-Path { $true }
            Mock -ModuleName Publish.Env Get-Content { @('A=1', 'B=2') }
            $r = Read-EnvFileContent -Path 'C:\fake\.env'
            @($r).Count | Should -Be 2
            $r[0] | Should -Be 'A=1'
        }
    }

    Context 'Write-EnvFileContent (file seam)' {
        It 'writes content via Set-Content on the success path' {
            $script:WrittenPath = $null
            $script:WrittenValue = $null
            Mock -ModuleName Publish.Env Set-Content {
                $script:WrittenPath = $LiteralPath
                $script:WrittenValue = $Value
            }
            Write-EnvFileContent -Path 'C:\fake\.env' -Content @('A=1')
            Assert-MockCalled -ModuleName Publish.Env Set-Content -Times 1 -Scope It
            $script:WrittenPath | Should -Be 'C:\fake\.env'
        }
        It '-WhatIf does not write' {
            Mock -ModuleName Publish.Env Set-Content { }
            Write-EnvFileContent -Path 'C:\fake\.env' -Content @('A=1') -WhatIf
            Assert-MockCalled -ModuleName Publish.Env Set-Content -Times 0 -Scope It
        }
    }
}
