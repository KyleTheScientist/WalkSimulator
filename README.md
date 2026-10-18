# WalkSimulator

Play Gorilla Tag with a keyboard and mouse. Walk, run, climb walls, swim, fly, and ride ziplines, ropes, gliders and broomsticks, all with procedurally animated gorilla arms, no VR headset required.

WalkSimulator only works in **modded lobbies**. In a public lobby it turns itself off.

## Requirements

- Gorilla Tag (Steam)
- [BepInEx 5.4.23.x](https://github.com/BepInEx/BepInEx/releases) (x64). Older BepInEx 5.4.22 crashes on launch with the current game.
- A current [Utilla](https://github.com/sirkingbinx/Utilla) build for modded lobbies. Utilla 1.6.27 and older no longer load on the current game.

## Installation

1. Install BepInEx into your Gorilla Tag folder and launch the game once.
2. Install Utilla into `BepInEx/plugins`.
3. Extract the WalkSimulator zip into your Gorilla Tag folder so the DLL ends up at `BepInEx/plugins/WalkSimulator/WalkSimulator.dll`.
4. Launch the game and join a modded lobby (or play offline).

## Controls

Press **H** at any time to show or hide the controls for your current mode on screen.

### VR and desktop

Without a VR headset, you start in walk mode. With a headset connected, you start in **VR mode**, where the mod stays out of the way and you play normally in VR. Press **V** at any time to switch between VR and desktop controls, or hold **Tab** to pick a desktop mode.

### Everywhere

| Key | Action |
| --- | --- |
| Tab (hold) | Mode menu: point the mouse at a mode and release |
| 1 – 6 | Emotes: wave, point, thumbs up, thumbs down, shrug, dance |
| C | Switch between first and third person |
| Esc | Free / lock the mouse cursor |
| E | Use a computer or terminal (when standing in front of one) |
| V | Switch between VR and desktop controls |
| H | Show / hide controls |

### Walk

| Key | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump |
| Space while holding a direction into a wall | Wall jump (repeat to climb) |
| Ctrl | Crouch |
| Hold left / right click | Grab a zipline, rope, glider or broomstick |

Walking onto icy surfaces makes you slide. Walking into deep water switches to swimming automatically.

### Swim

| Key | Action |
| --- | --- |
| WASD | Swim toward where you're looking |
| Space / Ctrl | Up / down |
| Shift | Swim faster |
| Space at the surface | Leap out of the water |

Flowing water carries you along.

### Zipline, rope and broomstick

| Key | Action |
| --- | --- |
| WASD | Pump the swing (ropes only) |
| Space | Jump off |
| Release the mouse button | Let go and keep your momentum |

### Glider

| Key | Action |
| --- | --- |
| A / D | Roll |
| W / S | Dive / climb |
| Mouse | Look and turn |
| WASD, Shift, Space (on the ground) | Walk, sprint and jump while carrying it |
| Release the mouse button | Let go |

### Fly

| Key | Action |
| --- | --- |
| WASD | Move |
| Space / Ctrl | Up / down |
| Shift | Faster |
| Scroll | Flight speed |
| N | Noclip on / off |

### Pose

| Key | Action |
| --- | --- |
| Mouse | Move the hand |
| Scroll, Up / Down arrows | Hand forward / back |
| Hold R + mouse | Rotate the hand (scroll rolls it) |
| Q | Switch hands |
| Left / right click | Trigger / grip |
| [ / ] | Thumb buttons |

### Interact

| Key | Action |
| --- | --- |
| Left / right click | Press the button under the crosshair with your left / right hand |
| Hold click + move the mouse up / down | Drag a slider |

### Emote

Move or press the same number again to stop.

### Computer

| Key | Action |
| --- | --- |
| Letters, 0 – 9 | Type |
| F1 / F2 / F3 | Option buttons |
| Enter | Enter |
| Backspace | Delete |
| Up / Down | Change screen |
| Esc | Leave the computer |

## Configuration

Settings are in `BepInEx/config/com.kylethescientist.gorillatag.walksimulator.cfg`, created after the first launch:

| Setting | Default | Description |
| --- | --- | --- |
| Interface › Show Controls | true | Show the controls overlay (H in game) |

## Building

1. Set `GamePath` in `Directory.Build.props` to your Gorilla Tag install. BepInEx and Utilla need to be installed there, since the project references them.
2. Run `dotnet build`. The DLL is copied into your game's `BepInEx/plugins/WalkSimulator` folder automatically. Close the game first, or the copy fails because the DLL is in use.
3. To make a release, add a `## <version>` section to `CHANGELOG.md`, set the version in `PluginInfo.cs`, and run `MakeRelease.ps1`. It produces `WalkSimulator-v<version>.zip` and writes that version's changelog section to `Release.md`, ready for `gh release create v<version> WalkSimulator-v<version>.zip --notes-file Release.md`.
