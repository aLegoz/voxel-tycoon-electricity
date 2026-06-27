using TVVTM.Electricity.Config;

namespace TVVTM.Electricity
{
    /// <summary>
    /// Mod-wide tunables. Defaults apply to every production building that has no per-asset
    /// <c>*.electricity</c> override (decision: default + overrides — "all production needs power").
    /// Values are data-driven: a <c>*.electricity</c> asset with <c>Role: "Defaults"</c> overrides
    /// these via <see cref="ApplyFrom"/>. Assemblies are loaded from bytes (no on-disk path), so
    /// defaults travel through the asset pipeline rather than a file next to the DLL.
    /// </summary>
    public class ElectricitySettings
    {
        /// <summary>Watts drawn by a Device (factory) with no per-asset override.</summary>
        public double DefaultDeviceConsumption { get; set; } = 30.0;

        /// <summary>Watts drawn by a Mine with no per-asset override.</summary>
        public double DefaultMineConsumption { get; set; } = 20.0;

        /// <summary>
        /// Seconds a generator keeps supplying power after fuel was last observed. Smooths the
        /// brief gaps between vanilla recipe cycles so power doesn't flicker.
        /// </summary>
        public float FuelGraceSeconds { get; set; } = 3.0f;

        /// <summary>How often (s) the pole graph + coverage map is rebuilt.</summary>
        public float TopologyRebuildInterval { get; set; } = 1.0f;

        /// <summary>How often (s) the per-grid power balance is recomputed.</summary>
        public float BalanceInterval { get; set; } = 0.25f;

        /// <summary>Apply overrides from a <c>Role: Defaults</c> spec (null fields are ignored).</summary>
        public void ApplyFrom(ElectricitySpec spec)
        {
            if (spec.DefaultDeviceConsumption.HasValue) DefaultDeviceConsumption = spec.DefaultDeviceConsumption.Value;
            if (spec.DefaultMineConsumption.HasValue) DefaultMineConsumption = spec.DefaultMineConsumption.Value;
            if (spec.FuelGraceSeconds.HasValue) FuelGraceSeconds = spec.FuelGraceSeconds.Value;
            if (spec.TopologyRebuildInterval.HasValue) TopologyRebuildInterval = spec.TopologyRebuildInterval.Value;
            if (spec.BalanceInterval.HasValue) BalanceInterval = spec.BalanceInterval.Value;
        }
    }
}
