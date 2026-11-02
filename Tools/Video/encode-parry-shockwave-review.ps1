param([string]$Revision='20261102-v2',[string]$FfmpegPath)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if($Revision -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid revision.'}
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    $off=".uloop/outputs/video/parry-shockwave-$Revision/strength-000"
    $on=".uloop/outputs/video/parry-shockwave-$Revision/strength-065"
    $a=Get-Content -LiteralPath (Join-Path $off 'events.json') -Raw | ConvertFrom-Json
    $b=Get-Content -LiteralPath (Join-Path $on 'events.json') -Raw | ConvertFrom-Json
    if($a.frameCount -ne 210 -or $b.frameCount -ne 210 -or $a.enemyHp -ne 30 -or $b.enemyHp -ne 30 -or $a.playerHp -ne 100 -or $b.playerHp -ne 100){throw 'Unexpected recording outcome.'}
    for($i=0;$i -lt 210;$i++){
        foreach($dir in @($off,$on)){if(-not(Test-Path -LiteralPath (Join-Path $dir ('frame-{0:D4}.png' -f $i)))){throw "Missing frame $i in $dir"}}
    }
    $destination='docs/video/previews'
    $single=Join-Path $destination 'parry-shockwave-on-20261102.mp4'
    $review=Join-Path $destination 'parry-shockwave-comparison-20261102.mp4'
    $gif=Join-Path $destination 'parry-shockwave-close-20261102.gif'
    $still=Join-Path $destination 'parry-shockwave-comparison-20261102.png'
    $font='C\:/Windows/Fonts/arial.ttf'
    & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i (Join-Path $on 'frame-%04d.png') -frames:v 210 -c:v libx264 -crf 17 -preset medium -pix_fmt yuv420p -an -movflags +faststart -y $single
    if($LASTEXITCODE -ne 0){throw 'Single encode failed.'}
    $crop='crop=800:540:680:420,scale=960:648:flags=neighbor'
    $label="drawtext=fontfile='$font':fontsize=36:fontcolor=white:box=1:boxcolor=black@0.75:boxborderw=10:x=20:y=20"
    $filter="[0:v]split[af][as];[1:v]split[bf][bs];"+
        "[af]${crop},${label}:text='OFF - 1x'[ao];[bf]${crop},${label}:text='ON - 1x'[bo];[ao][bo]vstack=inputs=2[normal];"+
        "[as]trim=start_frame=33:end_frame=75,setpts=4*(PTS-STARTPTS),fps=60,${crop},${label}:text='OFF - 0.25x'[ap];"+
        "[bs]trim=start_frame=33:end_frame=75,setpts=4*(PTS-STARTPTS),fps=60,${crop},${label}:text='ON - 0.25x'[bp];"+
        '[ap][bp]vstack=inputs=2[slow];[normal][slow]concat=n=2:v=1:a=0[out]'
    & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i (Join-Path $off 'frame-%04d.png') -framerate 60 -i (Join-Path $on 'frame-%04d.png') -filter_complex $filter -map '[out]' -c:v libx264 -crf 17 -preset medium -pix_fmt yuv420p -an -movflags +faststart -y $review
    if($LASTEXITCODE -ne 0){throw 'Comparison encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i (Join-Path $on 'frame-%04d.png') -filter_complex 'trim=start_frame=33:end_frame=75,setpts=4*(PTS-STARTPTS),fps=20,crop=800:540:680:420,scale=800:540:flags=neighbor,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3' -an -loop 0 -y $gif
    if($LASTEXITCODE -ne 0){throw 'GIF encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -i (Join-Path $off 'frame-0049.png') -i (Join-Path $on 'frame-0049.png') -filter_complex "[0:v]${crop},${label}:text='OFF'[a];[1:v]${crop},${label}:text='ON'[b];[a][b]vstack=inputs=2[out]" -map '[out]' -frames:v 1 -y $still
    if($LASTEXITCODE -ne 0){throw 'Still encode failed.'}
    foreach($file in @($single,$review,$gif,$still)){Get-Item -LiteralPath $file | Select-Object FullName,Length}
} finally {Pop-Location}
