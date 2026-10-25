param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$versionFile = Join-Path $root 'ProjectSettings\ProjectVersion.txt'
if (-not $UnityEditor) {
    $version = ((Get-Content -LiteralPath $versionFile | Select-String '^m_EditorVersion: ').Line -split ': ', 2)[1]
    $UnityEditor = Join-Path ${env:ProgramFiles} ('Unity\Hub\Editor\' + $version + '\Editor')
}
$driver = Join-Path $UnityEditor 'Data\Tools\UnityYAMLMerge.exe'
if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) { throw ('UnityYAMLMerge not found: ' + $driver) }
$driver = [System.IO.Path]::GetFullPath($driver).Replace('\', '/')
Push-Location $root
try {
    git config --local merge.unityyamlmerge.name 'Unity Smart Merge'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot configure merge driver name.' }
    # Windows PowerShell 5.1 drops embedded quotes when passing a string to a native executable.
    # Build one Windows argv element explicitly so spaces in Program Files and asset paths survive.
    $expectedDriver = '"' + $driver + '" merge -p "%O" "%B" "%A" "%A"'
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = (Get-Command git.exe).Source
    $processInfo.WorkingDirectory = $root
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.Arguments = 'config --local merge.unityyamlmerge.driver "' + $expectedDriver.Replace('"', '\"') + '"'
    $process = [System.Diagnostics.Process]::Start($processInfo)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Cannot configure merge driver command.' }
    $actualDriver = (git config --local --get merge.unityyamlmerge.driver | Out-String).Trim()
    if ($actualDriver -ne $expectedDriver) { throw ('Merge driver verification failed: ' + $actualDriver) }
    git config --local merge.unityyamlmerge.recursive binary
    if ($LASTEXITCODE -ne 0) { throw 'Cannot configure merge driver recursion.' }
    git config --local --get-regexp '^merge\.unityyamlmerge\.'
} finally { Pop-Location }
