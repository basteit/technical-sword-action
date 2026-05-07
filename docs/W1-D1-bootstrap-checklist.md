# W1-D1 Bootstrap Checklist

Date: 2026-05-07

## Configuration Snapshot
- Render pipeline: URP 2D assets are present under `Assets/Settings`.
- Build scene: `Assets/Scenes/SampleScene.unity` is enabled in build settings.
- Input System actions asset: `Assets/InputSystem_Actions.inputactions` is linked in build settings config objects.
- Input handling: `activeInputHandler: 1` in `ProjectSettings/ProjectSettings.asset`.

## Folder Convention (Assets)
- `Assets/Scenes`
- `Assets/Scripts`
- `Assets/Prefabs`
- `Assets/Art`
- `Assets/Audio`
- `Assets/Animations`
- `Assets/Materials`
- `Assets/UI`
- `Assets/VFX`
- `Assets/Resources`
- `Assets/StreamingAssets`

## Manual Final Check in Unity Editor
1. Open `Edit > Project Settings > Player > Active Input Handling` and confirm Input System is enabled.
2. Open `File > Build Profiles` (or Build Settings) and confirm `Assets/Scenes/SampleScene.unity` is checked.
3. Enter Play Mode and confirm there are no Console errors.
