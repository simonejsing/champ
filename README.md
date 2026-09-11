# Champ

Scaffolding for a 2D top-down champion walking a castle keep. The same simulation (`Champ.Sim`) is presented by three C# renderers so they can be compared side by side.

**Controls:** WASD or arrow keys. Escape quits the MonoGame and Stride windows.

The hero starts just inside the south gate. Walk the courtyard, side chambers, and the north room -- or head out the gate and down the path into the meadow around the keep. The camera eases along behind you and stops at the edge of the map.

## Layout

| Path | Role |
| --- | --- |
| `src/Champ.Sim` | Movement, AABB walls, ground surfaces, follow camera, generated hero pixels. No graphics types. |
| `engines/Champ.MonoGame` | DesktopGL SpriteBatch view |
| `engines/Champ.Stride` | Code-only Stride view (ortho 3D keep, lighting) |
| `engines/Champ.Unity` | 2D SpriteRenderer view |

## Where each renderer runs

| Engine | Windows | macOS | Linux |
| --- | --- | --- | --- |
| MonoGame | yes | yes | yes |
| Stride | yes | no | no |
| Unity | Editor and player | Editor; player needs a build-target switch | Editor; player needs a build-target switch |

Stride is Windows-only *as configured here*: the project pulls `Stride.CommunityToolkit.Windows` and renders through Direct3D11. `Champ.Sim` and `Champ.MonoGame` are plain portable .NET.

x64 and ARM64 hosts run the same commands. Where an architecture forces a detour -- Stride has to build `win-x64` even on ARM64 -- the projects and the scripts in `scripts/` work it out at build time, so nothing you invoke, and nothing in `.vscode/`, names a host architecture.

Both engine projects set `AppendRuntimeIdentifierToOutputPath=false`, so the exe lands in `bin/<config>/<tfm>/` whether or not the host pinned a RID. One output path stays valid everywhere, which is what lets a shared debug configuration name a single `program`.

The commands below use forward slashes and no shell-specific syntax, so they read the same in PowerShell, bash and zsh.

## MonoGame

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project engines/Champ.MonoGame
```

DesktopGL renders through OpenGL and SDL2, both of which ship as NuGet natives for
`win-x64`/`win-arm64`, `osx-x64`/`osx-arm64` and `linux-x64`, so this is the one engine that
needs nothing installed beyond the SDK.

### Windows on ARM64

The project pins `win-arm64` and places ARM64 `SDL2.dll` / OpenAL next to the exe. MonoGame
3.8.4 otherwise copies only x64 natives, which fail to load. See `CopyHostNativeLibs` in
`Champ.MonoGame.csproj`; nothing is needed from you.

## Stride

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (current Stride Community Toolkit targets `net10.0`) and a Direct3D11-capable Windows machine.

```
powershell -ExecutionPolicy Bypass -File scripts/stride.ps1 run
```

`scripts/stride.ps1` is a plain `dotnet run --project engines/Champ.Stride` on an x64 host --
use that directly if you like. On ARM64 it first finds the x64 SDK this engine needs (below),
which is why the editor tasks call the script on every machine rather than branching.

This slice uses an orthographic camera looking down at lit cubes so you can feel Stride as a renderer, not a SpriteBatch clone. Gameplay still comes from `Champ.Sim`.

### Windows on ARM64

Stride's shader compiler loads `spirv-cross.dll` unconditionally (even on the Direct3D11
backend), and `Stride.Dependencies.SpirvCross` only ships that native library for
`win-x64`/`win-x86` — there is no `win-arm64` build. On an ARM64 Windows machine the app
otherwise crashes on the first `Draw` with:

```
System.DllNotFoundException: Could not locate or load native library spirv-cross
   at Stride.Shaders.Compilers.SpirvTranslator..cctor()
