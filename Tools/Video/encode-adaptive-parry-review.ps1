param([string]$Revision='20261102-sync-v2',[string]$OutputTag='20261102-v2',[string]$ComparisonRevision='20261102-sync-v1',[string]$FfmpegPath)
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
    if($a.legacy -or $b.legacy -or $a.frameCount -ne 180 -or $b.frameCount -ne 180 -or $a.maximumStopTicks -ne 40 -or $b.maximumStopTicks -ne 36 -or $b.leadCombatFrames -ne 8 -or $b.impactEnemySprite -ne 'EnemySample_attack_14' -or $b.reactionFrame -le $b.impactFrame -or [Math]::Abs($b.maximumBackgroundAlpha-.3) -gt .001 -or [Math]::Abs($b.cameraOrthographicSize-5.625) -gt .001){throw 'Unexpected capture invariant.'}
    for($i=0;$i -lt 180;$i++){
        foreach($dir in @($before,$after)){if(-not(Test-Path -LiteralPath (Join-Path $dir ('frame-{0:D4}.png' -f $i)))){throw "Missing frame $i in $dir"}}
    }
    $stem="docs/video/previews/parry-adaptive-$OutputTag"
    $on="$stem-on.mp4";$comparison="$stem-comparison.mp4";$detail="$stem-detail.mp4";$gif="$stem-detail.gif"
    foreach($file in @($on,$comparison,$detail,$gif)){if(Test-Path -LiteralPath $file){throw "Preserve existing preview: $file"}}
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
    foreach($file in @($on,$comparison,$detail,$gif)){Get-Item -LiteralPath $file | Select-Object FullName,Length}
} finally {Pop-Location}
