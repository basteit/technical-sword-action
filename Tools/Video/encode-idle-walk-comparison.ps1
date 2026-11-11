param([string]$FfmpegPath)
$ErrorActionPreference='Stop'
$comparisonRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $comparisonRoot
try {
    if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
    if(-not $FfmpegPath){throw 'Provide -FfmpegPath.'}
    foreach($comparisonTake in @('speed-1p5x','speed-2x')){
        & (Join-Path $PSScriptRoot 'encode-idle-walk.ps1') -FfmpegPath $FfmpegPath -Take $comparisonTake
    }
    $comparisonTop=Get-Content -Raw -LiteralPath 'docs/video/previews/studio-idle-walk-speed-1p5x.json' | ConvertFrom-Json
    $comparisonBottom=Get-Content -Raw -LiteralPath 'docs/video/previews/studio-idle-walk-speed-2x.json' | ConvertFrom-Json
    if($comparisonTop.cameraSize -ne $comparisonBottom.cameraSize -or
       $comparisonTop.cameraCenter.x -ne $comparisonBottom.cameraCenter.x -or
       $comparisonTop.cameraCenter.y -ne $comparisonBottom.cameraCenter.y -or
       [Math]::Abs($comparisonTop.actualSpeedMultiplier-1.5) -gt .005 -or
       [Math]::Abs($comparisonBottom.actualSpeedMultiplier-2) -gt .005){throw 'Camera or measured speed comparison mismatch.'}
    $comparisonFont=(Resolve-Path -LiteralPath (Join-Path $env:WINDIR 'Fonts/arial.ttf')).Path.Replace('\','/').Replace(':','\:')
    $comparisonFilter=@"
[0:v]crop=1920:540:0:300,drawbox=x=0:y=0:w=iw:h=58:color=0xf8fafb:t=fill,drawtext=fontfile='$comparisonFont':text='1.5x':x=24:y=10:fontsize=36:fontcolor=0x202030,drawtext=fontfile='$comparisonFont':text='WALK SPEED / ANIMATION 1.0x':x=168:y=23:fontsize=20:fontcolor=0x505060[top];
[1:v]crop=1920:540:0:300,drawbox=x=0:y=0:w=iw:h=58:color=0xf8fafb:t=fill,drawtext=fontfile='$comparisonFont':text='2.0x':x=24:y=10:fontsize=36:fontcolor=0x202030,drawtext=fontfile='$comparisonFont':text='WALK SPEED / ANIMATION 1.0x':x=168:y=23:fontsize=20:fontcolor=0x505060[bottom];
[top][bottom]vstack=inputs=2[v]
"@
    $comparisonOutput='docs/video/previews/studio-idle-walk-speed-comparison.mp4'
    & $FfmpegPath -hide_banner -loglevel error -i 'docs/video/previews/studio-idle-walk-speed-1p5x.mp4' -i 'docs/video/previews/studio-idle-walk-speed-2x.mp4' -filter_complex $comparisonFilter -map '[v]' -frames:v 360 -c:v libx264 -crf 16 -preset slow -pix_fmt yuv420p -an -movflags +faststart -y $comparisonOutput
    if($LASTEXITCODE -ne 0){throw 'Comparison encoding failed.'}
    [ordered]@{
        frameCount=360;fps=60;duration=6;width=1920;height=1080;audio=$false
        top='studio-idle-walk-speed-1p5x.mp4';bottom='studio-idle-walk-speed-2x.mp4'
        sourceCrop=@(0,300,1920,540);resized=$false;animationSpeedMultiplier=1
        baseline='studio-idle-walk-game-scale.mp4'
        actualSpeedMultipliers=@($comparisonTop.actualSpeedMultiplier,$comparisonBottom.actualSpeedMultiplier)
    } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 -LiteralPath ($comparisonOutput -replace '\.mp4$','.json')
    Write-Output "$comparisonOutput : top 1.5x / bottom 2.0x / 6 seconds / no audio"
} finally { Pop-Location }
