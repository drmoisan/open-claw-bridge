#Requires -Version 7
<#
.SYNOPSIS
Verifies the Application RBAC scope boundary with one in-scope and one
out-of-scope mailbox (master checklist section 12 Step 7 / section 13 Step 3).
Read-only.
#>

<#
.SYNOPSIS
Tests that the assistant app can reach the in-scope mailbox and cannot reach
the out-of-scope mailbox, returning a structured pass/fail result.

.DESCRIPTION
Implements master checklist section 12 Step 7 and section 13 Step 3. Makes
exactly two Invoke-OpenClawTestServicePrincipalAuthorization calls (one per
mailbox) and evaluates the returned rows: the in-scope mailbox is allowed when
at least one row reports InScope = True; the out-of-scope mailbox is denied
when no row reports InScope = True. Succeeded is $true only for
(allowed, denied); every other combination sets Succeeded = $false with a
precise FailureReason naming the failing side(s), joined with '; ' when both
sides fail. The function is strictly read-only, returns a [pscustomobject],
and never calls exit - exit-code mapping belongs to the entry script only.

The underlying test cmdlet bypasses the RBAC propagation cache (30 minutes to
2 hours elsewhere), so this is the fastest way to validate the configuration.

.PARAMETER EnterpriseApplicationObjectId
The Enterprise Application service principal Object ID from Entra ID (NOT the
App Registration object ID).

.PARAMETER InScopeMailbox
SMTP address of a mailbox expected to be inside the scope.

.PARAMETER OutOfScopeMailbox
SMTP address of a mailbox expected to be outside the scope.

.OUTPUTS
[pscustomobject] with InScopeMailbox, OutOfScopeMailbox, InScopeAllowed,
OutOfScopeDenied, Succeeded, FailureReason, InScopeDetails, OutOfScopeDetails.

.EXAMPLE
$result = Test-OpenClawScopeBoundary -EnterpriseApplicationObjectId $spObjectId -InScopeMailbox 'in@contoso.com' -OutOfScopeMailbox 'out@contoso.com'
#>
function Test-OpenClawScopeBoundary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [guid]$EnterpriseApplicationObjectId,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
        [string]$InScopeMailbox,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^[^@\s]+@[^@\s]+\.[^@\s]+$')]
        [string]$OutOfScopeMailbox
    )

    $identity = $EnterpriseApplicationObjectId.ToString()

    $inScopeDetails = @(
        Invoke-OpenClawTestServicePrincipalAuthorization -Identity $identity -Resource $InScopeMailbox
    )
    $outOfScopeDetails = @(
        Invoke-OpenClawTestServicePrincipalAuthorization -Identity $identity -Resource $OutOfScopeMailbox
    )

    $inScopeAllowed = [bool]($inScopeDetails | Where-Object { $_.InScope -eq $true })
    $outOfScopeDenied = -not [bool]($outOfScopeDetails | Where-Object { $_.InScope -eq $true })
    $succeeded = $inScopeAllowed -and $outOfScopeDenied

    $failureReasons = @()
    if (-not $inScopeAllowed) {
        $failureReasons += 'in-scope mailbox has no effective role or InScope=False'
    }
    if (-not $outOfScopeDenied) {
        $failureReasons += 'out-of-scope mailbox is unexpectedly in scope'
    }
    $failureReason = $null
    if ($failureReasons.Count -gt 0) {
        $failureReason = $failureReasons -join '; '
    }

    return [pscustomobject]@{
        InScopeMailbox    = $InScopeMailbox
        OutOfScopeMailbox = $OutOfScopeMailbox
        InScopeAllowed    = $inScopeAllowed
        OutOfScopeDenied  = $outOfScopeDenied
        Succeeded         = $succeeded
        FailureReason     = $failureReason
        InScopeDetails    = $inScopeDetails
        OutOfScopeDetails = $outOfScopeDetails
    }
}
