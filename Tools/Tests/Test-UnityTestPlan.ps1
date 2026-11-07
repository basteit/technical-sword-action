param([string]$ResultXml)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$registry = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-suites.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$script:checks = 0
function Assert-Plan([bool]$Condition, [string]$Message) {
    $script:checks++
    if (-not $Condition) { throw $Message }
}
function Test-EffectiveSelection([System.Xml.XmlNode]$Case, [regex]$Pattern) {
    $node = $Case
    while ($node -is [System.Xml.XmlElement]) {
        # Unity regex filters can select an ancestor suite and all its children.
        if ($Pattern.IsMatch($node.GetAttribute('fullname'))) { return $true }
        $node = $node.ParentNode
    }
    return $false
}
Push-Location $root
try {
    $paths = @(rg --files Assets/Tests/PlayMode -g '*Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate PlayMode test sources.' }
    $actual = @($paths | ForEach-Object {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($_)
        $source = Get-Content -LiteralPath $_ -Raw -Encoding UTF8
        $declaration = [regex]::Match($source, '(?m)^\s*public\s+(?:sealed\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)')
        Assert-Plan ($declaration.Success -and $declaration.Groups[1].Value -eq $name) ('Fixture class/file name mismatch: ' + $_)
        $name
    } | Sort-Object)
    $registered = @($registry.fixtures.name | Sort-Object)
    Assert-Plan (@($registered | Select-Object -Unique).Count -eq $registered.Count) 'Duplicate fixture registration.'
    Assert-Plan (@(Compare-Object $actual $registered).Count -eq 0) 'PlayMode fixture registry is incomplete or stale.'
    $plans = @{}
    foreach ($suite in $registry.suites.PSObject.Properties.Name) {
        $plans[$suite] = & (Join-Path $PSScriptRoot 'Get-UnityTestPlan.ps1') -Suite $suite
        $null = [regex]::new($plans[$suite].filterValue)
        Assert-Plan ($plans[$suite].fixtures.Count -gt 0) ('Empty suite: ' + $suite)
    }
    foreach ($fixture in $registry.fixtures) {
        Assert-Plan ($fixture.suites.Count -gt 0) ('Unclassified fixture: ' + $fixture.name)
        foreach ($suite in $fixture.suites) {
            Assert-Plan ($registry.suites.PSObject.Properties.Name -contains $suite) ('Unknown suite: ' + $suite)
        }
        $example = 'TechnicalSwordAction.PlayerState.Tests.' + $fixture.name + '.Example(60,True)'
        Assert-Plan ($example -match $plans['all'].filterValue) ('All suite misses ' + $fixture.name)
    }
    foreach ($critical in @('CombatTimeIntegrationTests','PlayerInputRouterTests','PlayerStateContractTests','PlayerBasicActionsTests','PlayerAttackMotionTests','PrototypeCombatContractTests','ProximityParryTests','PrototypeMobilityArtTests','PrototypeActionStageTests','PrototypePlayerMovementFixTests')) {
        Assert-Plan ($plans['daily'].fixtures -contains $critical) ('Daily omits critical fixture: ' + $critical)
    }
    foreach ($optional in @('PrototypeCounterCinematicTests','PrototypeSfxTests','PrototypeDialogueTests')) {
        Assert-Plan ($plans['daily'].fixtures -notcontains $optional) ('Daily unexpectedly includes optional fixture: ' + $optional)
    }
    $combined = & (Join-Path $PSScriptRoot 'Get-UnityTestPlan.ps1') -Suite daily -AdditionalSuite @('movement','audio')
    $audio = 'TechnicalSwordAction.PlayerState.Tests.PrototypeSfxTests.Example(60,True)'
    $dialogue = 'TechnicalSwordAction.PlayerState.Tests.PrototypeDialogueTests.Example(60,True)'
    Assert-Plan ($audio -match $combined.filterValue) 'Combined selection loses audio.'
    Assert-Plan ($dialogue -notmatch $combined.filterValue) 'Combined selection unexpectedly includes dialogue.'
    $movement = 'TechnicalSwordAction.PlayerState.Tests.PrototypeMobilityArtTests.Example(60,True)'
    Assert-Plan ($movement -match $combined.filterValue) 'Combined selection loses movement.'
    $counts = @()
    if ($ResultXml) {
        [xml]$xml = Get-Content -LiteralPath $ResultXml -Raw -Encoding UTF8
        $cases = @($xml.SelectNodes('//test-case'))
        Assert-Plan ($cases.Count -gt 0) 'Reference XML contains no test cases.'
        foreach ($suite in $registry.suites.PSObject.Properties.Name) {
            $pattern = [regex]::new($plans[$suite].filterValue)
            $leafCount = @($cases | Where-Object { $pattern.IsMatch($_.fullname) }).Count
            $count = @($cases | Where-Object { Test-EffectiveSelection $_ $pattern }).Count
            Assert-Plan ($count -eq $leafCount) ('Parent suite matching changes the intended selection: ' + $suite)
            $counts += [pscustomobject]@{suite=$suite;referenceCases=$count}
        }
        Assert-Plan (@($cases | Where-Object { $_.fullname -match $plans['all'].filterValue }).Count -eq $cases.Count) 'All suite loses reference cases.'
        $dailyMovement = & (Join-Path $PSScriptRoot 'Get-UnityTestPlan.ps1') -Suite daily -AdditionalSuite movement
        $dailyCount = @($cases | Where-Object { $_.fullname -match $plans['daily'].filterValue }).Count
        Assert-Plan (@($cases | Where-Object { $_.fullname -match $dailyMovement.filterValue }).Count -eq $dailyCount) 'Combined daily/movement duplicates or loses reference cases.'
    }
    [pscustomobject]@{success=$true;checks=$script:checks;registeredFixtures=$registered.Count;referenceCounts=$counts} | ConvertTo-Json -Depth 5
} finally { Pop-Location }
