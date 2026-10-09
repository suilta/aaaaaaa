# Element Sandbox

Foundation milestone of a 2D side-scrolling action game set in a medieval sword-and-magic world.
Elements are **matter**, not status effects: fire spreads and consumes fuel, water flows and pools,
smoke rises. This milestone is the pixel-level falling-sand simulation everything else will sit on
(reference feel: *Noita*). There is no player, level or enemy yet.

## Requirements

| Tool | Version |
|---|---|
| Godot | **4.7.2 .NET edition** |
| .NET SDK | 8.0 |
| `Godot.NET.Sdk` (NuGet) | 4.7.2 (the exact version is on nuget.org; no fallback was needed) |
| Renderer | GL Compatibility |

## Running the game

1. Open Godot 4.7.2 .NET, choose **Import**, and select `game/project.godot`.
2. Press **Play** (F5). Godot restores NuGet packages and builds the C# project the first time.

The game opens on the prebuilt scene in a 1280 × 720 window (320 × 180 cells, 4 × 4 pixels each)
and runs one simulation step per physics tick (60 per second).

## Controls

| Input | Action |
|---|---|
| Left drag | Paint the current material (round brush; only fills empty cells; powders, liquids and gases fill ~35% of cells) |
| Right drag | Erase |
| Mouse wheel | Brush radius 1–20 |
| 1–8 | 石 Stone, 沙 Sand, 水 Water, 木 Wood, 油 Oil, 火 Fire, 熔岩 Lava, 蒸汽 Steam (keypad digits work too) |
| G | Flip gravity |
| Space | Pause (painting still works while paused) |
| R | Reset to the prebuilt scene (also resets gravity) |
| C | Clear everything |

The fire brush ignites flammable cells it touches and puts flames into empty cells.
Fast strokes are interpolated so lines stay continuous. The HUD in the top-left shows the brush,
its size, the gravity direction and the key help in Chinese.

## The prebuilt scene

- **Left:** a stone pool of water with an oil slick. Light the oil (6 + left click) and it burns on
  the water's surface while the water survives.
- **Middle:** a wooden box on wooden legs holding a pile of sand. Burn the legs or the box and the
  sand collapses onto the floor.
- **Right:** a lava box sealed by a thick wooden plug above a pool. With no input, the lava burns
  through the plug in about 4 seconds, drips into the pool, turns to stone and gives off steam.

## Tests

The simulation core is plain C# with no Godot types, so it is tested headless:

```sh
dotnet test                         # from the repository root (ElementSandbox.sln)
dotnet build game/Sandbox.csproj    # builds the Godot project without the editor
```

`tests/Sandbox.Tests` compiles `game/Scripts/Core/**/*.cs` directly and runs xUnit tests against
`World` with fixed seeds. They cover every scenario in the spec, including:

- sand resting on a floor
- water levelling out
- oil above water
- sand sinking through water
- wood burning away
- water putting out burning wood
- burning oil on water
- lava quenching to stone and steam
- steam condensing
- the gravity flip
- mass conservation over 1000 steps
- one move per particle per step

There are also tests for the demo scene's chain reactions.

CI (`.github/workflows/ci.yml`) runs `dotnet build game/Sandbox.csproj -warnaserror` and
`dotnet test tests/Sandbox.Tests` on every push.

## Architecture

```
game/
  project.godot, Sandbox.csproj, Main.tscn   Godot project (one Node2D with Sandbox.cs)
  Scripts/Sandbox.cs                         Godot node: input, stepping, texture upload, HUD
  Scripts/Core/                              pure C#, no Godot dependencies
    Materials.cs   Mat enum, Phase enum, MaterialDef property table
    World.cs       grid storage, update loop, reactions, brush, RGBA render
    DemoScene.cs   prebuilt starting scene
    Rgb.cs         small color struct
tests/Sandbox.Tests/                         xUnit project (kept outside game/ so Godot's
                                             csproj glob does not pick it up)
```

**Data.** Cells live in parallel arrays: `Mat[] cells`, `short[] life`, `short[] burn`,
`byte[] shade` (per-particle color noise) and `byte[] clock` (per-step "already updated" stamp).
A move swaps all four particle arrays together and stamps both cells.

