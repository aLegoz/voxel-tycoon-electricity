using HarmonyLib;
using TVVTM.Electricity.Config;
using TVVTM.Electricity.Network;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.AssetLoading;
using VoxelTycoon.Modding;

namespace TVVTM.Electricity
{
    /// <summary>
    /// Mod entry. Production buildings (Device + Mine) require electricity supplied by coal
    /// generators over a spatial pole/wire network. See the plan for the full design.
    /// </summary>
    public class ElectricityMod : Mod
    {
        private const string HarmonyId = "tvvtm.electricity";

        private Harmony _harmony;
        private ElectricityRegistry _registry;
        private bool _registryBuilt;

        protected override void Initialize()
        {
            // 1) Register our (de)serializer so the game imports every *.electricity config file.
            //    Must happen before assets are imported → Initialize() is the right hook.
            Manager<AssetLibrary>.Current.RegisterHandler<ElectricityAssetHandler>();

            // 2) Tunables: built-in defaults, overridable by a Role:"Defaults" *.electricity asset
            //    (applied in _registry.Build, since assets aren't loaded yet here).
            _registry = new ElectricityRegistry(new ElectricitySettings());

            // 3) Apply Harmony patches (freeze unpowered Device/Mine production).
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(ElectricityMod).Assembly);

            Debug.Log("[TVVTM.Electricity] Initialized.");
        }

        protected override void OnGameStarting()
        {
            // NOTE: assets are imported AFTER mod load (GameController: LoadMods → AssetLibrary.Load),
            // so OnModsInitialized is too early to resolve TargetUri → AssetId. OnGameStarting runs
            // after asset import, and LazyManagers reset per game — so build here (once) and attach.
            if (_registry != null && !_registryBuilt)
            {
                _registry.Build();
                _registryBuilt = true;
            }

            if (_registry != null)
                ElectricityNetworkManager.Current.Registry = _registry;
        }

        protected override void Deinitialize()
        {
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
        }
    }
}
