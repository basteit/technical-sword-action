param([string]$Revision='20261102-v2',[string]$FfmpegPath)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if($Revision -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid revision.'}
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    $off=".uloop/outputs/video/parry-shockwave-$Revision/strength-000"
    $on=".uloop/outputs/video/parry-shockwave-$Revision/strength-065"
    foreach($dir in @($off,$on)){
        $event=Get-Content -LiteralPath (Join-Path $dir 'events.json') -Raw | ConvertFrom-Json
        if($event.fps -ne 60 -or $event.frameCount -ne 210){throw 'Expected original 60fps prototype capture.'}
        for($i=40;$i -le 60;$i++){
            if(-not(Test-Path -LiteralPath (Join-Path $dir ('frame-{0:D4}.png' -f $i)))){throw "Missing frame $i in $dir"}
        }
    }
    $comparison='docs/video/previews/parry-impact-slow-comparison-20261102.mp4'
    $single='docs/video/previews/parry-impact-slow-on-20261102.mp4'
    $gif='docs/video/previews/parry-impact-slow-20261102.gif'
    foreach($file in @($comparison,$single,$gif)){if(Test-Path -LiteralPath $file){throw "Preserve existing preview: $file"}}
    $font='C\:/Windows/Fonts/arial.ttf'
    $label="drawtext=fontfile='$font':fontsize=28:fontcolor=white:box=1:boxcolor=black@0.75:boxborderw=7:x=14"
    $speed="${label}:y=14:text='OFF - 0.1x',${label}:y=500:text='ON - 0.1x'"
    $hold="${label}:y=14:text='OFF - DETAIL HOLD',${label}:y=500:text='ON - DETAIL HOLD'"
    # Read the original 60fps images at 6fps: exact frame repeats, no motion interpolation.
    # Frames 40..48: 1.5s, frame 49 hold: 1.5s, frames 49..60: 2s.
    $crop='trim=end_frame=21,setpts=PTS-STARTPTS,crop=800:540:680:420,scale=720:486:flags=neighbor'
    $filter="[0:v]${crop}[a];[1:v]${crop}[b];[a][b]vstack=inputs=2,split=3[pre][still][post];"+
        "[pre]trim=end_frame=9,setpts=PTS-STARTPTS,fps=30,tpad=stop_mode=clone:stop_duration=1,trim=duration=1.5,${speed}[p];"+
        "[still]trim=start_frame=9:end_frame=10,setpts=PTS-STARTPTS,fps=30,tpad=stop_mode=clone:stop_duration=1.5,trim=duration=1.5,${hold}[h];"+
        "[post]trim=start_frame=9:end_frame=21,setpts=PTS-STARTPTS,fps=30,tpad=stop_mode=clone:stop_duration=1,trim=duration=2,${speed}[q];"+
        '[p][h][q]concat=n=3:v=1:a=0[out]'
    & $FfmpegPath -hide_banner -loglevel error -n -framerate 6 -start_number 40 -i (Join-Path $off 'frame-%04d.png') -framerate 6 -start_number 40 -i (Join-Path $on 'frame-%04d.png') -filter_complex $filter -map '[out]' -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $comparison
    if($LASTEXITCODE -ne 0){throw 'Slow comparison encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -i $comparison -vf 'crop=720:486:0:486' -c:v libx264 -profile:v baseline -level:v 3.1 -crf 18 -preset medium -pix_fmt yuv420p -an -movflags +faststart $single
    if($LASTEXITCODE -ne 0){throw 'Slow close-up encode failed.'}
    & $FfmpegPath -hide_banner -loglevel error -n -i $single -filter_complex 'fps=20,scale=640:432:flags=neighbor,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3' -an -loop 0 $gif
    if($LASTEXITCODE -ne 0){throw 'Slow GIF encode failed.'}
    foreach($file in @($comparison,$single,$gif)){Get-Item -LiteralPath $file | Select-Object FullName,Length}
} finally {Pop-Location}
