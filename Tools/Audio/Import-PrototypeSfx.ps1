param(
    [string]$SourceRoot = '.uloop/outputs/audio-source',
    [string]$Ffmpeg = '.uloop/outputs/python-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $root
try {
    if (-not (Test-Path -LiteralPath $Ffmpeg -PathType Leaf)) { throw 'FFmpeg is required.' }
    $out = 'Assets/Audio/PrototypeFreeSfx'
    $archive = 'ThirdParty/KenneyAudio'
    New-Item -ItemType Directory -Force -Path $out,$archive | Out-Null
    $recipes = @(
        @{name='FootstepA'; seconds=.19; sources=@('impact-sounds/footstep_concrete_000'); filters=@('volume=1')},
        @{name='FootstepB'; seconds=.19; sources=@('impact-sounds/footstep_concrete_002'); filters=@('volume=1')},
        @{name='Jump'; seconds=.25; sources=@('rpg-audio/cloth3'); filters=@('atempo=1.35')},
        @{name='Land'; seconds=.27; sources=@('impact-sounds/impactSoft_heavy_001'); filters=@('volume=1')},
        @{name='Dash'; seconds=.27; sources=@('rpg-audio/knifeSlice2','rpg-audio/cloth2'); filters=@('atempo=1.8','atempo=1.5,volume=0.4')},
        @{name='SwordA'; seconds=.30; sources=@('rpg-audio/knifeSlice'); filters=@('atempo=1.6')},
        @{name='SwordB'; seconds=.27; sources=@('rpg-audio/knifeSlice2'); filters=@('atempo=1.75')},
        @{name='SwordIai'; seconds=.38; sources=@('rpg-audio/drawKnife3','rpg-audio/knifeSlice2'); filters=@('atempo=1.5,volume=0.65','atempo=1.4')},
        @{name='Sheathe'; seconds=.22; sources=@('rpg-audio/metalLatch','rpg-audio/clothBelt2'); filters=@('volume=0.7','atempo=2,volume=0.4')},
        @{name='ParryReady'; seconds=.20; sources=@('rpg-audio/drawKnife1'); filters=@('atempo=1.8')},
        @{name='ParryClang'; seconds=.65; sources=@('impact-sounds/impactMetal_heavy_001','impact-sounds/impactPlate_medium_000'); filters=@('volume=1','volume=0.32')},
        @{name='ParryMiss'; seconds=.16; sources=@('rpg-audio/cloth1'); filters=@('atempo=2')},
        @{name='CounterTransfer'; seconds=.29; sources=@('sci-fi-sounds/forceField_002'); filters=@('atrim=0:0.6,areverse,atempo=2')},
        @{name='CounterCharge'; seconds=.90; sources=@('sci-fi-sounds/forceField_000'); filters=@('volume=1')},
        @{name='CounterImpact'; seconds=.65; sources=@('impact-sounds/impactPunch_heavy_000','sci-fi-sounds/lowFrequency_explosion_000','sci-fi-sounds/forceField_001'); filters=@('volume=1','atrim=0:0.45,volume=0.3','atrim=0:0.30,volume=0.2')},
        @{name='LightHit'; seconds=.29; sources=@('rpg-audio/chop','impact-sounds/impactPunch_medium_000'); filters=@('atempo=1.5,volume=0.55','volume=1')},
        @{name='HeavyHit'; seconds=.42; sources=@('rpg-audio/chop','impact-sounds/impactPunch_heavy_002'); filters=@('volume=0.7','volume=1')},
        @{name='PlayerHit'; seconds=.34; sources=@('impact-sounds/impactPunch_medium_002'); filters=@('volume=1')},
        @{name='Fall'; seconds=.36; sources=@('rpg-audio/dropLeather','impact-sounds/impactSoft_heavy_000'); filters=@('atempo=1.4','volume=0.6')},
        @{name='EnemyWindup'; seconds=.24; sources=@('rpg-audio/cloth2'); filters=@('atempo=1.6')},
        @{name='EnemyAttack'; seconds=.29; sources=@('rpg-audio/chop'); filters=@('atempo=1.5')},
        @{name='Danger'; seconds=.30; sources=@('sci-fi-sounds/forceField_004'); filters=@('atempo=1.5')},
        @{name='Heal'; seconds=.42; sources=@('sci-fi-sounds/forceField_003'); filters=@('atempo=1.25')},
        @{name='Interact'; seconds=.14; sources=@('rpg-audio/metalClick'); filters=@('volume=1')}
    )
    $packs = @('rpg-audio','impact-sounds','sci-fi-sounds')
    foreach ($pack in $packs) {
        $license = Join-Path $SourceRoot "$pack/License.txt"
        if (-not (Test-Path -LiteralPath $license)) { throw "Missing original license: $pack" }
        New-Item -ItemType Directory -Force -Path "$archive/$pack" | Out-Null
        Copy-Item -LiteralPath $license -Destination "$archive/$pack/License.txt"
    }
    $records = foreach ($recipe in $recipes) {
        $arguments = @('-hide_banner','-loglevel','error','-y','-filter_complex_threads','1')
        $chains = @(); $sources = @()
        for ($i=0; $i -lt $recipe.sources.Count; $i++) {
            $parts = $recipe.sources[$i].Split('/')
            $source = Join-Path $SourceRoot "$($parts[0])/Audio/$($parts[1]).ogg"
            $raw = "$archive/$($parts[0])/$($parts[1]).ogg"
            if (-not (Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
            Copy-Item -LiteralPath $source -Destination $raw
            $arguments += @('-i',$raw)
            $chains += "[$($i):a]aformat=sample_rates=48000:channel_layouts=mono,$($recipe.filters[$i]),apad=whole_dur=2[s$i]"
            $sources += @{path=$raw; sha256=(Get-FileHash -LiteralPath $raw -Algorithm SHA256).Hash.ToLowerInvariant(); filter=$recipe.filters[$i]}
        }
        $labels = (0..($recipe.sources.Count-1) | ForEach-Object { "[s$_]" }) -join ''
        $seconds = $recipe.seconds.ToString('0.000',[Globalization.CultureInfo]::InvariantCulture)
        $fade = ($recipe.seconds-.025).ToString('0.000',[Globalization.CultureInfo]::InvariantCulture)
        $mix = if ($recipe.sources.Count -gt 1) { "amix=inputs=$($recipe.sources.Count):normalize=0," } else { '' }
        $chains += "${labels}${mix}silenceremove=start_periods=1:start_duration=0:start_threshold=-48dB,highpass=f=65,apad,atrim=duration=$seconds,afade=t=in:d=0.003,afade=t=out:st=${fade}:d=0.025[mix]"
        $graph = $chains -join ';'
        # ffmpeg suppresses measurements at error level.
        $analysisArgs = $arguments.Clone(); $analysisArgs[2]='info'
        # volumedetect converts to PCM16. Attenuate first so a layered transient is not clipped by the meter.
        $analysis = & $Ffmpeg @analysisArgs -filter_complex "$graph;[mix]volume=0.125,volumedetect[measure]" -map '[measure]' -f null NUL 2>&1 | Out-String
        $peakMatch = [regex]::Match($analysis,'max_volume: (-?[\d.]+) dB')
        if (-not $peakMatch.Success) { throw "Could not measure $($recipe.name): $analysis" }
        $peak = [double]::Parse($peakMatch.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture) + 18.06179974
        $gain = (-6-$peak).ToString('0.00',[Globalization.CultureInfo]::InvariantCulture)
        $dest = "$out/$($recipe.name).wav"
        & $Ffmpeg @arguments -filter_complex "$graph;[mix]volume=${gain}dB,alimiter=limit=0.56:level=false[out]" -map '[out]' -ar 48000 -ac 1 -c:a pcm_s16le $dest
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $dest" }
        @{name=$recipe.name; asset=$dest; seconds=$recipe.seconds; peakTargetDb=-6; gainDb=[double]::Parse($gain,[Globalization.CultureInfo]::InvariantCulture); sha256=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash.ToLowerInvariant(); sources=$sources}
    }
    # Keep compatibility GUIDs: the old recordings are replaced, not layered or left referenced.
    $compatibility = @{'HitSound_RayTap'='LightHit';'HitSound_Release'='HeavyHit';'HitSound_RayRelease'='ParryClang';'HitSound_RayStay'='PlayerHit'}
    foreach ($key in $compatibility.Keys) {
        & $Ffmpeg -hide_banner -loglevel error -y -i "$out/$($compatibility[$key]).wav" -c:a libmp3lame -q:a 2 "Assets/Audio/$key.mp3"
        if ($LASTEXITCODE -ne 0) { throw "Compatibility replacement failed: $key" }
    }
    $manifest = @{license='CC0-1.0'; author='Kenney'; acquired='2026-11-07'; sampleRate=48000; channels=1; bits=16; preprocessing='Remove leading silence below -48 dB; per-layer tempo/trim/reverse/gain; mix; high-pass 65 Hz; fade 3/25 ms; peak-normalize -6 dBFS; safety limit 0.56; PCM16 mono48kHz'; packs=@{ 'rpg-audio'='https://kenney.nl/assets/rpg-audio'; 'impact-sounds'='https://kenney.nl/assets/impact-sounds'; 'sci-fi-sounds'='https://kenney.nl/assets/sci-fi-sounds' }; compatibility=$compatibility; clips=@($records)}
    $manifest | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath "$archive/manifest.json" -Encoding utf8
    Write-Output "Imported $($records.Count) processed clips and replaced four legacy recordings."
} finally { Pop-Location }
