param(
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string]$Suite = 'daily',
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string[]]$AdditionalSuite = @(),
    [string]$ULoop = 'C:\Users\hirot\AppData\Roaming\npm\uloop.cmd',
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
$plan = & (Join-Path $PSScriptRoot 'Get-UnityTestPlan.ps1') -Suite $Suite -AdditionalSuite $AdditionalSuite
if ($PlanOnly) { $plan | ConvertTo-Json -Depth 6; return }
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'Wait-UnityTestIdle.ps1') -ULoop $ULoop
    # Quotes are intentional: cmd.exe must not interpret regex alternation as pipes.
    $quotedFilter = '"' + $plan.filterValue + '"'
    $arguments = @('run-tests', '--test-mode', 'PlayMode', '--filter-type', 'regex', '--filter-value', $quotedFilter)
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $raw = (& $ULoop @arguments 2>&1 | ForEach-Object { $_.ToString() } | Out-String)
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    $raw = $raw -replace '\x1b\[[0-9;]*m', ''
    Write-Output $raw.Trim()
    # Never retry tests: after a CLI timeout Unity may still be executing the same run.
    if ($code -ne 0) { throw 'ULoop failed. Inspect Unity/test progress before starting another run; do not retry automatically.' }
    $result = $raw | ConvertFrom-Json
    if (-not $result.Success) { throw ('Suite failed: ' + $Suite + '. Inspect XmlPath and Message above.') }
    if ($result.TestCount -le 0) { throw ('No tests executed for suite ' + $Suite) }
    & (Join-Path $PSScriptRoot 'Wait-UnityTestIdle.ps1') -ULoop $ULoop
} finally { Pop-Location }
