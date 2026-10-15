param([string]$FfmpegPath,[ValidatePattern('^[a-z0-9-]*$')][string]$Revision='')
$ErrorActionPreference = 'Stop'
$videoProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $videoProjectRoot
try {
    if (-not $FfmpegPath) {
        $videoFfmpegCommand = Get-Command ffmpeg -ErrorAction SilentlyContinue
        if ($videoFfmpegCommand) { $FfmpegPath = $videoFfmpegCommand.Source }
        else {
            $videoBundledFfmpeg = Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($videoBundledFfmpeg) { $FfmpegPath = $videoBundledFfmpeg.FullName }
        }
    }
    if (-not $FfmpegPath) { throw 'Provide -FfmpegPath or install FFmpeg; see docs/video/capture-guide.md.' }
    New-Item -ItemType Directory -Force -Path 'docs/video/previews' | Out-Null
    foreach ($videoVariant in @('2stage-hybrid','4stage-hybrid','4stage-pixel')) {
        $videoCaptureDir = Join-Path '.uloop/outputs/video' $videoVariant
        $videoEventFile = Join-Path $videoCaptureDir 'events.json'
        $videoMetadata = Get-Content -Raw -LiteralPath $videoEventFile | ConvertFrom-Json
        if ($videoMetadata.fps -ne 60 -or $videoMetadata.duration -lt 3 -or $videoMetadata.duration -gt 6) { throw "Invalid capture timing: $videoVariant" }
        for ($videoFrameIndex = 0; $videoFrameIndex -lt $videoMetadata.frameCount; $videoFrameIndex++) {
            if (-not (Test-Path -LiteralPath (Join-Path $videoCaptureDir ('frame-{0:D4}.png' -f $videoFrameIndex)))) { throw "Missing frame $videoFrameIndex in $videoVariant" }
        }
        $videoTier = if ($videoMetadata.playerArtworkIsRough) { 'rough' } else { 'final' }
        $videoName = $videoVariant -replace 'stage-', "stage-$videoTier-"
        if ($Revision) { $videoName="$videoVariant-$Revision" }
        $videoOutput = "docs/video/previews/combat-$videoName.mp4"
        & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i (Join-Path $videoCaptureDir 'frame-%04d.png') -frames:v $videoMetadata.frameCount -c:v libx264 -crf 16 -preset slow -pix_fmt yuv420p -an -movflags +faststart -y $videoOutput
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $videoVariant" }
        Copy-Item -LiteralPath $videoEventFile -Destination "docs/video/previews/combat-$videoName.json"
        Write-Output "$videoOutput : $($videoMetadata.frameCount) frames / $($videoMetadata.duration) seconds / no audio"
    }
} finally { Pop-Location }
