param(
    [string]$ULoop = 'C:\Users\hirot\AppData\Roaming\npm\uloop.cmd',
    [switch]$Focused,
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string]$Suite = 'daily',
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string[]]$AdditionalSuite = @()
)
$ErrorActionPreference = 'Stop'
$projectDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$reportDirectory = Join-Path $projectDirectory '.uloop\outputs\PrototypeValidation'
[System.IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
$reportPath = Join-Path $reportDirectory ((Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
$report = [ordered]@{ startedAt = (Get-Date).ToUniversalTime().ToString('o'); commit = ''; checks = @(); success = $false }
function Invoke-ULoopCheck([string[]]$CommandArguments) {
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $previousErrorPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $raw = (& $ULoop @CommandArguments 2>&1 | ForEach-Object { $_.ToString() } | Out-String)
            $commandExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $previousErrorPreference }
        $raw = $raw -replace '\x1b\[[0-9;]*m', ''
        if ($commandExitCode -eq 0) { return ($raw | ConvertFrom-Json) }
        if ($CommandArguments[0] -ne 'run-tests' -and $attempt -lt 8 -and $raw -match 'reloading|Domain Reload|starting') {
            Start-Sleep -Seconds 2
            continue
        }
        throw ('ULoop command failed: ' + $raw)
    }
}
Push-Location $projectDirectory
try {
    $report.commit = (git rev-parse HEAD).Trim()
    $plan = & (Join-Path $projectDirectory 'Tools\Tests\Get-UnityTestPlan.ps1') -Suite $Suite -AdditionalSuite $AdditionalSuite
    $report.suite = $plan.suite
    $commands = @(
        @{ name = 'compile'; arguments = @('compile', '--wait-for-domain-reload', 'true') },
        @{ name = 'assets'; arguments = @('execute-dynamic-code', '--code', 'PrototypeCombatValidator.ValidateMenu(); return true;') },
        @{ name = 'EditMode'; arguments = @('run-tests', '--test-mode', 'EditMode') },
        @{ name = 'PlayMode'; arguments = @('run-tests', '--test-mode', 'PlayMode') },
        @{ name = 'Console'; arguments = @('get-logs', '--log-type', 'Error', '--max-count', '100', '--include-stack-trace', 'true') }
    )
    foreach ($command in $commands) {
        $arguments = $command.arguments
        if ($command.name -eq 'PlayMode') {
            # Preserve the old 97-case shortcut; normal validation now uses the daily suite.
            $filter = if ($Focused) { 'PrototypeCombatContractTests' } else { $plan.filterValue }
            $arguments += @('--filter-type', 'regex', '--filter-value', ('"' + $filter + '"'))
        }
        Write-Host ('Running ' + $command.name)
        if ($command.name -in @('EditMode', 'PlayMode')) {
            & (Join-Path $projectDirectory 'Tools\Tests\Wait-UnityTestIdle.ps1') -ULoop $ULoop
        }
        $result = Invoke-ULoopCheck -CommandArguments $arguments
        $report.checks += @{ name = $command.name; result = $result }
        if ($command.name -eq 'Console') {
            if ($result.TotalCount -gt 0) { throw 'Console contains errors; inspect the saved report.' }
        } elseif (-not $result.Success) { throw ($command.name + ' failed; inspect the saved report.') }
        if ($command.name -eq 'compile' -and $result.WarningCount -gt 0) { throw 'Compilation has warnings.' }
        if ($command.name -in @('EditMode', 'PlayMode')) {
            & (Join-Path $projectDirectory 'Tools\Tests\Wait-UnityTestIdle.ps1') -ULoop $ULoop
        }
    }
    $report.success = $true
} finally {
    $report.completedAt = (Get-Date).ToUniversalTime().ToString('o')
    $json = $report | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($reportPath, $json, [System.Text.UTF8Encoding]::new($false))
    Pop-Location
    Write-Host ('Report: ' + $reportPath)
}