**Update order.** Each step scans rows from the gravity-side bottom towards the top, with a random
horizontal direction per row. Each cell runs reactions, then lifetime, then movement.

**Rendering.** `World.Render(byte[] rgba)` writes RGBA8. `Sandbox.cs` uploads it with
`Image.SetData` + `ImageTexture.Update` and stretches it over the window with nearest filtering.

### Reactions come from properties

There is no "A + B = C" table. Every rule asks about properties:

| Property | Meaning |
|---|---|
| `Phase` | Empty / Solid / Powder / Liquid / Gas: how it moves |
| `Density` | Movers swap with lighter liquids/gases (gases only enter empty cells) |
| `Dispersion`, `Fluidity` | Sideways flow distance; per-frame chance to move at all |
| `Flammability`, `BurnTicks`, `BurnsInto`, `FlameChance` | Catching fire, burning time, burnt remains, flames thrown while burning |
| `Heat` | > 0 means hot: ignites flammable neighbors; chance to boil an extinguisher it touches |
| `QuenchedInto` | What a hot material becomes when it touches an extinguisher |
| `Extinguishes`, `BoilsInto` | Puts out fire / quenches hot things; what it turns into when boiled |
| `LifeMin`/`LifeMax`, `DecaysInto`/`DecayChance` | Lifetime, and what it may leave behind on expiry |
| `Color`, `Jitter`, `AgeColor`, `FadesOut` | Looks: base color, noise, color at end of life, fade to background |

The spec's reactions fall out of these properties:

- **Fire** (hot gas) dies on water and may boil it.
- **Lava** (hot liquid, `QuenchedInto = Stone`) turns to stone and boils water into steam.
- **Burning wood** is put out by water.
- **Burning oil** keeps burning on water, because burning liquids are not extinguished.
- **Steam** may condense back into water when it expires.

Hot gases touch with their 8 neighbors, while denser phases touch with 4.

**Adding a material:** add a value to `Mat` and an entry in `Materials.Build()` with its
properties. It then moves, burns, quenches and layers with everything that already exists. To
make it paintable, add it to `Palette` in `Sandbox.cs`.

## Project status

All ten milestones of the first stage are done, one commit each.

Verification:

- `dotnet build` passes with no warnings or errors, and all 57 `dotnet test` tests pass.
- Godot 4.7.2 .NET for Linux builds the project headless (`--build-solutions`).
- Under Xvfb (Mesa llvmpipe, GL Compatibility), Godot rendered the demo scene with no errors.
- Measured on the Linux dev container (Release build), not the owner's machine:
  - about 0.7 ms per step+render on the demo scene
  - about 2.6 ms with the whole screen covered in burning oil
- 60 FPS on the owner's machine still needs checking in Godot's debugger monitors.

### Manual checklist for the owner

- [ ] Sand forms slopes.
- [ ] Water levels out in a container.
- [ ] Oil floats on water.
- [ ] Sand sinks to the bottom of water.
- [ ] Lit oil burns only on the oil layer.
- [ ] The sand pile collapses when the wooden frame burns.
- [ ] Lava turns to stone in water and gives off steam.
- [ ] Steam rises and later drips back down as water.
- [ ] After flipping gravity (G), all of the above reverses direction.
- [ ] Godot's debugger monitors show a steady 60 FPS.

### Behaviors worth knowing

- **Stone crust on lava pools.** Stone is a solid and never moves. The first lava to reach a pool
  becomes a stone crust on the water's surface, and later lava pools on top of that crust
  instead of sinking. Steam raining back down can harden more of it.
- **Lava falls slowly.** `Fluidity` is a per-frame chance to move at all, so viscous lava also
  falls through air at about a quarter of normal speed and drips in a loose stream.
- **Fire can stall in thin wood.** Fire spreads by chance. A one-cell-thick wooden beam can
  stop burning partway; thicker wood burns through reliably.
- **HUD font.** The HUD asks for Microsoft YaHei, SimHei or Noto Sans CJK SC. Otherwise Godot
  falls back to any system font that has the glyphs.

### Next stage (out of scope here)

- Frost, lightning and wind.
- A per-cell temperature system to replace "touching fire may ignite".
- Player, sword, enemies and levels.
- Rigid bodies.
- Performance work: chunk sleeping, threads.
