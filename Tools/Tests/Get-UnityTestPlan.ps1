param(
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'video', 'video-acceptance', 'all')]
    [string]$Suite = 'daily',
    [ValidateSet('daily', 'movement', 'input', 'combat', 'presentation', 'audio', 'dialogue', 'video', 'video-acceptance', 'all')]
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
        legacyVideoAcceptanceIncluded = @($plans | Where-Object legacyVideoAcceptanceIncluded).Count -gt 0
    }
    return
}
$registry = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-suites.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($registry.schemaVersion -ne 1) { throw 'Unsupported test suite schema.' }
$fixtures = @($registry.fixtures | Where-Object { $Suite -eq 'all' -or $_.suites -contains $Suite } | ForEach-Object name | Sort-Object)
$legacy = @($registry.videoAcceptancePatterns)
if ($Suite -eq 'video-acceptance') {
    $filter = '(?:' + ($legacy -join '|') + ')'
    $fixtures = @('DefenseCancelShowcaseTests')
} else {
    if ($fixtures.Count -eq 0) { throw ('No fixtures selected for ' + $Suite) }
    $names = @($fixtures | ForEach-Object { [regex]::Escape($_) }) -join '|'
    $exclude = ''
    if ($Suite -eq 'video') {
        # Unity also matches parameterized parent suites. Selecting that parent
        # would pull excluded four-stage cases back into the run.
        $excluded = @($legacy) + @($registry.videoExcludedParentPatterns)
        $exclude = '(?!.*(?:' + ($excluded -join '|') + '))'
    }
    $filter = '^' + $exclude + '(?:.*\.)?(?:' + $names + ')\.'
}
[pscustomobject]@{
    suite = $Suite
    description = $registry.suites.$Suite
    testMode = 'PlayMode'
    filterType = 'regex'
    filterValue = $filter
    fixtures = $fixtures
    legacyVideoAcceptanceIncluded = $Suite -eq 'all' -or $Suite -eq 'video-acceptance'
}
