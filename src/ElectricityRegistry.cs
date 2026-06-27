using System.Collections.Generic;
using TVVTM.Electricity.Config;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.AssetLoading;

namespace TVVTM.Electricity
{
    public enum BuildingKind { Device, Mine, Store }

    /// <summary>How a building participates in the electricity network at runtime.</summary>
    public enum ParticipantKind { None, Consumer, Generator, Seller }

    /// <summary>
    /// Per-asset electricity resolution: maps a building AssetId to its role and values.
    /// Built once after all assets load, from the <see cref="ElectricitySpec"/> assets the game
    /// imported (via <see cref="ElectricityAssetHandler"/>) plus the default-consumption fallback.
    /// </summary>
    public class ElectricityRegistry
    {
        public readonly struct Resolved
        {
            public readonly ParticipantKind Kind;
            public readonly double Consumption;   // watts drawn (consumers)
            public readonly double PowerOutput;   // watts supplied (generators/sellers)
            public readonly string FuelItemUri;   // optional fuel filter (generators)
            public readonly double PricePerWatt;  // money per watt/sec drawn (sellers)

            public Resolved(ParticipantKind kind, double consumption, double powerOutput,
                            string fuelItemUri, double pricePerWatt)
            {
                Kind = kind;
                Consumption = consumption;
                PowerOutput = powerOutput;
                FuelItemUri = fuelItemUri;
                PricePerWatt = pricePerWatt;
            }

            public static readonly Resolved None = new Resolved(ParticipantKind.None, 0, 0, null, 0);
        }

        private readonly ElectricitySettings _settings;
        private readonly Dictionary<int, ElectricitySpec> _byTargetAssetId = new Dictionary<int, ElectricitySpec>();

        public ElectricityRegistry(ElectricitySettings settings)
        {
            _settings = settings;
        }

        public ElectricitySettings Settings => _settings;

        /// <summary>
        /// Builds the AssetId → spec map. Call after every asset is loaded, since a spec's TargetUri
        /// may reference an asset that loaded after the .electricity file.
        /// </summary>
        public void Build()
        {
            _byTargetAssetId.Clear();
            AssetLibrary lib = Manager<AssetLibrary>.Current;

            foreach (ElectricitySpec spec in lib.GetAll<ElectricitySpec>())
            {
                // Defaults specs carry mod-wide tunables, not a building binding.
                if (spec.Role == ElectricityRole.Defaults)
                {
                    _settings.ApplyFrom(spec);
                    continue;
                }

                if (string.IsNullOrEmpty(spec.TargetUri))
                {
                    Debug.LogWarning($"[TVVTM.Electricity] '{spec.Uri}' has no TargetUri, skipped.");
                    continue;
                }

                if (!lib.TryGetAssetId(spec.TargetUri, out int targetId))
                {
                    Debug.LogWarning($"[TVVTM.Electricity] '{spec.Uri}' TargetUri '{spec.TargetUri}' not found, skipped.");
                    continue;
                }

                spec.TargetAssetId = targetId;
                _byTargetAssetId[targetId] = spec;
            }

            Debug.Log($"[TVVTM.Electricity] Registry built: {_byTargetAssetId.Count} explicit spec(s); " +
                      $"defaults dev={_settings.DefaultDeviceConsumption}W mine={_settings.DefaultMineConsumption}W.");
        }

        /// <summary>Resolve a building's electricity behavior, applying defaults when no override exists.</summary>
        public Resolved Resolve(int assetId, BuildingKind kind)
        {
            if (_byTargetAssetId.TryGetValue(assetId, out ElectricitySpec spec))
            {
                switch (spec.Role)
                {
                    case ElectricityRole.Generator:
                        return new Resolved(ParticipantKind.Generator, 0, spec.PowerOutput, spec.FuelItemUri, 0);
                    case ElectricityRole.Seller:
                        return new Resolved(ParticipantKind.Seller, 0, spec.PowerOutput, null, spec.PricePerWatt);
                    default: // Consumer
                        double c = spec.Consumption > 0 ? spec.Consumption : DefaultFor(kind);
                        return new Resolved(ParticipantKind.Consumer, c, 0, null, 0);
                }
            }

            // No override: Device/Mine consume by default ("all production needs power");
            // Stores (city buildings) stay out of the system unless explicitly configured.
            if (kind == BuildingKind.Store)
                return Resolved.None;

            return new Resolved(ParticipantKind.Consumer, DefaultFor(kind), 0, null, 0);
        }

        public bool IsGenerator(int assetId)
            => _byTargetAssetId.TryGetValue(assetId, out ElectricitySpec s) && s.Role == ElectricityRole.Generator;

        private double DefaultFor(BuildingKind kind)
            => kind == BuildingKind.Mine ? _settings.DefaultMineConsumption : _settings.DefaultDeviceConsumption;
    }
}
