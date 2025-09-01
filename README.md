# OpenXREyeTracking

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that pipes OpenXR eye tracking extensions into Resonite's InputManager by way of a headless OpenXR session

I don't like working with C#.

## Extension support
This mod currently supports two extensions, `XR_FB_face_tracking2`, and `XR_EXT_eye_gaze_interaction`, in order of priority.
- `XR_FB_face_tracking2` Is implemented, with per-eye gaze and blink.
- `XR_EXT_eye_gaze_interaction` Is theoretically implemented, but I haven't gotten it to work.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place [Silk.NET.Core.dll](https://github.com/headassbtw/ResoniteOpenXREyeTracking/releases/download/1.0.0/Silk.NET.Core.dll) into your `rml_libs` folder
   - You can also extract it from [the nupkg](https://www.nuget.org/api/v2/package/Silk.NET.Core/2.22.0)
3. Place [Silk.NET.OpenXR.dll](https://github.com/headassbtw/ResoniteOpenXREyeTracking/releases/download/1.0.0/Silk.NET.OpenXR.dll) into your `rml_libs` folder
    - You can also extract it from [the nupkg](https://www.nuget.org/api/v2/package/Silk.NET.OpenXR/2.22.0)
4. Place [OpenXREyeTracking.dll](https://github.com/headassbtw/ResoniteOpenXREyeTracking/releases/latest/download/OpenXREyeTracking.dll) into your `rml_mods` folder.
5. Start the game. If you want to verify that the mod is working you can check your Resonite logs and your eyes in a mirror.
