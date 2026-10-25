param(
    [string]$ULoop = 'C:\Users\hirot\AppData\Roaming\npm\uloop.cmd',
    [switch]$Focused
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
        if ($attempt -lt 8 -and $raw -match 'reloading|Domain Reload|starting') {
            Start-Sleep -Seconds 2
            continue
        }
        throw ('ULoop command failed: ' + $raw)
    }
}
Push-Location $projectDirectory
try {
    $report.commit = (git rev-parse HEAD).Trim()
    $commands = @(
        @{ name = 'compile'; arguments = @('compile') },
        @{ name = 'assets'; arguments = @('execute-dynamic-code', '--code', 'PrototypeCombatValidator.ValidateMenu(); return true;') },
        @{ name = 'EditMode'; arguments = @('run-tests', '--test-mode', 'EditMode') },
        @{ name = 'PlayMode'; arguments = @('run-tests', '--test-mode', 'PlayMode') },
        @{ name = 'Console'; arguments = @('get-logs', '--log-type', 'Error', '--max-count', '100', '--include-stack-trace', 'true') }
    )
    foreach ($command in $commands) {
        $arguments = $command.arguments
        if ($Focused -and $command.name -eq 'PlayMode') {
            $arguments += @('--filter-type', 'regex', '--filter-value', 'PrototypeCombatContractTests')
        }
        Write-Host ('Running ' + $command.name)
        $result = Invoke-ULoopCheck -CommandArguments $arguments
        $report.checks += @{ name = $command.name; result = $result }
        if ($command.name -eq 'Console') {
            if ($result.TotalCount -gt 0) { throw 'Console contains errors; inspect the saved report.' }
        } elseif (-not $result.Success) { throw ($command.name + ' failed; inspect the saved report.') }
        if ($command.name -eq 'compile' -and $result.WarningCount -gt 0) { throw 'Compilation has warnings.' }
    }
    $report.success = $true
} finally {
    $report.completedAt = (Get-Date).ToUniversalTime().ToString('o')
    $json = $report | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($reportPath, $json, [System.Text.UTF8Encoding]::new($false))
    Pop-Location
    Write-Host ('Report: ' + $reportPath)
}
