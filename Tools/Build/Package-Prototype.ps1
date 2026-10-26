param([Parameter(Mandatory = $true)][string]$BuildReport)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$builds = [IO.Path]::GetFullPath((Join-Path $root 'Builds/Playtest')) + [IO.Path]::DirectorySeparatorChar
$reportPath = (Resolve-Path -LiteralPath $BuildReport).Path
if (-not $reportPath.StartsWith($builds, [StringComparison]::OrdinalIgnoreCase)) { throw 'Report must be inside this workspace Builds/Playtest.' }
$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $report.completed -or -not $report.success) { throw 'A successful, completed Unity build is required.' }
$source = [IO.Path]::GetFullPath($report.outputDirectory)
if (-not $source.StartsWith($builds, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build output is outside Builds/Playtest.' }
$directory = [IO.Path]::GetDirectoryName($reportPath)
$package = Join-Path $directory 'Package/TechnicalSwordAction-Prototype'
$zip = Join-Path $directory ($report.buildId + '.zip')
if ((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)) { throw 'Refusing to overwrite an existing package.' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | Where-Object {
    $_.Name -notmatch 'BackUpThisFolder_ButDontShipItWithYourGame|BurstDebugInformation_DoNotShip|\.pdb$'
} | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $package -Recurse }
# Preserve the supplied package license/notice texts; never include package source trees.
$cache = Join-Path $root 'Library/PackageCache'
Get-ChildItem -LiteralPath $cache -Directory | ForEach-Object {
    $dependency = $_
    $notices = @(Get-ChildItem -LiteralPath $dependency.FullName -File | Where-Object {
        $_.Name -match '^(licen[cs]e(?:\..*)?|third[ -]?party[ -]?notices?(?:\..*)?)$'
    })
    if ($notices.Count) {
        $destination = Join-Path $package ('ThirdPartyNotices/' + $dependency.Name)
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $notices | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination }
    }
}
$required = @('TechnicalSwordAction.exe', 'UnityPlayer.dll', 'TechnicalSwordAction_Data', 'MonoBleedingEdge', 'README.txt', 'BUILD_INFO.json')
foreach ($item in $required) { if (-not (Test-Path -LiteralPath (Join-Path $package $item))) { throw ('Missing required player item: ' + $item) } }
$files = @(Get-ChildItem -LiteralPath $package -Recurse -File -Force)
$unexpected = @($files | Where-Object { $_.Name -match '(^UnityEditor.*\.dll$|^Assembly-CSharp-Editor\.dll$|\.(cs|aseprite|psd|pdb)$)' })
if ($unexpected.Count) { throw ('Editor/source/debug files in package: ' + ($unexpected.Name -join ', ')) }
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
    if ($entries.Count -ne $files.Count) { throw 'ZIP file count differs from package.' }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        $entry = $archive.GetEntry('TechnicalSwordAction-Prototype/' + $relative)
        if ($null -eq $entry -or $entry.Length -ne $file.Length) { throw ('Missing/incorrect ZIP entry: ' + $relative) }
        $stream = $entry.Open(); $hash = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $hash.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw ('ZIP hash mismatch: ' + $relative) }
    }
} finally { $archive.Dispose() }
[pscustomobject]@{
    buildId = $report.buildId; zip = $zip; package = $package; files = $files.Count
    bytes = (Get-Item -LiteralPath $zip).Length; sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    archiveVerified = $true
} | ConvertTo-Json
