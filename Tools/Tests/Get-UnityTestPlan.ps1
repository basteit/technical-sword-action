param(
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string]$Suite = 'daily',
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'all')]
    [string[]]$AdditionalSuite = @()
)
$ErrorActionPreference = 'Stop'
$selection = @((@($Suite) + @($AdditionalSuite)) | Select-Object -Unique)
if ($selection.Count -gt 1) {
    $plans = @($selection | ForEach-Object { & $PSCommandPath -Suite $_ })
    [pscustomobject]@{
        suite = $selection -join '+'
        description = ($plans.description -join ' ')
        testMode = 'PlayMode'
        filterType = 'regex'
        filterValue = '(?:' + (($plans | ForEach-Object { '(?:' + $_.filterValue + ')' }) -join '|') + ')'
        fixtures = @($plans.fixtures | Sort-Object -Unique)
    }
    return
}
$registry = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-suites.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($registry.schemaVersion -ne 1) { throw 'Unsupported test suite schema.' }
$fixtures = @($registry.fixtures | Where-Object { $Suite -eq 'all' -or $_.suites -contains $Suite } | ForEach-Object name | Sort-Object)
if ($fixtures.Count -eq 0) { throw ('No fixtures selected for ' + $Suite) }
$names = @($fixtures | ForEach-Object { [regex]::Escape($_) }) -join '|'
$filter = '^(?:.*\.)?(?:' + $names + ')\.'
[pscustomobject]@{
    suite = $Suite
    description = $registry.suites.$Suite
    testMode = 'PlayMode'
    filterType = 'regex'
    filterValue = $filter
    fixtures = $fixtures
}
