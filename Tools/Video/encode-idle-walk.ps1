param([string]$FfmpegPath, [ValidateSet('game-scale','speed-1p5x','speed-2x')][string]$Take='game-scale')
$ErrorActionPreference='Stop'
$motionVideoRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $motionVideoRoot
try {
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    if(-not $FfmpegPath){throw 'Provide -FfmpegPath.'}
    $motionSource=".uloop/outputs/video/studio-idle-walk-$Take"
    $motionMetadata=Get-Content -Raw -LiteralPath "$motionSource/events.json" | ConvertFrom-Json
    if($motionMetadata.takeKind -ne 'original-idle-walk' -or $motionMetadata.fps -ne 60 -or $motionMetadata.frameCount -ne 360){throw 'Invalid motion capture metadata.'}
    for($motionFrame=0;$motionFrame -lt $motionMetadata.frameCount;$motionFrame++){
        if(-not(Test-Path -LiteralPath (Join-Path $motionSource ('frame-{0:D4}.png' -f $motionFrame)))){throw "Missing frame $motionFrame"}
    }
    $motionOutput="docs/video/previews/studio-idle-walk-$Take.mp4"
    & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i "$motionSource/frame-%04d.png" -frames:v $motionMetadata.frameCount -c:v libx264 -crf 16 -preset slow -pix_fmt yuv420p -an -movflags +faststart -y $motionOutput
    if($LASTEXITCODE -ne 0){throw 'Encoding failed.'}
    Copy-Item -LiteralPath "$motionSource/events.json" -Destination ($motionOutput -replace '\.mp4$','.json')
    Write-Output "$motionOutput : 360 frames / 6 seconds / no audio"
} finally { Pop-Location }
