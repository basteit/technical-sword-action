# Kenney prototype sound effects

Author: Kenney (https://kenney.nl/). Acquired on 2026-11-07 (Japan time).

These selected original Ogg files are from the official CC0 packs below. They are not the old project recordings. Each pack's original `License.txt` is retained beside its selected sources.

- RPG Audio 1.0: https://kenney.nl/assets/rpg-audio
- Impact Sounds 1.0: https://kenney.nl/assets/impact-sounds
- Sci-Fi Sounds 1.0: https://kenney.nl/assets/sci-fi-sounds
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/

CC0 permits commercial use, adaptation, and redistribution. Attribution is optional; no endorsement is implied. No sounds from OtoLogic or Sound Effect Lab are included.

The current combined manifest at `ThirdParty/PrototypeAudio/manifest.json` records the selected original filenames and SHA-256 values, processed asset hashes, source-layer edits, compatibility replacements, and acquisition date. The metallic refinement also uses SoundBible (CC BY 3.0) and BigSoundBank (CC0); those originals and notices are in `ThirdParty/AdditionalAudio`. The original downloaded Kenney ZIP SHA-256 values were:

| Official pack | SHA-256 |
| --- | --- |
| RPG Audio | `6dbeaf8544da958d8f2adcb4a4a4b76c1ade34a05f8ab9edccd327da7375f38b` |
| Impact Sounds | `029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8` |
| Sci-Fi Sounds | `119340f351a5098ad814f78719438c0da355a9ce8a4c8a3af6a8d48aa3d49e04` |

The playable derivatives live in `Assets/Audio/PrototypeFreeSfx`. Rebuild them with `Tools/Audio/Import-PrototypeSfx.ps1` using the official unpacked packs. Processing: source-layer trim/tempo/reversal/gain as recorded; remove leading silence; 65 Hz high-pass; 3 ms attack/25 ms tail fades; normalize to approximately -6 dBFS; safety limit; 48 kHz mono PCM16. Four compatibility MP3s replace the old recordings while preserving their Unity GUIDs for other scenes. Original project recordings remain only in earlier Git revisions, not the current assets.

Optional distribution credit:

```text
Sound effects: Kenney (kenney.nl), CC0 1.0.
Edited/mixed for this prototype. See ThirdParty/PrototypeAudio/manifest.json.
```
