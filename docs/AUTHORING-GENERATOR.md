# Authoring the coal generator building

The mod's code turns a building into a power source purely by its **AssetId** — via
[`pack/coalgenerator.electricity`](../pack/coalgenerator.electricity) whose `TargetUri` points at
the generator device. You still need to ship the actual building asset. It's an ordinary Voxel
Tycoon `Device` (a recipe machine) — you reuse a standard model and give it a coal-consuming recipe.

> Keep this file out of the deployed pack — it's a guide. The real asset is `coalgenerator.device`
> placed **inside** the pack folder so its Uri becomes `tvvtm_electricity/coalgenerator.device`
> (must match `TargetUri`).

## What the game needs

A `*.device` file is JSON deserialized into `DeviceAssetSurrogate` (see the decompile at
`VoxelTycoon/Buildings/AssetLoading/DeviceAssetSurrogate.cs` and its base
`BuildingAssetSurrogate` / `PrimitiveBuildingAssetSurrogate`). Key fields:

| Field | Meaning |
|---|---|
| `Size` | footprint in voxels, `{ "X":.., "Y":.., "Z":.. }` |
| `MeshUri` *(or `Liveries`)* | the model. Reuse a base mesh Uri, or ship your own `.obj`. |
| `Conveyors` | input/output connectors. The generator needs at least **one coal input**. |
| `RecipeTargetUri` | the recipe target this device uses. |
| `Price`, `RunningCosts`, `CategoryUri` | economy + build-menu category. |

You also need a recipe whose **input is coal** (and no output, or a trivial one) so the device
"burns" coal. The code treats the generator as supplying power whenever it has coal in its input
buffer or is mid-cycle (see `ElectricityNetworkManager.GeneratorFueled`).

## Starter template (`coalgenerator.device`)

Fill the placeholders (`<...>`) with real Uris from the game (find them in the decompile or with
dnSpy / the in-game asset browser). This is a starting point, not guaranteed-complete:

```json
{
  "Size": { "X": 2, "Y": 2, "Z": 2 },
  "MeshUri": "<base/some_building.obj#default>",
  "CategoryUri": "<base/industry.assetcategory>",
  "Price": 50000,
  "RunningCosts": 200,
  "RecipeTargetUri": "<your recipe target uri>",
  "Conveyors": [
    {
      "Uri": "<base/conveyor_connector.conveyorconnector>",
      "ConveyorType": "Input",
      "Position": { "X": 0, "Y": 0, "Z": -1 },
      "Rotation": "Rotate0",
      "SpawnConnection": true
    }
  ]
}
```

DisplayName comes from a locale string keyed `tvvtm_electricity/coalgenerator.device#DisplayName`.

## Optional: fuel filter

By default the generator counts as "fueled" while any input item is present. To pin it strictly to
coal, add `"FuelItemUri": "<base/coal.item>"` to `pack/coalgenerator.electricity` (verify coal's
real Uri first).

## Making OTHER buildings consume power

You don't need a file per building — every Device/Mine consumes the defaults from
[`pack/defaults.electricity`](../pack/defaults.electricity). To override one building, drop a
`*.electricity` file in the pack:

```json
{ "TargetUri": "base/sawmill.device", "Role": "Consumer", "Consumption": 45 }
```
