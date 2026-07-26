# Prologue movie

Open `Assets/Single_detailed_truck/example_scene.unity`, then choose
`GMTK > Prologue > Prepare Recorder`. Press **START RECORDING** in the Recorder
window. The prepared Recorder automatically captures the 5-second performance as
1920×1080, 30 FPS, H.264 MP4 and exits Play Mode when it finishes.

`Assets/StreamingAssets/Prologue.mp4`

`RaceFlow` automatically plays that file full-screen during its `Prologue` phase.
If the file is missing or cannot be decoded, the existing text prologue is shown
for the configured fallback duration.

The output path and timing are already configured by the menu command.
Audio capture is enabled, so the collision sound is included in the MP4.

# Ending movie

Open `Assets/Dnk_Dev/HospitalHorrorPack/ending.unity`, then choose
`GMTK > Ending > Record MP4`. The Recorder automatically captures 11 seconds as
1920×1080, 30 FPS, H.264 MP4 with audio and exits Play Mode when it finishes.

`Assets/StreamingAssets/Ending.mp4`

`GMTKRaceState` plays this file full-screen when the authoritative race result is
`RaceFinishType.Win`. Other scene audio is paused during playback, while the
movie audio remains audible. After playback finishes, the last movie frame stays
on screen until the player exits the application.
