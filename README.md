# TVVTM — Voxel Tycoon Electricity Mod

Production buildings (**factories `Device`** and **mines `Mine`**) require **electricity**.
You supply it by building **coal generators** and wiring them to your industry with the game's
**poles + wires**. Electricity is a **spatial network**: each connected group of poles is its own
**grid**. A grid is powered while `generation ≥ consumption`; an under-powered grid **freezes all
its production** (with a warning indicator) until you add generation or shed load.

This is a **Harmony code mod** plus a small **content pack**.

## How it works

- **Grids** = connected components of poles (`Pole._wires`). Separate pole networks are independent.
- A building is **on a grid** if its footprint sits within a pole's `PoweringRadius`.
- **Generators** are `Device`s flagged by AssetId (via `*.electricity` config). They feed a fixed
  wattage into their grid while fueled with coal (delivered by the normal conveyor/train system).
- **Consumption/generation values are data** (`*.electricity` files). Every Device/Mine uses the
  defaults unless a per-asset override exists.
- Harmony prefixes on the private `Device.InvalidateState` / `Mine.InvalidateState` freeze
  production when the grid is under-powered. **No custom save data** — everything derives from
  poles (saved by the game) and live state.

See [the plan](docs/PLAN.md) and `docs/` for design details.

## Repository layout

```
src/                     C# code mod (netstandard2.1)
  Config/                *.electricity (de)serializer: surrogate, handler, spec
  Network/               ElectricityNetworkManager (grids, balance, indicators)
  Patches/               Harmony prefixes (Device/Mine InvalidateState)
  ElectricityMod.cs      Mod entry
pack/                    deployable content pack (mod.json + *.electricity configs)
docs/                    authoring guide for the generator building
lib/                     reference DLLs (you provide — see below; gitignored)
VoxelTycoon/             decompiled game sources (reference only; gitignored)
```

## Build

1. Copy these from `<game>/VoxelTycoon_Data/Managed/` into `lib/`:
   `VoxelTycoon.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.dll`, `Newtonsoft.Json.dll`.
2. Put **`0Harmony.dll`** (NuGet `Lib.Harmony`, Unity-compatible build) into `lib/` too — it is
   bundled with the mod (the game does not ship Harmony).
3. Build:
   ```sh
   dotnet build src/TVVTM.Electricity.csproj -c Release
   ```

## Install

Create a folder named **`TVVTM_Electricity`** under the game's `Content/` directory and copy into it:

- `TVVTM.Electricity.dll` (from `bin/Release/`) — Harmony is provided by the game, no need to bundle.
- everything in `pack/`: `mod.json`, the `*.electricity` configs, and the **included placeholder
  generator** (`coalgenerator.device` + `.recipe` + `.recipetarget`, reusing the base alloy-smelter
  model) and its localization (`en.strings`, `en.strings.json`).

The folder name becomes the pack Uri prefix `tvvtm_electricity`, matching the generator's
`TargetUri`. Enable the pack in-game. To ship your own generator model later, see
[docs/AUTHORING-GENERATOR.md](docs/AUTHORING-GENERATOR.md).

> Heads-up: once enabled, **every factory and mine needs power**. Build "Coal Generator(s)",
> deliver coal, and wire poles to your industry — anything off-grid or on an under-powered grid
> stops (with a warning indicator).

> The game loads mods by scanning the pack folder for `*.dll` and instantiating any `Mod` subclass,
> and resolves dependencies (`0Harmony.dll`) from the same folder — so just drop both DLLs in.

## Verify

1. Launch; check the log for `[TVVTM.Electricity] Initialized.` and `Registry built: …`.
2. In a save: build a generator, deliver coal, run poles/wires to a factory in range.
   - Factory runs while the grid has enough generation.
   - Cut coal / overload the grid / disconnect poles ⇒ that grid's factories **and** mines freeze
     and show a warning indicator.
   - A second, separate pole network behaves independently.
3. Save & reload mid-state — powered/unpowered state and generator fuel recompute correctly.
4. Edit a value in `defaults.electricity` (or a per-asset override), restart — behavior changes
   without recompiling.
```
