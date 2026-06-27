namespace TVVTM.Electricity.Config
{
    /// <summary>
    /// Deserialization target for a <c>*.electricity</c> asset config file.
    /// One file per building that participates in the electricity network.
    ///
    /// This is the "deserializer" shape: Voxel Tycoon's <c>AssetHandler.LoadData&lt;T&gt;</c>
    /// parses the JSON into this POCO using the game's JsonHelper settings (so enums are
    /// by-name and Vector/Xz converters are available). Unknown JSON fields are ignored
    /// (MissingMemberHandling = Ignore), and missing fields keep these defaults.
    ///
    /// Example (coal generator):
    /// <code>
    /// {
    ///   "TargetUri": "tvvtm/coalgenerator.device",
    ///   "Role": "Generator",
    ///   "PowerOutput": 120,
    ///   "FuelItemUri": "base/coal.item"
    /// }
    /// </code>
    /// Example (a factory that needs power):
    /// <code>
    /// { "TargetUri": "base/sawmill.device", "Role": "Consumer", "Consumption": 30 }
    /// </code>
    /// </summary>
    public class ElectricityAssetSurrogate
    {
        /// <summary>
        /// Asset Uri this spec applies to (e.g. <c>"base/sawmill.device"</c> or a mod generator).
        /// Resolved to an AssetId after all assets are loaded.
        /// </summary>
        public string TargetUri { get; set; }

        /// <summary>Consumer (default) or Generator.</summary>
        public ElectricityRole Role { get; set; } = ElectricityRole.Consumer;

        /// <summary>Power drawn from the grid each instant, in watts. Consumers only.</summary>
        public double Consumption { get; set; }

        /// <summary>Power supplied to the grid while fueled, in watts. Generators only.</summary>
        public double PowerOutput { get; set; }

        /// <summary>
        /// Optional. Item Uri the generator burns as fuel (e.g. <c>"base/coal.item"</c>).
        /// When set, the generator only contributes <see cref="PowerOutput"/> while it holds
        /// this item in its Device input buffer. When null, "fueled" = the generator Device is
        /// running. Generators only.
        /// </summary>
        public string FuelItemUri { get; set; }

        /// <summary>
        /// Money charged per watt drawn per second. Sellers only (e.g. heating plant). With
        /// <see cref="PowerOutput"/>=200 and PricePerWatt=0.01, fully loaded it costs 2/sec.
        /// </summary>
        public double PricePerWatt { get; set; }

        // ---- Role = Defaults only (mod-wide tunables; null = keep built-in default) ----

        public double? DefaultDeviceConsumption { get; set; }
        public double? DefaultMineConsumption { get; set; }
        public float? FuelGraceSeconds { get; set; }
        public float? TopologyRebuildInterval { get; set; }
        public float? BalanceInterval { get; set; }
    }
}
