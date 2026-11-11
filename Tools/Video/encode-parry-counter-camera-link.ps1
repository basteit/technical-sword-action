param([string]$Revision='20261103-camera-link-v1',[string]$OutputTag='20261103',[string]$FfmpegPath)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if($Revision -notmatch '^[a-zA-Z0-9-]+$' -or $OutputTag -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid revision/tag.'}
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    $before=".uloop/outputs/video/adaptive-parry-$Revision/counter-before"
    $after=".uloop/outputs/video/adaptive-parry-$Revision/counter"
    $a=Get-Content -LiteralPath (Join-Path $before 'events.json') -Raw | ConvertFrom-Json
    $b=Get-Content -LiteralPath (Join-Path $after 'events.json') -Raw | ConvertFrom-Json
    foreach($r in @($a,$b)){
        if(-not $r.counterSequence -or $r.frameCount -ne 360 -or $r.maximumCapturedWindupFrames -ne 96 -or $r.windupSampleFrames -lt 96 -or $r.windupStartFrame -gt 1 -or $r.enemyDamageHits -ne 1 -or $r.counterDamage -ne 90 -or $r.finalEnemyHp -ne 0 -or $r.finalPlayerHp -ne 100 -or $r.cameraRestoredFrame -le $r.counterFinishedFrame){throw 'Incomplete windup/counter recording.'}
    }
    foreach($key in @('successFrame','impactFrame','firstHitstopReleaseFrame','counterInputFrame','counterStartFrame','counterChargeFrame','counterReleaseFrame','counterHitFrame','counterLaunchEndFrame','counterFinishedFrame')){if($a.$key -ne $b.$key){throw "Timing differs: $key"}}
    if($a.configuredCounterCameraBridge -or -not $b.configuredCounterCameraBridge -or $b.cameraReturnFrame -ge 0 -or $b.cameraBridgeFrame -lt 0 -or $a.maximumCameraSizeBetweenStopAndCharge -le 4.25 -or $b.maximumCameraSizeBetweenStopAndCharge -gt 4.2501){throw 'Unexpected camera-link comparison.'}
    for($i=0;$i -lt 360;$i++){foreach($dir in @($before,$after)){if(-not(Test-Path -LiteralPath (Join-Path $dir ('frame-{0:D4}.png' -f $i)))){throw "Missing frame $i in $dir"}}}
    $stem="docs/video/previews/parry-counter-camera-link-$OutputTag"
    $on="$stem-on.mp4";$comparison="$stem-comparison.mp4";$detail="$stem-detail.mp4"
    foreach($file in @($on,$comparison,$detail)){if(Test-Path -LiteralPath $file){throw "Preserve existing preview: $file"}}
    $font='C\:/Windows/Fonts/arial.ttf'
    $label="drawtext=fontfile='$font':fontsize=26:fontcolor=white:box=1:boxcolor=black@0.75:boxborderw=7:x=14:y=14"
    $small="drawtext=fontfile='$font':fontsize=23:fontcolor=white:box=1:boxcolor=black@0.75:boxborderw=7:x=14:y=14"
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 60 -i (Join-Path $after 'frame-%04d.png') -frames:v 180 -vf "scale=1280:720:flags=neighbor,fps=30,${label}:text='FULL ENEMY WINDUP - DIRECT CAMERA LINK - REAL TIME'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $on
    if($LASTEXITCODE -ne 0){throw 'Real-time encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 60 -i (Join-Path $before 'frame-%04d.png') -framerate 60 -i (Join-Path $after 'frame-%04d.png') -filter_complex "[0:v]scale=640:360:flags=neighbor,fps=30,${small}:text='PREVIOUS - SAME INPUT'[a];[1:v]scale=640:360:flags=neighbor,fps=30,${small}:text='DIRECT LINK - SAME INPUT'[b];[a][b]vstack=inputs=2[out]" -map '[out]' -frames:v 180 -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $comparison
    if($LASTEXITCODE -ne 0){throw 'Comparison encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 30 -i (Join-Path $after 'frame-%04d.png') -frames:v 360 -vf "scale=1280:720:flags=neighbor,${label}:text='FULL ENEMY WINDUP - DIRECT CAMERA LINK - 0.5x'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $detail
    if($LASTEXITCODE -ne 0){throw 'Slow encode failed.'}
    foreach($file in @($on,$comparison,$detail)){Get-Item -LiteralPath $file | Select-Object FullName,Length}
} finally {Pop-Location}
