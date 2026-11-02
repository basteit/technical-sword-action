param([string]$Revision='20261103-hitstop-return-v1',[string]$OutputTag='20261103-hitstop-return',[string]$ComparisonRevision='20261102-expressive-v1',[string]$FfmpegPath,[switch]$IncludeCounter)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if($Revision -notmatch '^[a-zA-Z0-9-]+$' -or $OutputTag -notmatch '^[a-zA-Z0-9-]+$' -or $ComparisonRevision -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid revision/tag.'}
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    $before=".uloop/outputs/video/adaptive-parry-$ComparisonRevision/adaptive"
    $after=".uloop/outputs/video/adaptive-parry-$Revision/adaptive"
    $a=Get-Content -LiteralPath (Join-Path $before 'events.json') -Raw | ConvertFrom-Json
    $b=Get-Content -LiteralPath (Join-Path $after 'events.json') -Raw | ConvertFrom-Json
    if($a.legacy -or $b.legacy -or $a.frameCount -ne 180 -or $b.frameCount -ne 180 -or $a.maximumStopTicks -ne 30 -or $b.maximumStopTicks -ne 30 -or $a.impactFrame -ne $b.impactFrame -or $a.reactionFrame -ne $b.reactionFrame -or $b.leadCombatFrames -ne 8 -or $b.impactEnemySprite -ne 'EnemySample_attack_14' -or $b.reactionFrame -le $b.impactFrame -or [Math]::Abs($b.maximumBackgroundAlpha-.4) -gt .001 -or [Math]::Abs($b.minimumCameraSize-4.25) -gt .001 -or $b.cameraRestoredFrame -le $b.cameraReturnFrame -or $b.playerSortingOrder -le $b.enemySortingOrder -or $b.maximumPlayerJitterPixels -ne 2 -or $b.maximumEnemyJitterPixels -ne 2 -or $b.orangeSparkFrames -le 0 -or $b.orangeGlowFrames -le 0 -or $b.legacyHaloSeen -or -not $b.configuredExpressiveCamera -or -not $b.configuredReferenceAccent -or $b.referenceFlareFrames -le 0 -or $b.peakParryParticles -ne 32 -or $b.configuredPositionLeadFrames -ne 2 -or $b.configuredZoomDelayFrames -ne 2){throw 'Unexpected capture invariant.'}
    if(-not $b.configuredReturnOnHitstopEnd -or $b.cameraReturnFrame -ne $b.firstHitstopReleaseFrame -or $b.cameraReturnFrame -ge $a.cameraReturnFrame){throw 'Camera did not return earlier on hitstop release.'}
    for($i=0;$i -lt 180;$i++){
        foreach($dir in @($before,$after)){if(-not(Test-Path -LiteralPath (Join-Path $dir ('frame-{0:D4}.png' -f $i)))){throw "Missing frame $i in $dir"}}
    }
    $stem="docs/video/previews/parry-adaptive-$OutputTag"
    $on="$stem-on.mp4";$comparison="$stem-comparison.mp4";$detail="$stem-detail.mp4";$gif="$stem-detail.gif"
    $outputs=@($on,$comparison,$detail,$gif)
    if($IncludeCounter){
        $counterDir=".uloop/outputs/video/adaptive-parry-$Revision/counter"
        $c=Get-Content -LiteralPath (Join-Path $counterDir 'events.json') -Raw | ConvertFrom-Json
        if(-not $c.counterSequence -or -not $c.counterInheritedCamera -or $c.frameCount -ne 300 -or $c.cameraReturnFrame -ne $c.firstHitstopReleaseFrame -or $c.counterStartFrame -le $c.cameraReturnFrame -or $c.counterHitFrame -le $c.counterChargeFrame -or $c.enemyDamageHits -ne 1 -or $c.counterDamage -ne 90 -or $c.finalEnemyHp -ne 0 -or $c.finalPlayerHp -ne 100 -or $c.counterFinishedFrame -le $c.counterHitFrame -or $c.counterLaunchEndFrame -le $c.counterHitFrame -or $c.cameraRestoredFrame -le $c.counterFinishedFrame){throw 'Unexpected counter sequence.'}
        for($i=0;$i -lt $c.frameCount;$i++){if(-not(Test-Path -LiteralPath (Join-Path $counterDir ('frame-{0:D4}.png' -f $i)))){throw "Missing counter frame $i"}}
        $counterOn="$stem-counter.mp4";$counterDetail="$stem-counter-detail.mp4";$outputs+=@($counterOn,$counterDetail)
    }
    foreach($file in $outputs){if(Test-Path -LiteralPath $file){throw "Preserve existing preview: $file"}}
    $font='C\:/Windows/Fonts/arial.ttf'
    $label="drawtext=fontfile='$font':fontsize=28:fontcolor=white:box=1:boxcolor=black@0.75:boxborderw=7:x=14:y=14"
    # Keep the whole gameplay camera. Resize only; do not crop or simulate a close-up.
    $full='scale=1280:720:flags=neighbor'
    $compare='scale=640:360:flags=neighbor'
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 60 -i (Join-Path $after 'frame-%04d.png') -frames:v 90 -vf "${full},fps=30,${label}:text='LATEST - GAME CAMERA - REAL TIME'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $on
    if($LASTEXITCODE -ne 0){throw 'Real-time encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 60 -i (Join-Path $before 'frame-%04d.png') -framerate 60 -i (Join-Path $after 'frame-%04d.png') -filter_complex "[0:v]${compare},fps=30,${label}:text='PREVIOUS - REAL TIME'[a];[1:v]${compare},fps=30,${label}:text='LATEST - REAL TIME'[b];[a][b]vstack=inputs=2[out]" -map '[out]' -frames:v 90 -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $comparison
    if($LASTEXITCODE -ne 0){throw 'Comparison encode failed.'}
    $start=[Math]::Max(0,$b.successFrame-4);$end=[Math]::Min(180,$b.reactionFrame+12);$count=$end-$start
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 30 -start_number $start -i (Join-Path $after 'frame-%04d.png') -frames:v $count -vf "${full},${label}:text='LATEST - GAME CAMERA - 0.5x INSPECTION'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $detail
    if($LASTEXITCODE -ne 0){throw 'Inspection encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -i $detail -filter_complex 'fps=20,scale=640:360:flags=neighbor,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3' -an -loop 0 $gif
    if($LASTEXITCODE -ne 0){throw 'GIF encode failed.'}
    if($IncludeCounter){
        & $FfmpegPath -hide_banner -loglevel error -n -framerate 60 -i (Join-Path $counterDir 'frame-%04d.png') -frames:v 150 -vf "${full},fps=30,${label}:text='PARRY TO COUNTER - GAME CAMERA - REAL TIME'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $counterOn
        if($LASTEXITCODE -ne 0){throw 'Counter real-time encode failed.'}
        $counterStart=[Math]::Max(0,$c.successFrame-4);$counterEnd=[Math]::Min($c.frameCount,$c.cameraRestoredFrame+12);$counterCount=$counterEnd-$counterStart
        & $FfmpegPath -hide_banner -loglevel error -n -framerate 30 -start_number $counterStart -i (Join-Path $counterDir 'frame-%04d.png') -frames:v $counterCount -vf "${full},${label}:text='PARRY TO COUNTER - 0.5x INSPECTION'" -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $counterDetail
        if($LASTEXITCODE -ne 0){throw 'Counter inspection encode failed.'}
    }
    foreach($file in $outputs){Get-Item -LiteralPath $file | Select-Object FullName,Length}
} finally {Pop-Location}