```

`Champ.Stride.csproj` pins `RuntimeIdentifier=win-arm64` → **`win-x64`** on ARM64 hosts so the
app runs under Windows' built-in x64 emulation instead, using the natives that actually exist.
That alone isn't enough, though: Stride's AssetCompiler shells out to a nested `dotnet` on
`PATH`, and if that resolves to an ARM64-native SDK it refuses to load the win-x64 build
(`FileLoadException: assembly architecture is not compatible`). So install an x64 SDK side by
side, once:

```powershell
# Either: the official x64 installer, which lands in Program Files\dotnet\x64 (needs admin)
# Or: the install script, no admin rights required
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0 -Architecture x64 -InstallDir $HOME\dotnet-x64 -NoPath
```

From then on `scripts/stride.ps1` handles it: it looks for that SDK in `Program Files\dotnet\x64`,
`$HOME\dotnet-x64` and `C:\dotnet-x64`, and puts the first one it finds ahead of `PATH` for the
build. On an x64 host it finds nothing to do and shells straight through to `dotnet` -- which is
how one task serves both machines.

MonoGame doesn't need this dance — its native deps (SDL2 etc.) do ship `win-arm64` builds.

## Unity

Needs [Unity 2022.3 LTS](https://unity.com/download) or newer (Hub will upgrade the project if you open it in Unity 6). The Editor runs on Windows, macOS and Linux.

1. Add `engines/Champ.Unity` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play. `CastleView` builds the keep at runtime and pulls sim code from the local `com.champ.sim` package.

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

## Editor run configurations

`.vscode/launch.json` has one entry per engine, so **Run ▸ Start Without Debugging** (or the
Run and Debug dropdown) starts any of the three:

| Configuration | Runs |
| --- | --- |
| `Run MonoGame` | `dotnet run --project engines/Champ.MonoGame` |
| `Run Stride` | `scripts/stride.ps1 run` |
| `Run Unity player` | `scripts/unity.ps1 -Run` -- batchmode build, then the player it produced |

They are typed `node`, the launcher built into VS Code and Cursor. It runs whatever
`runtimeExecutable` names -- `dotnet` and `powershell` here, nothing to do with Node -- and
`noDebug` tells it not to wait for a debugger to attach, so **no extension has to be
installed**. Output goes to the Debug Console, so no terminal is left open when the game
quits. Breakpoints are the thing that would need an extension, and none is configured.

`outputCapture: std` matters more than it looks: the default, `console`, reads output through
the debugger protocol, and `noDebug` means there is no protocol -- the Debug Console stays
empty. That is survivable for MonoGame and Stride, whose windows appear in seconds, but the
Unity entry spends minutes in its batchmode build and looks dead without it.

Cursor flags `noDebug` as "not allowed": it is a DAP launch-request field rather than part of
js-debug's own schema, and VS Code passes it through regardless. The warning is cosmetic --
but the property is load-bearing, so leave it.

`.vscode/tasks.json` carries the same three as `run:` tasks (**Terminal ▸ Run Task**) plus a
`build:` task per engine.

Both files are meant to be committed and shared as-is: nothing in them names a host
architecture, an SDK location or an editor install -- the flattened output path covers the
first, and the two scripts resolve the other two per machine.

## Shared sim

Change castle shape, map size or hero speed in `src/Champ.Sim/CastleWorld.cs`. MonoGame and Stride pick it up on rebuild. Unity uses the same folder as a `file:` package (`engines/Champ.Unity/Packages/manifest.json`).

`CastleWorld.Surfaces` is the ground: a list of `Surface` (grass, path, stone) ordered back-to-front. That order is load-bearing -- MonoGame paints them in sequence, and the 3D renderers lift patch `i` by `i * step` so overlapping ground never z-fights. Adding a surface is a sim-only edit.

`CameraFollow` holds the follow-and-clamp math rather than each renderer rolling its own, so all three pan identically: it eases towards the hero at `Smoothing` per second, then clamps the view inside `CastleWorld.Bounds`. Each renderer passes its own visible extent, since all three fix a 24-unit view height and derive width from the window.

It is night in all three: torches are the only real light, over a floor of `CastleWorld.AmbientLight` so ground no torch reaches stays walkable instead of going black. `CastleWorld.Torches` lists the torch posts -- around all four walls, at the gate, down the path and through the rooms of the keep -- and `CastleWorld.TorchLight(point)` gives the torchlight reaching any point, blocked by walls; `LightAt` adds the ambient floor and `TorchIntensity` on top, which is what the two renderers that bake light maps ask for.

Those four numbers live in the sim because the engines light the world in ways that share nothing else. Unity is the reference: real point lights with hard shadows, a flat ambient colour, gamma space, no tone mapping. Stride cannot follow it directly -- its point lights are unusable with this camera -- so its ground and wall tops are emissive materials multiplied by a light map baked from `LightAt`, and its tone map and bloom are switched off, leaving the same `albedo x light` Unity computes. MonoGame bakes the same map into one texture and multiplies it over the scene, with flames and hero drawn afterwards so they stay unlit.

`TorchLight` follows Unity's own point light attenuation, `1/(1 + 25(d/r)^2)`, so a torch throws the same pool in all three. If the engines drift apart again, `TorchBrightness` in `Champ.Stride/Program.cs` is the trim for Stride's overall level, and `CastleWorld.AmbientLight` moves all three at once.
