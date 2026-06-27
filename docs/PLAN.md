# Voxel Tycoon Mod — Development Setup Plan

## Context
Greenfield project at `D:\Projects\TVVTM` (empty). User wants to develop a Voxel Tycoon
mod that uses **Harmony** for runtime patching. User can obtain decompiled game sources via
dnSpy and asks how to provide those sources so Claude can write the mod effectively.

Grounding facts (verified via official docs):
- A VT mod is a .NET class library (netstandard2.1) implementing `VoxelTycoon.Modding.Mod`.
- Reference DLLs in `<game>/VoxelTycoon_Data/Managed/`: `VoxelTycoon.dll`,
  `UnityEngine.CoreModule.dll`, `UnityEngine.UI.dll`, etc.
- Harmony is bundled by the modder to patch private/internal game methods.
- Docs: https://docs.voxeltycoon.xyz/ ; samples: https://github.com/voxeltycoon/mods

## Decisions (all resolved via grilling)
- [x] Q1: Mod intent — production buildings require electricity (alter existing logic → Harmony).
- [x] Q2: Network model — **(b) spatial wired grids**; separate systems; coal generators.
- [x] Q3: Feeding sources — dnSpy "Export to Project" → 1790 `.cs` in `VoxelTycoon\`; Claude
      greps/reads on demand. Decompile = read-only reference; compile against original DLLs.
- [x] Q4: Energy semantics — **(a) instantaneous power balance** (no stored energy).
- [x] Q5: Underpowered — **(a) binary**, whole grid freezes; warning indicator.
- [x] Q6: Scope — **(b) `Device` + `Mine`**.
- [x] Q7: Values — **data-driven, read from asset config file**; no hardcoding.
- [x] Generator — new `Device` asset in mod content pack, reused model, flagged by AssetId.
- [x] Save/load — no custom save data (derived from poles + Device buffers).
- [x] References/publicize — netstandard2.1, lib/ DLLs, Harmony reflection (no publicizer).

## Glossary (verified against decompile)
- **Device** (`Buildings/Device.cs`) — the real production machine (recipe-based factory/станок).
  State machine in private `InvalidateState(float)`: NoRecipe → WaitingForConsumeItem →
  Working (advances `_elapsedTime`) → WaitingForOutputItem. Public `IsEnabled`, `IsConfigured`.
- **Plant** (`Buildings/Plant.cs`) — NOT a factory; it's flora/tree (decoration). Ignore.
- **Mine** (`Buildings/Mine.cs`), **Store** (`Buildings/Store.cs`) — other building types; scope TBD.
- **Electricity (base game)** = `ElectricityManager` + `Pole` + `Wire` = purely DECORATIVE.
  No generation, no consumption, no grid, no "powered" concept. Mod must invent it.
- **Harmony hook point**: Prefix on private `Device.InvalidateState(float)` returning `false`
  when device is not powered → freezes production cleanly.

## Mod intent
Make production buildings (`Device`, possibly `Mine`/`Store`) REQUIRE electricity to operate.
Since no consumption mechanic exists, the mod builds it from scratch.
**Chosen model: spatial wired network (b).** Separate grids; coal-fueled generators feed power.

## Network model (verified feasible)
- `Pole : Building` has public `List<Wire> _wires`, `PoweringRadius`, `ConnectionRadius`.
  Poles+wires already form an undirected GRAPH → **a "grid" = connected component** (BFS over
  `_wires` + `Wire.GetOtherPole`). This gives "separate systems" for free.
- Pole registers powering area in `ElectricityManager.Mark(xz,pole)` over `Area.Square(Origin,
  PoweringRadius)` → "device under a pole" is computable (footprint tile in powering area).
- `Tools/WiringTool.cs` + `Electricity/AssetLoading/Pole*` ⇒ poles/wires are REAL player-buildable
  content (just currently meaningless). Mod gives them purpose.
- OPEN/verify: confirm poles are actually reachable in the build menu in normal play.

## Generator strategy (RESOLVED)
- Generator = a new **`Device` asset shipped in the mod's content Pack**, reusing a standard
  building model (.obj/submesh). Authored via `DeviceAssetSurrogate` fields: Submeshes (model),
  Conveyors (coal input connector), RecipeTargetUri (coal-consuming recipe), Smokes/Sounds.
- "Produces electricity" is NOT an asset field — it's the code layer keyed on the generator's
  AssetId. Coal arrives via normal conveyor/train delivery (reuses Device input buffers).
- Content path is the standard VT Pack pipeline (`AssetManagement/Pack`, `*AssetHandler`),
  NOT the Unity Mod SDK. Mod ships: (1) code DLL, (2) content pack folder (asset JSON + model).

## Energy semantics (RESOLVED)
- **(a) Instantaneous power balance.** While a generator has coal it supplies +W to its grid;
  devices draw −W. Grid powered iff sum(generation) >= sum(consumption). No stored energy object;
  only state in save = generator's coal (already in its Device input buffer).

## Underpowered behavior (RESOLVED)
- **(a) Binary per-grid.** If generation < consumption, ALL devices on that grid freeze
  (prefix on InvalidateState returns false). Show a warning indicator on de-powered buildings
  (reuse `WarningIndicator`, as `Pole` does).

## Scope (RESOLVED)
- Power-consuming = **`Device` (factories) + `Mine` (extractors)**. Both have their own private
  `InvalidateState(float)` → two Harmony prefixes, same powered-check. Store/Warehouse/HQ excluded.

## Consumption/generation values (RESOLVED)
- Values are **data, read from a config file**, never hardcoded. Mod loads them at
  `OnModsInitialized` by enumerating assets (`AssetLibrary.GetAll/GetAllIds`) and reading config.
- Game JSON deserializer uses `MissingMemberHandling = Ignore` (JsonHelper) ⇒ custom electricity
  fields can live INSIDE asset JSON without breaking base loading; mod reads them via
  `AssetInfo.FilePath` (`JObject.Parse(File.ReadAllText(...))`). A single side-car mod config
  file (default consumption + per-asset-Uri overrides + generator specs) also works and avoids
  editing base-game files.
- AssetInfo gives `FilePath`, `Uri`, `Id (=Uri.GetHashCode())`; AssetLibrary gives
  `GetAll<T>()`, `GetAllIds<T>()`, `GetAssetInfo(id)`, `GetAssetId(uri)`, `Get<T>`.

## Plan (final)

### Deliverables
1. **Code mod** — netstandard2.1 class library implementing `VoxelTycoon.Modding.Mod`, bundling
   Harmony (`0Harmony.dll`).
2. **Content pack** — generator `.device` asset (reused MeshUri + coal-input conveyor + coal recipe)
   plus electricity **config file** with consumption/generation values.

### Components
- `ElectricityModEntry : Mod` — in `Initialize()` create `new Harmony("tvvtm.electricity")` +
  `PatchAll()`; in `OnModsInitialized()` load the config and build `AssetId → ElectricitySpec`
  (consumption for Device/Mine; output + fuel for generators) by reading asset configs.
- `ElectricityNetworkManager` (a `LazyManager`/`Manager`) — single source of truth:
  - **Grids = connected components** of poles, traversed via `Pole._wires` + `Wire.GetOtherPole`.
  - **Pole coverage**: on pole built/removed, record `Area.Square(Origin, PoweringRadius)` tiles
    → maps a building footprint tile to the pole (hence grid) powering it.
  - **Per-grid balance** (instantaneous): `sum(generator output where fueled) >=
    sum(consumption of enabled+configured Device/Mine attached)` ⇒ grid powered (binary).
  - Cache results; invalidate on pole/wire add/remove and recompute lazily.
- **Harmony patches** (private methods, target via `AccessTools.Method`):
  - Prefix `Device.InvalidateState(float)` → return `false` (freeze) when the device is a
    consumer whose grid is not powered (or not attached to any grid). Skip gating if the device
    is a **generator** (it produces power, must keep burning coal).
  - Prefix `Mine.InvalidateState(float)` → same powered-check.
  - Postfix `Pole.OnBuilt` / `Pole.OnRemoving` (and wire apply/remove) → mark network dirty.
- **Generator** = our `.device` asset flagged by AssetId; modeled as a normal Device whose recipe
  consumes coal. It contributes its configured `+W` to its grid while it has coal in its input
  buffer. No new burn logic needed — reuse Device input buffers + recipe consumption.
- **UI feedback**: `WarningIndicator.For(building, ...)` on de-powered Device/Mine (pattern already
  used by `Pole.UpdateIndicator`).

### Save/load
- **No custom mod save data.** Grids derive from poles (saved by game); generator coal lives in
  the Device input buffer (saved by `Device.Read/Write`); powered state is recomputed each tick.

### Project / build setup
- `lib/` — reference DLLs copied from `<game>/VoxelTycoon_Data/Managed/`: `VoxelTycoon.dll`,
  `UnityEngine.CoreModule.dll`, `UnityEngine.dll`, `Newtonsoft.Json.dll`; plus `0Harmony.dll`
  (Lib.Harmony). `.gitignore` `lib/` and `reference/decompiled/` (do not redistribute game code).
- `src/` — the mod `.csproj` (netstandard2.1) referencing `lib/`. Private members reached via
  Harmony reflection (`AccessTools`) — no publicizer needed.
- Build output (DLL + pack) deployed to the game's mod/content directory.
- RESOLVED: `ModLoader` scans the pack folder for `*.dll`, instantiates any non-abstract `Mod`
  subclass (no manifest entry needed), and `AppDomain.AssemblyResolve` loads dependencies
  (`0Harmony.dll`) from the SAME folder — so bundling Harmony next to the mod DLL works.
- RESOLVED: poles/wires are player content (`Tools/WiringTool.cs`, `Pole`/`Wire` assets).
- Mod is a LocalPack: folder under `Content/` with a `mod.json` manifest (Title/Description/Tags/
  Dependencies/Hidden). Folder name → lowercased pack Uri prefix.
- NOTE: mod assemblies load via `Assembly.Load(bytes)` ⇒ `Assembly.Location` is empty, so config
  next to the DLL is unreadable. Defaults are therefore data-driven via a `Role:"Defaults"`
  `.electricity` asset instead.

## Implementation status (DONE — pending compile with game DLLs)
- `src/Config/` — `*.electricity` (de)serializer: `ElectricityAssetSurrogate`, `ElectricityAssetHandler`
  (Extension "electricity"), `ElectricitySpec`, `ElectricityRole` (Consumer/Generator/Defaults).
- `src/ElectricitySettings.cs`, `src/ElectricityRegistry.cs` (AssetId→spec + defaults).
- `src/Network/ElectricityNetworkManager.cs` (grids via union-find on `Pole._wires`, coverage via
  `Area.Square`, instantaneous balance, warning indicators).
- `src/Patches/` — prefixes on `Device.InvalidateState` + `Mine.InvalidateState`.
- `src/ElectricityMod.cs` — registers handler, applies Harmony, builds registry, attaches manager.
- `pack/` — `mod.json`, `defaults.electricity`, `coalgenerator.electricity`. `docs/AUTHORING-GENERATOR.md`.
- BUILD PREREQ: drop `VoxelTycoon.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.dll`,
  `Newtonsoft.Json.dll`, `0Harmony.dll` into `lib/`, then `dotnet build src/TVVTM.Electricity.csproj`.

## Verification
1. Build the mod; confirm it loads (no missing-dependency errors for Harmony) — check game log.
2. In a test save: place a generator, deliver coal, build poles/wires to a factory in range.
   - Factory works while grid generation ≥ consumption.
   - Cut coal / remove a pole / overload the grid ⇒ factories + mines on that grid freeze and show
     the warning indicator.
   - A second, separate pole network behaves independently (separate grid).
3. Save & reload mid-state; confirm powered/unpowered and generator fuel restore correctly.
4. Tweak a value in the config file, restart; confirm consumption/generation changes without
   recompiling.
