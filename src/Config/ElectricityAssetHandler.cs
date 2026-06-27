using VoxelTycoon.AssetLoading;

namespace TVVTM.Electricity.Config
{
    /// <summary>
    /// (De)serializer for <c>*.electricity</c> asset config files — registered with the game's
    /// asset pipeline so every asset that has a companion <c>.electricity</c> config is parsed
    /// automatically, exactly like the built-in <c>DeviceAssetHandler</c> / <c>ItemAssetHandler</c>.
    ///
    /// Register once, before assets are imported:
    /// <code>Manager&lt;AssetLibrary&gt;.Current.RegisterHandler&lt;ElectricityAssetHandler&gt;();</code>
    ///
    /// The game maps file extension → handler and calls <see cref="Import"/> for each matching file.
    /// We deserialize via <see cref="AssetHandler.LoadData{T}"/> (game JsonHelper settings) and
    /// return an <see cref="ElectricitySpec"/>, which lands in the AssetLibrary keyed by AssetId.
    /// </summary>
    public class ElectricityAssetHandler : AssetHandler
    {
        public const string FileExtension = "electricity";

        public override string Extension => FileExtension;

        protected override object Import(AssetInfo assetInfo)
        {
            ElectricityAssetSurrogate surrogate =
                AssetHandler.LoadData<ElectricityAssetSurrogate>(assetInfo);

            return new ElectricitySpec
            {
                Uri = assetInfo.Uri,
                AssetId = assetInfo.Id,
                TargetUri = surrogate.TargetUri,
                Role = surrogate.Role,
                Consumption = surrogate.Consumption,
                PowerOutput = surrogate.PowerOutput,
                FuelItemUri = surrogate.FuelItemUri,
                PricePerWatt = surrogate.PricePerWatt,
                DefaultDeviceConsumption = surrogate.DefaultDeviceConsumption,
                DefaultMineConsumption = surrogate.DefaultMineConsumption,
                FuelGraceSeconds = surrogate.FuelGraceSeconds,
                TopologyRebuildInterval = surrogate.TopologyRebuildInterval,
                BalanceInterval = surrogate.BalanceInterval,
            };
        }
    }
}
