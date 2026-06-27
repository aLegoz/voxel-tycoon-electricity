using HarmonyLib;
using TVVTM.Electricity.Network;
using VoxelTycoon.Buildings;

namespace TVVTM.Electricity.Patches
{
    /// <summary>
    /// Freezes a mine's extraction while its electrical grid is under-powered. Same approach as
    /// <see cref="DeviceInvalidateStatePatch"/>: skipping the original private
    /// <c>Mine.InvalidateState</c> halts mining progress until power returns.
    /// </summary>
    [HarmonyPatch(typeof(Mine), "InvalidateState", new[] { typeof(float) })]
    internal static class MineInvalidateStatePatch
    {
        private static bool Prefix(Mine __instance, float deltaTime)
        {
            ElectricityNetworkManager mgr = ElectricityNetworkManager.Current;
            if (mgr?.Registry == null || !__instance.IsBuilt)
                return true;

            return mgr.IsConsumerPowered(__instance);
        }
    }
}
