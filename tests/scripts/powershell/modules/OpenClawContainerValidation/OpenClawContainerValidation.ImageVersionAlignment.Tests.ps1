BeforeDiscovery {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\..\scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'OpenClawContainerValidation.psm1 (image version alignment)' {
    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..\..\..\..\..\scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
        Import-Module -Name $modulePath -Force -ErrorAction Stop
    }

    Context 'ConvertFrom-OpenClawImageReference' {
        It 'splits a repo:tag reference on the last colon' {
            # Arrange / Act
            $result = ConvertFrom-OpenClawImageReference -ImageReference 'openclaw/core:1.2.3.0'

            # Assert
            $result.Repository | Should -Be 'openclaw/core'
            $result.Tag | Should -Be '1.2.3.0'
        }

        It 'returns an empty Tag (not a throw) when no colon is present' {
            # Arrange / Act
            $result = ConvertFrom-OpenClawImageReference -ImageReference 'openclaw/core'

            # Assert
            $result.Repository | Should -Be 'openclaw/core'
            $result.Tag | Should -Be ''
        }

        It 'splits only on the last colon when the repository segment contains a slash' {
            # Arrange / Act
            $result = ConvertFrom-OpenClawImageReference -ImageReference 'openclaw/agent:1.2.3.0'

            # Assert
            $result.Repository | Should -Be 'openclaw/agent'
            $result.Tag | Should -Be '1.2.3.0'
        }
    }

    Context 'Test-OpenClawImageVersionAligned - matched and single-mismatch' {
        It 'reports IsExpected true when both tags equal the expected version' {
            # Arrange / Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference 'openclaw/core:1.2.3.0' -AgentImageReference 'openclaw/agent:1.2.3.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeTrue
            $result.Category | Should -Be 'Container'
            $result.Name | Should -Be 'ImageVersionAlignment'
        }

        It 'reports IsExpected false and names both references/tags when only the agent tag mismatches' {
            # Arrange / Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference 'openclaw/core:1.2.3.0' -AgentImageReference 'openclaw/agent:1.2.4.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeFalse
            $result.Details.coreImageReference | Should -Be 'openclaw/core:1.2.3.0'
            $result.Details.agentImageReference | Should -Be 'openclaw/agent:1.2.4.0'
            $result.Details.coreTag | Should -Be '1.2.3.0'
            $result.Details.agentTag | Should -Be '1.2.4.0'
            $result.Details.expectedVersion | Should -Be '1.2.3.0'
        }
    }

    Context 'Test-OpenClawImageVersionAligned - edge cases (same-wrong-version, pre-mvp)' {
        It 'reports IsExpected false when both tags agree with each other but not with ExpectedVersion' {
            # Arrange / Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference 'openclaw/core:1.2.2.0' -AgentImageReference 'openclaw/agent:1.2.2.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeFalse
            $result.Summary | Should -Match 'version mismatch'
        }

        It 'reports IsExpected false with a distinguishing message when a tag is the pre-mvp floating tag' {
            # Arrange / Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference 'openclaw/core:pre-mvp' -AgentImageReference 'openclaw/agent:1.2.3.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeFalse
            $result.Summary | Should -Match 'malformed'
            $result.Summary | Should -Match 'pre-mvp'
        }
    }

    Context 'Test-OpenClawImageVersionAligned - malformed tags' {
        It 'reports IsExpected false with a message distinguishing missing tag from mismatched version' {
            # Arrange / Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference 'openclaw/core' -AgentImageReference 'openclaw/agent:1.2.3.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeFalse
            $result.Summary | Should -Match 'malformed'
            $result.Details.coreTag | Should -Be ''
        }

        It 'reports IsExpected false naming the literal malformed string <_>' -ForEach @('1.2.3', 'v1.2.3.0', '1.2.3.a', 'latest') {
            # Arrange
            $malformedTag = $_

            # Act
            $result = Test-OpenClawImageVersionAligned -CoreImageReference "openclaw/core:$malformedTag" -AgentImageReference 'openclaw/agent:1.2.3.0' -ExpectedVersion '1.2.3.0'

            # Assert
            $result.IsExpected | Should -BeFalse
            $result.Summary | Should -Match ([regex]::Escape($malformedTag))
        }
    }
}
