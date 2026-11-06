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
    $manifestFolder = 'ThirdParty/PrototypeAudio'
    New-Item -ItemType Directory -Force -Path $out,$archive,$manifestFolder | Out-Null
    $additional = @{
        'SoundBible/Sword_Swing' = @{path='ThirdParty/AdditionalAudio/SoundBible/Sword_Swing.wav'; title='Sword Swing'; author='Mike Koenig'; license='CC-BY-3.0'; url='https://soundbible.com/1176-Sword-Swing.html'; download='https://soundbible.com/grab.php?id=1176&type=wav'; sha256='93a2e6e9e17daea9cbebb54317c3669640c7b07c919a71c6856750cff76a7389'}
        'SoundBible/Swords_Collide' = @{path='ThirdParty/AdditionalAudio/SoundBible/Swords_Collide.wav'; title='Swords Collide'; author='Sound Explorer'; license='CC-BY-3.0'; url='https://soundbible.com/1980-Swords-Collide.html'; download='https://soundbible.com/grab.php?id=1980&type=wav'; sha256='5113abd89a3efabc32c40a1c6868ab1cfb796055a642e5ff3052e0bcf836cd3f'}
        'SoundBible/Swords_Clashing' = @{path='ThirdParty/AdditionalAudio/SoundBible/Swords_Clashing.wav'; title='Swords Clashing'; author='Mike Koenig'; license='CC-BY-3.0'; url='https://soundbible.com/857-Swords-Clashing.html'; download='https://soundbible.com/grab.php?id=857&type=wav'; sha256='ea20c872a4b7bed3cb0db8f44db9e5d6ed93638709cc816a2318efe64ec74b90'}
        'SoundBible/Ninja_Samurai_Sword' = @{path='ThirdParty/AdditionalAudio/SoundBible/Ninja_Samurai_Sword.mp3'; title='Ninja Samurai Sword'; author='Mike Koenig'; license='CC-BY-3.0'; url='https://soundbible.com/1526-Ninja-Samurai-Sword.html'; download='https://soundbible.com/grab.php?id=1526&type=mp3'; sha256='4d13cfb15f59aa60fdb3233499da8a8c11d11e9a3009ab7540932082c8590822'}
        'SoundBible/Electricity' = @{path='ThirdParty/AdditionalAudio/SoundBible/Electricity.wav'; title='Electricity'; author='Mike Koenig'; license='CC-BY-3.0'; url='https://soundbible.com/1107-Electricity.html'; download='https://soundbible.com/grab.php?id=1107&type=wav'; sha256='275893afa155c64dfd5bafd06e7fd4eec6b341a256fb7550fb5c862a75980a90'}
        'SoundBible/Woosh' = @{path='ThirdParty/AdditionalAudio/SoundBible/Woosh.wav'; title='Woosh'; author='Mark DiAngelo'; license='CC-BY-3.0'; url='https://soundbible.com/2068-Woosh.html'; download='https://soundbible.com/grab.php?id=2068&type=wav'; sha256='438915b14b2f394f4404f02cacf970a3b1a08295eff0dee77e98910da031cb30'}
        'SoundBible/Kick' = @{path='ThirdParty/AdditionalAudio/SoundBible/Kick.wav'; title='Kick'; author='Mike Koenig'; license='CC-BY-3.0'; url='https://soundbible.com/996-Kick.html'; download='https://soundbible.com/grab.php?id=996&type=wav'; sha256='da8a074296b72bc208e64279ce102c40f138e63b6d4bd7d82a1e127e9a970ab1'}
        'SoundBible/Realistic_Punch' = @{path='ThirdParty/AdditionalAudio/SoundBible/Realistic_Punch.wav'; title='Realistic Punch'; author='Mark DiAngelo'; license='CC-BY-3.0'; url='https://soundbible.com/2069-Realistic-Punch.html'; download='https://soundbible.com/grab.php?id=2069&type=wav'; sha256='c04bdef72208d0aa3e7c74f8225e9f9e00f4eee8d3ed845a897d3454f3932cd2'}
        'BigSoundBank/0514' = @{path='ThirdParty/AdditionalAudio/BigSoundBank/0514.wav'; title='Footsteps, Shoe on Concrete'; author='Joseph SARDIN'; license='CC0-1.0'; url='https://bigsoundbank.com/footsteps-shoe-on-concrete-s0514.html'; download='https://bigsoundbank.com/UPLOAD/bwf-en/0514.wav'; sha256='e424c7d07c7197f0d9aa5e3570de72d2572c1930984a92254013fa5e8fa76397'}
    }
    $packUrls = [ordered]@{'rpg-audio'='https://kenney.nl/assets/rpg-audio'; 'impact-sounds'='https://kenney.nl/assets/impact-sounds'; 'sci-fi-sounds'='https://kenney.nl/assets/sci-fi-sounds'}
    $recipes = @(
        @{name='FootstepA'; seconds=.19; sources=@('impact-sounds/footstep_concrete_000'); filters=@('volume=1')},
        @{name='FootstepB'; seconds=.19; sources=@('impact-sounds/footstep_concrete_002'); filters=@('volume=1')},
        @{name='Jump'; seconds=.25; sources=@('rpg-audio/cloth3'); filters=@('atempo=1.35')},
        @{name='Land'; seconds=.18; sources=@('BigSoundBank/0514'); filters=@('atrim=start=0.38:end=0.59,asetpts=PTS-STARTPTS,highpass=f=300,lowpass=f=7000,equalizer=f=1500:t=q:w=1:g=2,atempo=1.2')},
        @{name='Dash'; seconds=.27; sources=@('SoundBible/Woosh'); filters=@('atrim=start=0.04:end=0.18,asetpts=PTS-STARTPTS,atempo=0.6,highpass=f=450,lowpass=f=9000,afade=t=in:d=0.02')},
        @{name='SwordA'; seconds=.22; sources=@('SoundBible/Sword_Swing','rpg-audio/knifeSlice'); filters=@('asetrate=55200,aresample=48000,highpass=f=650,equalizer=f=3000:t=q:w=1.2:g=3','atempo=1.4,highpass=f=900,volume=0.25')},
        @{name='SwordB'; seconds=.20; sources=@('SoundBible/Sword_Swing','rpg-audio/knifeSlice2'); filters=@('asetrate=62400,aresample=48000,highpass=f=750,equalizer=f=3400:t=q:w=1.2:g=3','atempo=1.55,highpass=f=1000,volume=0.25')},
        @{name='SwordIai'; seconds=.30; sources=@('SoundBible/Sword_Swing','rpg-audio/drawKnife3'); filters=@('asetrate=52800,aresample=48000,highpass=f=650,equalizer=f=3000:t=q:w=1.2:g=3','atempo=1.5,highpass=f=1800,volume=0.18')},
        @{name='Sheathe'; seconds=.22; sources=@('rpg-audio/metalLatch','rpg-audio/clothBelt2'); filters=@('volume=0.7','atempo=2,volume=0.4')},
        @{name='ParryReady'; seconds=.28; sources=@('impact-sounds/impactBell_heavy_000','impact-sounds/impactGlass_light_002'); filters=@('asetrate=115200,aresample=48000,highpass=f=2200,lowpass=f=11000,volume=0.7','asetrate=76800,aresample=48000,highpass=f=3000,lowpass=f=11000,adelay=25:all=1,volume=0.22')},
        @{name='ParryClang'; seconds=.65; sources=@('SoundBible/Swords_Clashing','SoundBible/Swords_Collide'); filters=@('atrim=start=0.135:end=0.98,asetpts=PTS-STARTPTS,asetrate=60000,aresample=48000,highpass=f=1100,lowpass=f=11000,equalizer=f=4000:t=q:w=1.2:g=3','atrim=start=0.019,asetpts=PTS-STARTPTS,highpass=f=1800,lowpass=f=11000,volume=0.35')},
        @{name='ParryMiss'; seconds=.16; sources=@('rpg-audio/cloth1'); filters=@('atempo=2')},
        @{name='CounterTransfer'; seconds=.29; sources=@('sci-fi-sounds/forceField_002'); filters=@('atrim=0:0.6,areverse,atempo=2')},
        @{name='CounterCharge'; seconds=.18; sources=@('SoundBible/Electricity','sci-fi-sounds/impactMetal_004','sci-fi-sounds/impactMetal_000'); filters=@('atrim=start=0.55:end=0.77,asetpts=PTS-STARTPTS,asetrate=72000,aresample=48000,highpass=f=900,lowpass=f=11000,equalizer=f=5000:t=q:w=1:g=5,afade=t=out:st=0.05:d=0.08','atempo=3,highpass=f=1600,lowpass=f=11000,volume=0.65','atrim=0:0.12,atempo=2,highpass=f=2200,equalizer=f=6500:t=q:w=1:g=5,volume=0.6')},
        @{name='CounterImpact'; seconds=.24; sources=@('SoundBible/Realistic_Punch','impact-sounds/impactPunch_medium_000','impact-sounds/impactPunch_heavy_004'); filters=@('atrim=start=0.08:end=0.25,asetpts=PTS-STARTPTS,asetrate=57600,aresample=48000,highpass=f=500,lowpass=f=7500,equalizer=f=2000:t=q:w=0.8:g=2','atrim=0:0.09,atempo=1.4,highpass=f=1200,lowpass=f=8000,volume=0.5','atrim=0:0.20,asetrate=55200,aresample=48000,highpass=f=450,lowpass=f=7000,volume=0.45')},
        @{name='LightHit'; seconds=.21; sources=@('SoundBible/Woosh','rpg-audio/knifeSlice2'); filters=@('atrim=start=0.04:end=0.18,asetpts=PTS-STARTPTS,highpass=f=500,lowpass=f=7500,equalizer=f=2200:t=q:w=1:g=2','atempo=1.55,highpass=f=650,lowpass=f=8000,volume=0.65')},
        @{name='HeavyHit'; seconds=.28; sources=@('SoundBible/Woosh','rpg-audio/knifeSlice'); filters=@('atrim=start=0.04:end=0.20,asetpts=PTS-STARTPTS,atempo=0.75,highpass=f=450,lowpass=f=7500,equalizer=f=2000:t=q:w=1:g=2','atempo=1.15,highpass=f=550,lowpass=f=8000,volume=0.75')},
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
        if (-not (Test-Path -LiteralPath $license)) { $license = "$archive/$pack/License.txt" }
        if (-not (Test-Path -LiteralPath $license)) { throw "Missing original license: $pack" }
        New-Item -ItemType Directory -Force -Path "$archive/$pack" | Out-Null
        if ([IO.Path]::GetFullPath($license) -ne [IO.Path]::GetFullPath("$archive/$pack/License.txt")) {
            Copy-Item -LiteralPath $license -Destination "$archive/$pack/License.txt"
        }
    }
    $records = foreach ($recipe in $recipes) {
        $arguments = @('-hide_banner','-loglevel','error','-y','-filter_complex_threads','1')
        $chains = @(); $sources = @()
        for ($i=0; $i -lt $recipe.sources.Count; $i++) {
            $parts = $recipe.sources[$i].Split('/')
            if ($additional.ContainsKey($recipe.sources[$i])) {
                $metadata = $additional[$recipe.sources[$i]]
                $raw = $metadata.path
                if (-not (Test-Path -LiteralPath $raw) -or (Get-FileHash -LiteralPath $raw -Algorithm SHA256).Hash.ToLowerInvariant() -ne $metadata.sha256) {
                    throw "Missing or changed licensed original: $raw"
                }
            } else {
                $source = Join-Path $SourceRoot "$($parts[0])/Audio/$($parts[1]).ogg"
                $raw = "$archive/$($parts[0])/$($parts[1]).ogg"
                if (-not (Test-Path -LiteralPath $raw)) {
                    if (-not (Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
                    Copy-Item -LiteralPath $source -Destination $raw
                }
                $metadata = @{title=$parts[1]; author='Kenney'; license='CC0-1.0'; url=$packUrls[$parts[0]]}
            }
            $arguments += @('-i',$raw)
            $chains += "[$($i):a]aformat=sample_rates=48000:channel_layouts=mono,$($recipe.filters[$i]),apad=whole_dur=2[s$i]"
            $sources += [ordered]@{path=$raw; sha256=(Get-FileHash -LiteralPath $raw -Algorithm SHA256).Hash.ToLowerInvariant(); filter=$recipe.filters[$i]; title=$metadata.title; author=$metadata.author; license=$metadata.license; url=$metadata.url; download=$metadata.download}
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
        $license = if ($sources.license -contains 'CC-BY-3.0') { 'CC-BY-3.0' } else { 'CC0-1.0' }
        [ordered]@{name=$recipe.name; asset=$dest; license=$license; seconds=$recipe.seconds; peakTargetDb=-6; gainDb=[double]::Parse($gain,[Globalization.CultureInfo]::InvariantCulture); sha256=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash.ToLowerInvariant(); sources=$sources}
    }
    # Keep compatibility GUIDs: the old recordings are replaced, not layered or left referenced.
    $compatibility = [ordered]@{'HitSound_RayTap'='LightHit';'HitSound_Release'='HeavyHit';'HitSound_RayRelease'='ParryClang';'HitSound_RayStay'='PlayerHit'}
    foreach ($key in $compatibility.Keys) {
        & $Ffmpeg -hide_banner -loglevel error -y -i "$out/$($compatibility[$key]).wav" -c:a libmp3lame -q:a 2 "Assets/Audio/$key.mp3"
        if ($LASTEXITCODE -ne 0) { throw "Compatibility replacement failed: $key" }
    }
    $jump = @($records | Where-Object name -eq 'Jump')[0]
    if ($jump.sha256 -ne '9e923b4a2ffe056a4eb271416676f11c95fb141a7e87f74a26822a260f882c7c') { throw 'The approved jump sound must remain byte-identical.' }
    # Stable key order keeps re-imports byte-identical and makes future recipe changes reviewable.
    $manifest = [ordered]@{licenses=@('CC0-1.0','CC-BY-3.0'); authors=@('Kenney','Mike Koenig','Mark DiAngelo','Sound Explorer','Joseph SARDIN'); acquired='2026-11-07'; credits='Assets/StreamingAssets/AudioCredits.txt'; sampleRate=48000; channels=1; bits=16; preprocessing='Per-layer trim/tempo/pitch/EQ/reverse/delay/tremolo/gain as recorded; remove leading silence below -48 dB; mix; high-pass 65 Hz; fade 3/25 ms; peak-normalize -6 dBFS; safety limit 0.56; PCM16 mono48kHz'; packs=$packUrls; compatibility=$compatibility; clips=@($records)}
    $manifest | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath "$manifestFolder/manifest.json" -Encoding utf8
    Write-Output "Imported $($records.Count) processed clips and replaced four legacy recordings."
} finally { Pop-Location }
