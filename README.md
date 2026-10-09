# Element Sandbox

Foundation of a 2D side-scrolling action game set in a medieval sword-and-magic world.
Elements are **matter**, not status effects: fire spreads and consumes fuel, water flows and pools,
smoke rises. This project is the pixel-level falling-sand simulation everything else will sit on
(reference feel: *Noita*). There is no player, level or enemy yet.

Two stages are done:

1. **Falling sand:** materials, movement, gravity flip and the prebuilt scene.
2. **Temperature:** every cell has a temperature, and all reactions are driven by heat.

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
| 1–9 | 石 Stone, 沙 Sand, 水 Water, 木 Wood, 油 Oil, 火 Fire, 熔岩 Lava, 蒸汽 Steam, 冰 Ice (keypad digits work too) |
| G | Flip gravity |
| T | Toggle the thermal view (heat map of every cell) |
| Space | Pause (painting still works while paused) |
| R | Reset to the prebuilt scene (also resets gravity) |
| C | Clear everything |

The fire brush lights flammable cells it touches and puts flames into empty cells. The ice brush
paints deep-frozen ice (−60 °C).

Fast strokes are interpolated so lines stay continuous. The HUD in the top-left shows the brush,
its size, the gravity direction, the pause and thermal-view state, and the key help in Chinese.

## The prebuilt scene

- **Left:** a stone pool of water with an oil slick. Light the oil (6 + left click) and it burns on
  the water's surface. The water barely boils, and only stray droplets of oil survive.
- **Middle:** a wooden box on wooden legs holding a pile of sand. Light the legs and the fire eats
  into them from the outside. The box comes down and the sand collapses onto the floor, about 15 s
  after lighting.
- **Right:** a lava box sealed by a wooden plug above a pool. With no input:
  1. The lava heats the plug until it smoulders.
  2. The plug burns from its underside, where air reaches it, and gives way after about 12 s.
  3. The lava drips into the pool, hardens into a stone crust and gives off steam.
  4. More lava collects on top of the crust as a lake.

## Tests

The simulation core is plain C# with no Godot types, so it is tested headless:

```sh
dotnet test                         # from the repository root (ElementSandbox.sln)
dotnet build game/Sandbox.csproj    # builds the Godot project without the editor
```

`tests/Sandbox.Tests` compiles `game/Scripts/Core/**/*.cs` directly and runs 77 xUnit tests against
`World` with fixed seeds.

Stage 1 tests cover every scenario in its spec:

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

Stage 2 tests cover:

- conduction conserving heat
- insulation
- heat travelling with particles
- heat through a stone wall igniting wood
- smothered fuel going out
- bulk lava staying molten
- water capped at 100 °C and boiling gradually
- freezing and melting
- glow

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
    World.cs       grid storage, conduction, update loop, reactions, brush, RGBA render
    DemoScene.cs   prebuilt starting scene
    Rgb.cs         small color struct
tests/Sandbox.Tests/                         xUnit project (kept outside game/ so Godot's
                                             csproj glob does not pick it up)
