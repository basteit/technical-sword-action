param([string]$FfmpegPath)
$ErrorActionPreference='Stop'
$videoShowcaseRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $videoShowcaseRoot
try {
 if(-not $FfmpegPath){$FfmpegPath=(Get-ChildItem -LiteralPath '.uloop/outputs/python-deps/imageio_ffmpeg/binaries' -Filter 'ffmpeg-win*.exe' | Select-Object -First 1).FullName}
 if(-not $FfmpegPath){throw 'Provide -FfmpegPath.'}
 foreach($videoEnding in @(2,4)){
  $videoSource=".uloop/outputs/video/defense-cancel-${videoEnding}stage-hybrid"
  $videoMetadata=Get-Content -Raw -LiteralPath "$videoSource/events.json" | ConvertFrom-Json
  if($videoMetadata.takeKind -ne 'defense-cancel' -or $videoMetadata.fps -ne 60 -or $videoMetadata.duration -lt 3 -or $videoMetadata.duration -gt 6){throw 'Invalid showcase metadata.'}
  for($videoFrame=0;$videoFrame -lt $videoMetadata.frameCount;$videoFrame++){
   if(-not(Test-Path -LiteralPath (Join-Path $videoSource ('frame-{0:D4}.png' -f $videoFrame)))){throw "Missing frame $videoFrame"}
  }
  $videoOut="docs/video/previews/combat-defense-cancel-${videoEnding}stage-rough-hybrid.mp4"
  & $FfmpegPath -hide_banner -loglevel error -framerate 60 -i "$videoSource/frame-%04d.png" -frames:v $videoMetadata.frameCount -c:v libx264 -crf 16 -preset slow -pix_fmt yuv420p -an -movflags +faststart -y $videoOut
  if($LASTEXITCODE -ne 0){throw "Encoding failed: $videoEnding"}
  Copy-Item -LiteralPath "$videoSource/events.json" -Destination ($videoOut -replace '\.mp4$','.json')
  Write-Output "$videoOut : $($videoMetadata.frameCount) frames / $($videoMetadata.duration) seconds / no audio"
 }
}finally{Pop-Location}
