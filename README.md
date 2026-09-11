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

## MonoGame

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run --project engines/Champ.MonoGame
```

On Windows ARM64 the project pins `win-arm64` and places ARM64 `SDL2.dll` / OpenAL next to the exe. MonoGame 3.8.4 otherwise copies only x64 natives, which fail to load.

## Stride

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (current Stride Community Toolkit targets `net10.0`).

```powershell
dotnet run --project engines/Champ.Stride
```

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
(`FileLoadException: assembly architecture is not compatible`). So an x64 SDK has to be first
on `PATH` for the whole build/run:

```powershell
# one-time: install an x64 SDK side-by-side (no admin rights required)
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0 -Architecture x64 -InstallDir C:\dotnet-x64 -NoPath

# each session: put the x64 SDK first, then run as usual
$env:PATH = "C:\dotnet-x64;$env:PATH"
$env:DOTNET_ROOT = "C:\dotnet-x64"
dotnet run --project engines/Champ.Stride
```

MonoGame doesn't need this dance — its native deps (SDL2 etc.) do ship `win-arm64` builds, so
`Champ.MonoGame.csproj` just pins the RID and copies them next to the exe (see
`CopyHostNativeLibs` in that `.csproj`).

## Unity

Needs [Unity 2022.3 LTS](https://unity.com/download) or newer (Hub will upgrade the project if you open it in Unity 6).

1. Add `engines/Champ.Unity` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play. `CastleView` builds the keep at runtime and pulls sim code from the local `com.champ.sim` package.

### Standalone build

`Assets/Editor/BuildScript.cs` builds a Windows player from `Main.unity`, either from the
Editor menu (**Champ ▸ Build Standalone Windows**, drops the exe at `Build/Windows/Champ.exe`)
or headless from the CLI:

```powershell
& "C:\Program Files\Unity 2022.3.62f3\Editor\Unity.exe" -batchmode -nographics -quit `
    -projectPath engines\Champ.Unity `
    -executeMethod Champ.Unity.Editor.BuildScript.BuildWindowsCli `
    -buildOutput Build\Windows\Champ.exe `
    -logFile -
```

Either path needs the Windows Build Support module installed alongside the Editor, and an
activated Unity Editor license — that gate applies to batchmode builds exactly like it does to
opening the project, so there's no way around it via the CLI.

## Shared sim

Change castle shape, map size or hero speed in `src/Champ.Sim/CastleWorld.cs`. MonoGame and Stride pick it up on rebuild. Unity uses the same folder as a `file:` package (`engines/Champ.Unity/Packages/manifest.json`).

`CastleWorld.Surfaces` is the ground: a list of `Surface` (grass, path, stone) ordered back-to-front. That order is load-bearing -- MonoGame paints them in sequence, and the 3D renderers lift patch `i` by `i * step` so overlapping ground never z-fights. Adding a surface is a sim-only edit.

`CameraFollow` holds the follow-and-clamp math rather than each renderer rolling its own, so all three pan identically: it eases towards the hero at `Smoothing` per second, then clamps the view inside `CastleWorld.Bounds`. Each renderer passes its own visible extent, since all three fix a 24-unit view height and derive width from the window.

It is a moonless night in Stride and Unity: torchlight is the only light. `CastleWorld.Torches` lists the torch posts -- around all four walls, at the gate, down the path and through the rooms of the keep -- and `CastleWorld.TorchLight(point)` gives the light reaching any point, falling off over `TorchRange` and blocked by walls. Stride bakes that into light maps for the ground and wall tops (its point lights are unusable with this camera); Unity uses real point lights with shadows. MonoGame does not draw the torches and stays in daylight.
