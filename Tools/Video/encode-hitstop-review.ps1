param([string]$FfmpegPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if (-not $FfmpegPath) { $FfmpegPath = (Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName }
    if (-not $FfmpegPath) { throw 'Provide -FfmpegPath.' }
    function Run-Encoder([object[]]$Arguments) {
        & $FfmpegPath @Arguments
        if ($LASTEXITCODE -ne 0) { throw 'FFmpeg failed.' }
    }
    $captureRoot = '.uloop/outputs/video/prototype-attack-review-hitstop8-20261026'
    $output = 'docs/video/previews/prototype-attack-hitstop8-review-20261026.mp4'
    if (Test-Path -LiteralPath $output) { throw "Preserve existing output: $output" }
    $takes = @(
        @{key='previous-fast-hit-right-stop8'; label='TRIAL - 8F HITSTOP  |  hits 12F / 9F'},
        @{key='previous-fast-hit-right-stop3'; label='BASE - 3F HITSTOP  |  hits 12F / 9F'},
        @{key='previous-fast-hit-left-stop8'; label='TRIAL - 8F HITSTOP  |  FACING LEFT'},
        @{key='previous-fast-whiff-right-stop8'; label='TRIAL - NO TARGET  |  no hitstop on misses'}
    )
    $metadata = @(); $clips = @()
    for ($i=0; $i -lt $takes.Count; $i++) {
        $folder = Join-Path $captureRoot $takes[$i].key
        $report = Get-Content -Raw -LiteralPath (Join-Path $folder 'events.json') | ConvertFrom-Json
        if ($report.frameCount -ne 180 -or $report.fps -ne 60 -or $report.attack1 -ne 12 -or $report.attack2 -ne 9) { throw "Unexpected attack timing: $folder" }
        for ($frame=0; $frame -lt 180; $frame++) {
            if (-not (Test-Path -LiteralPath (Join-Path $folder ('frame-{0:D4}.png' -f $frame)))) { throw "Missing frame $frame in $folder" }
        }
        $metadata += $report
        $hitMarks = @($report.events | Where-Object kind -eq 'Hit' | Sort-Object frame)
        if ($report.target -and ($hitMarks.Count -ne 4 -or $report.targetHp -ne 960)) { throw 'Unexpected hit count or damage.' }
        $clip = Join-Path $captureRoot "clip-$i.mp4"; $clips += $clip
        $arguments = @('-hide_banner','-loglevel','error','-framerate','60','-i',"$folder/frame-%04d.png",'-i',$report.hitAudioAsset,'-f','lavfi','-i','anullsrc=r=48000:cl=stereo')
        $filters = @()
        $label = $takes[$i].label
        $filters += "[0:v]crop=960:540:600:360,scale=1280:720:flags=neighbor,drawbox=x=0:y=0:w=iw:h=72:color=black@0.82:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='$label':fontsize=32:fontcolor=white:x=24:y=18,drawbox=x=0:y=670:w=iw:h=50:color=black@0.82:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='1x speed - 60 fps - existing hit SFX  |  Stationary review target':fontsize=22:fontcolor=white:x=24:y=684,setsar=1[v]"
        if ($hitMarks.Count -gt 0) {
            # Rebuild the existing 2D hit voice from the recorded hit frames, not a new/heavier sound.
            # Trim each voice at the next contact, matching CombatFeedback2D.sound.Stop()/Play().
            $frequency = [int]$report.hitAudioFrequency
            $rate = ([double]$frequency * [double]$report.hitAudioPitch).ToString('0.######',[Globalization.CultureInfo]::InvariantCulture)
            $gain = ([double]$report.hitAudioGain).ToString('0.######',[Globalization.CultureInfo]::InvariantCulture)
            $splitNames = ((0..($hitMarks.Count-1) | ForEach-Object { "[s$_]" }) -join '')
            $filters += "[1:a]aresample=$frequency,asetrate=$rate,aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,volume=$gain,asplit=$($hitMarks.Count)$splitNames"
            $delayedNames = ''
            for ($hit=0; $hit -lt $hitMarks.Count; $hit++) {
                $next = if ($hit+1 -lt $hitMarks.Count) { [int]$hitMarks[$hit+1].frame } else { 180 }
                $length = (($next - [int]$hitMarks[$hit].frame)/60.0).ToString('0.########',[Globalization.CultureInfo]::InvariantCulture)
                $delay = [int]$hitMarks[$hit].frame * 800
                $filters += "[s$hit]atrim=duration=$length,asetpts=PTS-STARTPTS,adelay=delays=${delay}S:all=1[d$hit]"
                $delayedNames += "[d$hit]"
            }
            $filters += "[2:a]${delayedNames}amix=inputs=$($hitMarks.Count+1):duration=first:normalize=0,atrim=duration=3[a]"
        } else { $filters += '[2:a]atrim=duration=3[a]' }
        $arguments += @('-filter_complex',($filters -join ';'),'-map','[v]','-map','[a]','-t','3','-frames:v','180','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-ar','48000','-movflags','+faststart','-y',$clip)
        Run-Encoder $arguments
    }
    $comparison = Join-Path $captureRoot 'comparison.mp4'
    $comparisonFilters = @(
        "[0:v]crop=640:720:288:0,drawbox=x=0:y=0:w=iw:h=72:color=black:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='8F - TRIAL':fontsize=30:fontcolor=white:x=24:y=18,drawbox=x=0:y=670:w=iw:h=50:color=black:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='Same speed - audio from 8F':fontsize=22:fontcolor=white:x=24:y=684[left]",
        "[1:v]crop=640:720:288:0,drawbox=x=0:y=0:w=iw:h=72:color=black:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='3F - BASE':fontsize=30:fontcolor=white:x=24:y=18,drawbox=x=0:y=670:w=iw:h=50:color=black:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='Same speed - audio from 8F':fontsize=22:fontcolor=white:x=24:y=684[right]",
        '[left][right]hstack=inputs=2[v]'
    ) -join ';'
    Run-Encoder @('-hide_banner','-loglevel','error','-i',$clips[0],'-i',$clips[1],'-filter_complex',$comparisonFilters,'-map','[v]','-map','0:a','-t','3','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-c:a','copy','-movflags','+faststart','-y',$comparison)
    $combined = Join-Path $captureRoot 'finished.mp4'
    $arguments = @('-hide_banner','-loglevel','error')
    foreach ($clip in @($clips[0],$clips[1],$comparison,$clips[2],$clips[3])) { $arguments += @('-i',$clip) }
    $concatFilters = @()
    for ($i=0; $i -lt 5; $i++) {
        # Remove AAC packet padding at section boundaries; retain exactly 180 video frames / 144000 audio samples.
        $concatFilters += "[${i}:v]trim=start_frame=0:end_frame=180,setpts=N/(60*TB)[v$i]"
        $concatFilters += "[${i}:a]apad,atrim=end_sample=144000,asetpts=N/SR/TB[a$i]"
    }
    $concatFilters += '[v0][a0][v1][a1][v2][a2][v3][a3][v4][a4]concat=n=5:v=1:a=1[v][a]'
    $arguments += @('-filter_complex',($concatFilters -join ';'),'-map','[v]','-map','[a]','-t','15','-frames:v','900','-r','60','-fps_mode','cfr','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-ar','48000','-movflags','+faststart','-y',$combined)
    Run-Encoder $arguments
    Run-Encoder @('-v','error','-i',$combined,'-f','null','-')
    Copy-Item -LiteralPath $combined -Destination $output
    $metadata | ConvertTo-Json -Depth 6 | Out-File -LiteralPath (Join-Path $captureRoot 'combined-report.json') -Encoding utf8
    Write-Output "$output : 900 frames / 15 seconds / 1280x720 / 60fps / existing hit SFX rebuilt from recorded frames"
} finally { Pop-Location }
