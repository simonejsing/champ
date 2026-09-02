# Champ

Scaffolding for a 2D top-down champion walking a castle keep. The same simulation (`Champ.Sim`) is presented by three C# renderers so they can be compared side by side.

**Controls:** WASD or arrow keys. Escape quits the MonoGame and Stride windows.

The hero starts just inside the south gate. Walk the courtyard, side chambers, and the north room.

## Layout

| Path | Role |
| --- | --- |
| `src/Champ.Sim` | Movement, AABB walls, generated hero pixels. No graphics types. |
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

## Unity

Needs [Unity 2022.3 LTS](https://unity.com/download) or newer (Hub will upgrade the project if you open it in Unity 6).

1. Add `engines/Champ.Unity` in Unity Hub.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play. `CastleView` builds the keep at runtime and pulls sim code from the local `com.champ.sim` package.

## Shared sim

Change castle shape or hero speed in `src/Champ.Sim/CastleWorld.cs`. MonoGame and Stride pick it up on rebuild. Unity uses the same folder as a `file:` package (`engines/Champ.Unity/Packages/manifest.json`).
