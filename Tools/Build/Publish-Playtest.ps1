[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$NotesFile,
    [string]$Repository = 'basteit/technical-sword-action',
    [string]$Title
)
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Use an owner/repository name.' }
if ($Tag -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw 'Use letters, numbers, dots, underscores or hyphens in the release tag.' }

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$builds = [IO.Path]::GetFullPath((Join-Path $root 'Builds/Playtest')) + [IO.Path]::DirectorySeparatorChar
$archivePath = (Resolve-Path -LiteralPath $ZipPath).Path
$notesPath = (Resolve-Path -LiteralPath $NotesFile).Path
if (-not $archivePath.StartsWith($builds, [StringComparison]::OrdinalIgnoreCase)) { throw 'Select a ZIP inside this workspace Builds/Playtest.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($required in 'TechnicalSwordAction.exe', 'UnityPlayer.dll', 'README.txt', 'BUILD_INFO.json') {
        $entry = $archive.GetEntry('TechnicalSwordAction-Prototype/' + $required)
        if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing playtest file $required" }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('TechnicalSwordAction-Prototype/BUILD_INFO.json').Open())
    try { $info = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
} finally { $archive.Dispose() }
if (-not $info.buildId -or $info.sourceCommit -notmatch '^[a-f0-9]{40}$') { throw 'The archive has no valid build identity.' }
if (-not $Title) { $Title = 'Windows試遊版 ' + $info.buildId }
Write-Host ("Validated build {0} ({1}), {2:N1} MiB" -f $info.buildId, $info.sourceCommit, ((Get-Item -LiteralPath $archivePath).Length / 1MB))
if (-not $PSCmdlet.ShouldProcess("$Repository / $Tag", 'Upload the playtest ZIP and publish it as the latest release')) { return }
Get-Command gh -ErrorAction Stop | Out-Null

function Invoke-GitHub([string[]]$Arguments) {
    $response = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($response -join [Environment]::NewLine) }
    return ($response -join [Environment]::NewLine)
}

function Find-TaggedRelease {
    # The tag endpoint only resolves published releases; list releases to include drafts.
    $pages = (Invoke-GitHub -Arguments @('api', "repos/$Repository/releases", '--paginate', '--slurp')) | ConvertFrom-Json
    $taggedReleases = @(foreach ($page in $pages) {
        foreach ($item in $page) {
            if ($item.tag_name -eq $Tag) { $item }
        }
    })
    if ($taggedReleases.Count -gt 1) { throw 'More than one release uses the selected tag.' }
    if ($taggedReleases.Count -eq 1) { return $taggedReleases[0] }
    return $null
}

# A local single-flight lock keeps concurrent publication attempts from replacing the same draft.
$publicationLock = [IO.File]::Open((Join-Path $builds '.publication.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $release = Find-TaggedRelease
    if ($null -ne $release) {
        if (-not $release.draft) { throw 'This tag is already published. Use a new tag for a new archive.' }
    } else {
        Invoke-GitHub -Arguments @('release', 'create', $Tag, '--repo', $Repository, '--target', $info.sourceCommit, '--draft', '--title', $Title, '--notes-file', $notesPath) | Out-Null
        # The release list can briefly lag behind a successful creation.
        for ($lookupAttempt = 0; $lookupAttempt -lt 5; $lookupAttempt++) {
            $release = Find-TaggedRelease
            if ($null -ne $release) { break }
            if ($lookupAttempt -lt 4) { Start-Sleep -Seconds 2 }
        }
        if ($null -eq $release -or -not $release.draft) { throw 'The newly created draft could not be found.' }
    }

    $staging = Join-Path $root ('.codex-tmp/release/' + $Tag)
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    $downloadArchive = Join-Path $staging 'Keraunos-Windows.zip'
    Copy-Item -LiteralPath $archivePath -Destination $downloadArchive -Force
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $downloadArchive -Algorithm SHA256).Hash) { throw 'The staged ZIP differs from the selected archive.' }

    Invoke-GitHub -Arguments @('release', 'edit', $Tag, '--repo', $Repository, '--title', $Title, '--notes-file', $notesPath) | Out-Null
    Invoke-GitHub -Arguments @('release', 'upload', $Tag, $downloadArchive, '--repo', $Repository, '--clobber') | Out-Null
    $draft = (Invoke-GitHub -Arguments @('api', "repos/$Repository/releases/$($release.id)")) | ConvertFrom-Json
    $asset = @($draft.assets | Where-Object { $_.name -eq 'Keraunos-Windows.zip' -and $_.state -eq 'uploaded' })
    if (-not $draft.draft -or $draft.prerelease -or $asset.Count -ne 1 -or
        $asset[0].size -ne (Get-Item -LiteralPath $downloadArchive).Length) { throw 'The draft or uploaded archive could not be verified.' }

    # Publish only after upload verification so a failed upload keeps the previous latest release available.
    Invoke-GitHub -Arguments @('release', 'edit', $Tag, '--repo', $Repository, '--draft=false', '--latest') | Out-Null
    for ($latestAttempt = 0; $latestAttempt -lt 5; $latestAttempt++) {
        $latest = (Invoke-GitHub -Arguments @('api', "repos/$Repository/releases/latest")) | ConvertFrom-Json
        if ($latest.tag_name -eq $Tag) { break }
        if ($latestAttempt -lt 4) { Start-Sleep -Seconds 2 }
    }
    if ($latest.tag_name -ne $Tag) { throw 'The published release is not the current latest release.' }
    [pscustomobject]@{
        release = $latest.html_url
        download = "https://github.com/$Repository/releases/latest/download/Keraunos-Windows.zip"
        buildId = $info.buildId
        sourceCommit = $info.sourceCommit
        workingTreeDirty = $info.workingTreeDirty
        sha256 = (Get-FileHash -LiteralPath $downloadArchive -Algorithm SHA256).Hash
    } | ConvertTo-Json
} finally { $publicationLock.Dispose() }