```

**Data.** Cells live in parallel arrays:

- `Mat[] cells`: the material
- `short[] life`: remaining lifetime
- `short[] burn`: remaining burn time
- `byte[] shade`: per-particle color noise
- `float[] temp`: temperature in °C; air cells have one too
- `byte[] clock`: a per-step "already updated" stamp

A move swaps all the particle arrays together and stamps both cells.

**Update order.** Each step:

1. Runs a **conduction pass** over every cell face.
2. Scans rows from the gravity-side bottom towards the top, with a random horizontal direction per
   row. Each cell runs reactions, then lifetime, then movement.

**Rendering.** `World.Render(byte[] rgba, RenderMode)` writes RGBA8, either material colors or the
thermal heat map. `Sandbox.cs` uploads it with `Image.SetData` + `ImageTexture.Update` and
stretches it over the window with nearest filtering.

### Reactions come from properties

There is no "A + B = C" table, and since stage 2 no "touching X does Y" rule either. Every rule
asks about properties, and every reaction is driven by temperature.

| Property | Meaning |
|---|---|
| `Phase` | Empty / Solid / Powder / Liquid / Gas: how it moves |
| `Density` | Movers swap with lighter liquids/gases (gases only enter empty cells) |
| `Dispersion`, `Fluidity` | Sideways flow distance; per-frame chance to move at all |
| `BaseTemp` | Temperature of a freshly created particle (lava 1200 °C, ice −60 °C, most 20 °C) |
| `Conductivity`, `HeatCapacity` | How readily heat crosses its faces; heat needed per degree |
| `AmbientRate` | Relaxation toward 20 °C per step (air: the open world as a heat sink) |
| `SourceTemp`, `SourceRate` | Heat sources that hold their own temperature (lava, flames) |
| `HotAbove`/`HotInto`/`HotLatent` | Boiling and melting: what it becomes above a temperature, with latent heat |
| `ColdBelow`/`ColdInto`/`ColdLatent` | Freezing, hardening and going out: what it becomes below a temperature |
| `Flammability`, `IgnitionTemp` | Chance per step to catch fire once at ignition temperature; cooling below it puts the fire out |
| `BurnHeat`, `BurnTemp`, `BurnTicks`, `BurnsInto`, `FlameChance` | Heat from combustion, its ceiling, burn time, remains, flames thrown |
| `SupportsCombustion` | Supplies oxygen (air, flame, smoke): fuel only burns while touching it |
| `LifeMin`/`LifeMax`, `DecaysInto`/`DecayChance` | Lifetime, and what it may leave behind on expiry |
| `Color`, `Jitter`, `AgeColor`, `FadesOut` | Looks: base color, noise, color at end of life, fade to background |

Everything else falls out of these properties:

- **Fire spreads** only through heat. A burning cell heats itself while it touches air, and heat
  conducted into its neighbours lights them once they reach their ignition temperature. This works
  through walls: lava behind a stone wall can light wood on the other side.
- **Water puts fires out by cooling** the burning cell below its ignition point. Insulating oil
  burns too hot for that, so burning oil keeps burning on water.
- **Lava hardens into stone below 700 °C.** Air never cools it that far, but water does. The new
  stone keeps its heat and glows.
- **Water boils at 100 °C and freezes below −2 °C**, both with latent heat, so it never gets hotter
  than 100 °C and boils gradually. Ice melts above 2 °C. The gap stops cells flickering between
  the two.
- **Flames die below 400 °C** (water cools them fast).
- **Steam** may condense back into water when it expires.
- **Hot solids, powders and liquids glow** above 450 °C.

**Adding a material:** add a value to `Mat` and an entry in `Materials.Build()` with its
properties. It then moves, heats, burns, boils, freezes and layers with everything that already
exists. The table rejects thermal values that would make conduction unstable. To make the
material paintable, add it to `Palette` in `Sandbox.cs`.

## Project status

Both stages are done and committed in steps: stage 1 as milestones 1–10, stage 2 as T1–T4.

Verification:

- `dotnet build` passes with no warnings or errors, and all 77 `dotnet test` tests pass.
- Godot 4.7.2 .NET for Linux builds the project headless (`--build-solutions`).
- Under Xvfb (Mesa llvmpipe, GL Compatibility), Godot rendered the demo scene with no errors.
- Measured on the Linux dev container (Release build), not the owner's machine:
  - about 1.0 ms per step+render on the demo scene, conduction included
  - about 3.4 ms with the whole screen covered in burning oil
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
- [ ] The thermal view (T) shows heat spreading from fire and lava.
- [ ] Freshly hardened stone and stone next to lava glow.
- [ ] Wood placed against the outside of a lava-filled stone basin eventually catches fire.
- [ ] Water dripped onto a thick ice slab (9) freezes; ice dropped near fire or lava melts.
- [ ] Godot's debugger monitors show a steady 60 FPS.

### Design decisions

- **Stone crust on lava pools (intended).** Lava touching water hardens into stone, and stone is a
  solid that never moves. The first lava to reach a pool hardens into a crust on the water's
  surface, and later lava collects on top of it as a lava lake instead of sinking, as in *Noita*.
  This is kept on purpose because it builds new terrain during play.
  `LavaOnWaterFormsAStoneCrustThatHoldsALavaLake` pins this behavior.
- **Heat contact uses the geometric mean** of the two conductivities. Two insulators barely
  exchange heat, while water dominates its contact with wood. This is what lets wood be insulating
  enough to keep burning, yet still be put out by water. A harmonic mean could not do both.
- **Combustion needs air.** Only burning cells that touch air produce heat and use up fuel. Wood
  burns from the outside in, buried fuel smoulders, and the bottom of an oil slick waits for the
  layer above to burn off instead of boiling the water under it.
- **Latent heat without extra state.** Past a threshold, a cell is held at that temperature and
  changes form with chance excess ÷ latent heat per step. On average it absorbs the latent heat
  first.
- **Lava values were solved, not tuned.** Lava's conductivity (0.02), self-heating (0.03) and air
  conductivity (0.0005) are chosen so that all three of these hold:
  - water on deep lava hardens it
  - bulk lava on cold stone stays molten
  - a lava drop falling through air stays molten

### Behaviors worth knowing

- **Lava falls slowly.** `Fluidity` is a per-frame chance to move at all, so viscous lava also
  falls through air at about a quarter of normal speed and drips in a loose stream.
- **Lava builds a stone bed.** The advancing front of a lava flow over cold stone hardens into a
  bed under the molten lava.
- **Oil leaves droplets.** As an oil film burns thin it breaks into droplets with air between them.
  Air is an excellent insulator, so an isolated droplet can't gather enough heat to ignite.
- **Freezing needs a lot of cold.** A little water on a lot of ice freezes, but an ice block in a
  warm pool melts instead.
- **HUD font.** The HUD asks for Microsoft YaHei, SimHei or Noto Sans CJK SC. Otherwise Godot
  falls back to any system font that has the glyphs.

### Next stage (out of scope here)

- Frost, lightning and wind as player elements. Frost can build directly on temperature and ice.
- Player, sword, enemies and levels.
- Rigid bodies.
- Performance work: chunk sleeping, threads.
