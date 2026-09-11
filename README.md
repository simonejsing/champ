# Champ

A 2D top-down champion walking a castle keep at night, rendered in Unity. The simulation lives
apart from the view, in `Champ.Sim` -- a plain C# library that knows nothing about graphics and
reaches Unity as a local package.

**Controls:** WASD or arrow keys.

The hero starts just inside the south gate. Walk the courtyard, side chambers, and the north room
-- or head out the gate and down the path into the meadow around the keep. The camera eases along
behind you and stops at the edge of the map.

## Layout

| Path | Role |
| --- | --- |
| `src/Champ.Sim` | Movement, AABB walls, ground surfaces, follow camera, generated hero pixels. No graphics types. |
| `engines/Champ.Unity` | The Unity project: `CastleView` builds the keep at runtime from the sim. |
| `scripts/unity.ps1` | Builds the standalone player without a hard-coded editor path. |

## Running it

Needs [Unity 2022.3 LTS](https://unity.com/download) or newer (Hub will upgrade the project if you
open it in Unity 6). The Editor runs on Windows, macOS and Linux.

1. Add `engines/Champ.Unity` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play. `CastleView` builds the keep at runtime and pulls sim code from the local
   `com.champ.sim` package.

### Standalone build

`Assets/Editor/BuildScript.cs` builds a Windows player from `Main.unity`, either from the
Editor menu (**Champ ▸ Build Standalone Windows**, drops the exe at `Build/Windows/Champ.exe`)
or headless from the CLI:

```
powershell -ExecutionPolicy Bypass -File scripts/unity.ps1
```

That wraps the editor's batchmode CLI:

```
<unity-editor> -batchmode -nographics -quit -projectPath engines/Champ.Unity -executeMethod Champ.Unity.Editor.BuildScript.BuildWindowsCli -buildOutput Build/Windows/Champ.exe -logFile engines/Champ.Unity/Logs/BuildPlayer.log
```

`<unity-editor>` is the one path that differs per machine, so the script resolves it from the
version in `ProjectSettings/ProjectVersion.txt` instead of anyone hard-coding it. It looks in:

| OS | Path |
| --- | --- |
| Windows | `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, or `C:\Program Files\Unity <version>\Editor\Unity.exe` from the standalone installer |
| macOS | `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity` |
| Linux | `~/Unity/Hub/Editor/<version>/Editor/Unity` |

Set `UNITY_EDITOR` to the binary to override that search. The editor itself is an x64 build on
Windows (emulated on an ARM64 host) and native on Apple Silicon, so the host architecture never
comes into it. Pass a different output path as the first argument to build somewhere else.

Either path needs the Windows Build Support module installed alongside the Editor, and an
activated Unity Editor license — that gate applies to batchmode builds exactly like it does to
opening the project, so there's no way around it via the CLI.

The script only knows `BuildTarget.StandaloneWindows64`. A macOS or Linux player is a two-line
change in `BuildScript.cs` (`StandaloneOSX` / `StandaloneLinux64`, plus that target's Build
Support module).

Both the script and the editor launch it use `Start-Process -Wait`. `Unity.exe` and the player
are GUI binaries, and PowerShell does not block on those -- without it the script races past the
build and exits before the game has opened.

## Editor run configurations

`.vscode/launch.json` has a `Run Unity` entry, so **Run ▸ Start Without Debugging** (or the Run
and Debug dropdown) builds the player and launches it. `.vscode/tasks.json` carries the same as
`build:`/`run:` tasks (**Terminal ▸ Run Task**), plus `build: sim`, which type-checks
`Champ.Sim` without opening Unity and routes any errors to the Problems panel.

The launch entry is typed `node`, the launcher built into VS Code and Cursor. It runs whatever
`runtimeExecutable` names -- `powershell` here, nothing to do with Node -- and `noDebug` tells it
not to wait for a debugger to attach, so **no extension has to be installed**. Output goes to the
Debug Console, so no terminal is left open when the game quits. Breakpoints are the thing that
would need an extension, and none is configured.

`outputCapture: std` matters more than it looks: the default, `console`, reads output through the
debugger protocol, and `noDebug` means there is no protocol -- the Debug Console stays empty
through a build that takes minutes, which looks exactly like a run that never started.

Cursor flags `noDebug` as "not allowed": it is a DAP launch-request field rather than part of
js-debug's own schema, and VS Code passes it through regardless. The warning is cosmetic --
but the property is load-bearing, so leave it.

Both files are meant to be committed and shared as-is: nothing in them names a machine, an editor
install or a host architecture; `scripts/unity.ps1` works those out per machine.

## The sim

Change castle shape, map size or hero speed in `src/Champ.Sim/CastleWorld.cs`. Unity uses that
folder as a `file:` package (`engines/Champ.Unity/Packages/manifest.json`), so edits land the next
time the Editor compiles. It is also a `netstandard2.1` project, which is only there so the sim
can be built and type-checked without Unity -- nothing ships from it.

`CastleWorld.Surfaces` is the ground: a list of `Surface` (grass, path, stone) ordered
back-to-front. That order is load-bearing -- `CastleView` lifts patch `i` by `i * step` so
overlapping ground never z-fights, keeping elevation in step with paint order. Adding a surface is
a sim-only edit.

`CameraFollow` holds the follow-and-clamp math rather than `CastleView` rolling its own: it eases
towards the hero at `Smoothing` per second, then clamps the view inside `CastleWorld.Bounds`. The
view fixes a 24-unit height and derives width from the window.

It is night. Torches are the only real light -- one shadow-casting point light per entry in
`CastleWorld.Torches`, at `TorchIntensity`, reaching `TorchRange` -- over a flat floor of
`CastleWorld.AmbientLight` in the same warm colour, so ground no torch reaches stays walkable
instead of going black. Those three constants live in the sim beside the torch positions they
describe. The hero and the flames are unlit sprites, which is what keeps them readable wherever
they are.

Unity's built-in forward path lights each object with only `pixelLightCount` per-pixel lights, and
the grass is a single object, so `CastleView` raises that to cover every torch at once -- without
it most of the pools simply would not show.
