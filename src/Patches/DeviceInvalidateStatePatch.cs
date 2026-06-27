using HarmonyLib;
using TVVTM.Electricity.Network;
using VoxelTycoon.Buildings;

namespace TVVTM.Electricity.Patches
{
    /// <summary>
    /// Freezes a factory's production state machine while its electrical grid is under-powered.
    /// Returning <c>false</c> skips the original private <c>Device.InvalidateState</c>, so
    /// <c>_elapsedTime</c> stops advancing and the device makes no progress until power returns.
    /// Generators are exempt (they produce power and must keep burning coal).
    /// </summary>
    [HarmonyPatch(typeof(Device), "InvalidateState", new[] { typeof(float) })]
    internal static class DeviceInvalidateStatePatch
    {
        private static bool Prefix(Device __instance, float deltaTime)
        {
            ElectricityNetworkManager mgr = ElectricityNetworkManager.Current;
            if (mgr?.Registry == null || !__instance.IsBuilt)
                return true;

            if (mgr.Registry.IsGenerator(__instance.AssetId))
                return true; // generator: always run

            // Consumer: run only if its grid has enough power; otherwise freeze.
            return mgr.IsConsumerPowered(__instance);
        }
    }
}
