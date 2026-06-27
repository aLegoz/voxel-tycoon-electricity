namespace TVVTM.Electricity.Config
{
    /// <summary>
    /// Runtime electricity spec for one building asset. Produced by
    /// <see cref="ElectricityAssetHandler"/> from a deserialized
    /// <see cref="ElectricityAssetSurrogate"/> and stored in the game's AssetLibrary,
    /// so all specs can be retrieved later via <c>AssetLibrary.GetAll&lt;ElectricitySpec&gt;()</c>.
    ///
    /// <para><see cref="TargetUri"/> is resolved to <see cref="TargetAssetId"/> lazily (after
    /// every asset has loaded) because the target building asset may not exist yet at import time.</para>
    /// </summary>
    public class ElectricitySpec
    {
        /// <summary>Uri of this <c>.electricity</c> asset itself.</summary>
        public string Uri { get; set; }

        /// <summary>AssetId of this <c>.electricity</c> asset itself.</summary>
        public int AssetId { get; set; }

        /// <summary>Uri of the building asset this spec applies to.</summary>
        public string TargetUri { get; set; }

        /// <summary>
        /// Resolved AssetId of the target building. <see cref="Unresolved"/> until
        /// <see cref="ResolveTarget"/> runs.
        /// </summary>
        public int TargetAssetId { get; set; } = Unresolved;

        public ElectricityRole Role { get; set; }
        public double Consumption { get; set; }
        public double PowerOutput { get; set; }
        public string FuelItemUri { get; set; }
        public double PricePerWatt { get; set; }

        // Role = Defaults only.
        public double? DefaultDeviceConsumption { get; set; }
        public double? DefaultMineConsumption { get; set; }
        public float? FuelGraceSeconds { get; set; }
        public float? TopologyRebuildInterval { get; set; }
        public float? BalanceInterval { get; set; }

        public const int Unresolved = 0;

        public bool IsResolved => TargetAssetId != Unresolved;
    }
}
