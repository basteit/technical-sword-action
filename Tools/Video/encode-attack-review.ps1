param([string]$FfmpegPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $root
try {
    if (-not $FfmpegPath) {
        $FfmpegPath = (Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName
    }
    if (-not $FfmpegPath) { throw 'Provide -FfmpegPath.' }
    $takes = @(
        @{ key='snappy-whiff-right'; label='NEW - NO TARGET  |  hits 9F / 7F' },
        @{ key='snappy-hit-right'; label='NEW - HIT  |  hits 9F / 7F' },
        @{ key='snappy-hit-left'; label='NEW - FACING LEFT  |  hits 9F / 7F' },
        @{ key='previous-fast-whiff-right'; label='PREVIOUS - NO TARGET  |  hits 12F / 9F' },
        @{ key='previous-fast-hit-right'; label='PREVIOUS - HIT  |  hits 12F / 9F' }
    )
    $argsList = @('-hide_banner','-loglevel','error')
    $filters = @(); $metadata = @()
    for ($i=0; $i -lt $takes.Count; $i++) {
        $folder = '.uloop/outputs/video/prototype-attack-review-20261026/' + $takes[$i].key
        $report = Get-Content -Raw -LiteralPath "$folder/events.json" | ConvertFrom-Json
        if ($report.frameCount -ne 180 -or $report.fps -ne 60 -or $report.duration -ne 3) { throw "Invalid capture: $folder" }
        for ($frame=0; $frame -lt 180; $frame++) {
            if (-not (Test-Path -LiteralPath (Join-Path $folder ('frame-{0:D4}.png' -f $frame)))) { throw "Missing frame $frame in $folder" }
        }
        $metadata += $report
        $argsList += @('-framerate','60','-i',"$folder/frame-%04d.png")
        $label = $takes[$i].label
        # Crop the recorded pixels, not the gameplay camera; 3x source pixels become exact 4x pixels.
        $filters += "[${i}:v]crop=960:540:600:360,scale=1280:720:flags=neighbor,drawbox=x=0:y=0:w=iw:h=72:color=black@0.82:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='$label':fontsize=32:fontcolor=white:x=24:y=18,drawbox=x=0:y=670:w=iw:h=50:color=black@0.82:t=fill,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='1x speed - 60 fps - no audio   |   HIT uses a stationary high-HP review target':fontsize=21:fontcolor=white:x=24:y=684,setsar=1[v$i]"
    }
    $filters += '[v0][v1][v2][v3][v4]concat=n=5:v=1:a=0[out]'
    $output = 'docs/video/previews/prototype-attack-1-2-review-20261026.mp4'
    if (Test-Path -LiteralPath $output) { throw "Preserve existing video: $output" }
    $argsList += @('-filter_complex',($filters -join ';'),'-map','[out]','-frames:v','900','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-an','-movflags','+faststart',$output)
    & $FfmpegPath @argsList
    if ($LASTEXITCODE -ne 0) { throw 'Encoding failed.' }
    $metadata | ConvertTo-Json -Depth 6 | Out-File -LiteralPath '.uloop/outputs/video/prototype-attack-review-20261026/combined-report.json' -Encoding utf8
    Write-Output "$output : 900 frames / 15 seconds / 1280x720 / 60fps / no audio"
} finally { Pop-Location }
