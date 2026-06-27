using System.Collections.Generic;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Buildings;
using VoxelTycoon.Electricity;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Recipes;
using VoxelTycoon.UI;

namespace TVVTM.Electricity.Network
{
    /// <summary>
    /// Single source of truth for the electricity simulation.
    ///
    /// <para>Topology: poles + wires form an undirected graph (<c>Pole._wires</c>); each connected
    /// component is a "grid". A pole powers the tiles in <c>Area.Square(pole.Origin, PoweringRadius)</c>.
    /// A building belongs to a grid if any of its footprint tiles is in that grid's coverage.</para>
    ///
    /// <para>Balance (instantaneous): a grid is powered iff
    /// <c>sum(fueled generator output) &gt;= sum(enabled consumer consumption)</c>. Binary: an
    /// under-powered grid freezes ALL its consumers (handled by the Harmony prefixes querying
    /// <see cref="IsConsumerPowered"/>).</para>
    ///
    /// Driven by <see cref="LazyManager{T}"/>'s per-frame OnUpdate; heavy work is throttled.
    /// No custom save data — everything derives from poles (saved by the game) and live state.
    /// </summary>
    public class ElectricityNetworkManager : LazyManager<ElectricityNetworkManager>
    {
        /// <summary>Set by the mod entry once the registry is built.</summary>
        public ElectricityRegistry Registry { get; set; }

        private static readonly Color WarnBg = new Color(0.95f, 0.6f, 0.1f, 1f);
        private static readonly Color WarnFg = Color.white;

        private readonly Dictionary<Xz, HashSet<int>> _coverage = new Dictionary<Xz, HashSet<int>>();
        private readonly Dictionary<int, bool> _gridPowered = new Dictionary<int, bool>();
        private readonly Dictionary<Device, float> _genFuelUntil = new Dictionary<Device, float>();
        private readonly Dictionary<Building, Indicator> _indicators = new Dictionary<Building, Indicator>();

        private float _topoTimer;
        private float _balanceTimer;

        // ---- Public query used by the Harmony prefixes (must be cheap) ----

        /// <summary>True if the consumer building is on a grid that currently has enough power.</summary>
        public bool IsConsumerPowered(Building building)
        {
            int grid = GetPrimaryGrid(building);
            return grid >= 0 && _gridPowered.TryGetValue(grid, out bool powered) && powered;
        }

        // ---- Lifecycle ----

        protected override void OnUpdate()
        {
            if (Registry == null)
                return;

            ElectricitySettings s = Registry.Settings;
            float dt = Time.deltaTime;

            _topoTimer += dt;
            if (_topoTimer >= s.TopologyRebuildInterval)
            {
                _topoTimer = 0f;
                RebuildTopology();
            }

            _balanceTimer += dt;
            if (_balanceTimer >= s.BalanceInterval)
            {
                float elapsed = _balanceTimer; // real time since last balance — used to bill power
                _balanceTimer = 0f;
                RecomputeBalance(elapsed);
                UpdateIndicators();
            }
        }

        // ---- Topology ----

        private void RebuildTopology()
        {
            _coverage.Clear();

            ImmutableList<Pole> poles = LazyManager<BuildingManager>.Current.GetAll<Pole>();
            int count = poles.Count;
            if (count == 0)
                return;

            // Index poles and union-find over wire adjacency → connected-component grid ids.
            var index = new Dictionary<Pole, int>(count);
            for (int i = 0; i < count; i++)
                index[poles[i]] = i;

            var parent = new int[count];
            for (int i = 0; i < count; i++)
                parent[i] = i;

            for (int i = 0; i < count; i++)
            {
                Pole pole = poles[i];
                List<Wire> wires = pole._wires;
                if (wires == null)
                    continue;
                for (int w = 0; w < wires.Count; w++)
                {
                    Pole other = wires[w].GetOtherPole(pole);
                    if (other != null && index.TryGetValue(other, out int j))
                        Union(parent, i, j);
                }
            }

            // Coverage: each pole stamps its powering square with its grid id.
            for (int i = 0; i < count; i++)
            {
                Pole pole = poles[i];
                if (!pole.IsBuilt)
                    continue;
                int grid = Find(parent, i);
                using (PooledList<Xz> tiles = Area.Square(pole.Origin, pole.PoweringRadius))
                {
                    for (int t = 0; t < tiles.Count; t++)
                    {
                        Xz xz = tiles[t];
                        if (!_coverage.TryGetValue(xz, out HashSet<int> set))
                        {
                            set = new HashSet<int>();
                            _coverage[xz] = set;
                        }
                        set.Add(grid);
                    }
                }
            }
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb)
                parent[rb] = ra;
        }

        // ---- Balance ----

        private void RecomputeBalance(float dt)
        {
            var consumption = new Dictionary<int, double>();   // consumer demand
            var freeGen = new Dictionary<int, double>();        // coal generators (fueled)
            var sellerCap = new Dictionary<int, double>();      // paid power available
            var sellerCapPrice = new Dictionary<int, double>(); // capacity * pricePerWatt (for avg price)
            var grids = new HashSet<int>();

            BuildingManager bm = LazyManager<BuildingManager>.Current;

            ImmutableList<Device> devices = bm.GetAll<Device>();
            for (int i = 0; i < devices.Count; i++)
            {
                Device d = devices[i];
                if (!d.IsBuilt)
                    continue;
                int grid = GetPrimaryGrid(d);
                if (grid < 0)
                    continue;
                grids.Add(grid);

                ElectricityRegistry.Resolved r = Registry.Resolve(d.AssetId, BuildingKind.Device);
                if (r.Kind == ParticipantKind.Generator)
                {
                    if (GeneratorFueled(d, r))
                        Add(freeGen, grid, r.PowerOutput);
                }
                else if (r.Kind == ParticipantKind.Consumer && d.IsEnabled && d.IsConfigured)
                {
                    Add(consumption, grid, r.Consumption);
                }
            }

            ImmutableList<Mine> mines = bm.GetAll<Mine>();
            for (int i = 0; i < mines.Count; i++)
            {
                Mine m = mines[i];
                if (!m.IsBuilt || !m.IsEnabled)
                    continue;
                int grid = GetPrimaryGrid(m);
                if (grid < 0)
                    continue;
                grids.Add(grid);

                ElectricityRegistry.Resolved r = Registry.Resolve(m.AssetId, BuildingKind.Mine);
                if (r.Kind == ParticipantKind.Consumer)
                    Add(consumption, grid, r.Consumption);
            }

            // Sellers (e.g. city heating plants): supply power for money, no fuel needed.
            ImmutableList<Store> stores = bm.GetAll<Store>();
            for (int i = 0; i < stores.Count; i++)
            {
                Store st = stores[i];
                if (!st.IsBuilt)
                    continue;
                ElectricityRegistry.Resolved r = Registry.Resolve(st.AssetId, BuildingKind.Store);
                if (r.Kind != ParticipantKind.Seller)
                    continue;
                int grid = GetPrimaryGrid(st);
                if (grid < 0)
                    continue;
                grids.Add(grid);
                Add(sellerCap, grid, r.PowerOutput);
                Add(sellerCapPrice, grid, r.PowerOutput * r.PricePerWatt);
            }

            _gridPowered.Clear();
            double totalCharge = 0.0;
            foreach (int grid in grids)
            {
                double c = consumption.TryGetValue(grid, out double cv) ? cv : 0.0;
                double f = freeGen.TryGetValue(grid, out double fv) ? fv : 0.0;
                double sc = sellerCap.TryGetValue(grid, out double sv) ? sv : 0.0;

                bool powered = (f + sc) >= c;
                _gridPowered[grid] = powered;

                // Bill only for power actually drawn from sellers, and only when the grid runs
                // (frozen grids consume nothing). Free coal generation is used first.
                if (powered && c > 0.0 && sc > 0.0)
                {
                    double drawnFromSellers = Mathf.Clamp((float)(c - f), 0f, (float)sc);
                    if (drawnFromSellers > 0.0)
                    {
                        double avgPrice = sellerCapPrice[grid] / sc;
                        totalCharge += drawnFromSellers * avgPrice * dt;
                    }
                }
            }

            if (totalCharge > 0.0 && Company.Current != null)
                Company.Current.AddMoney(-totalCharge, BudgetItem.BuildingRunningCosts, false);
        }

        private bool GeneratorFueled(Device device, ElectricityRegistry.Resolved r)
        {
            float now = Time.time;
            bool observed = false;

            Recipe recipe = device.Recipe;
            if (recipe != null && recipe.InputItems != null)
            {
                for (int i = 0; i < recipe.InputItems.Length; i++)
                {
                    RecipeItem ri = recipe.InputItems[i];
                    if (r.FuelItemUri != null && (ri.Item == null || ri.Item.AssetUri != r.FuelItemUri))
                        continue;
                    if (device.GetReceivedItemCount(i) > 0)
                    {
                        observed = true;
                        break;
                    }
                }
            }

            // Mid-cycle counts as fueled (coal already consumed this cycle).
            if (!observed && (device.State == DeviceState.Working || device.State == DeviceState.WaitingForOutputItem))
                observed = true;

            if (observed)
                _genFuelUntil[device] = now + Registry.Settings.FuelGraceSeconds;

            return _genFuelUntil.TryGetValue(device, out float until) && until > now;
        }

        // ---- Indicators ----

        private void UpdateIndicators()
        {
            BuildingManager bm = LazyManager<BuildingManager>.Current;

            ImmutableList<Device> devices = bm.GetAll<Device>();
            for (int i = 0; i < devices.Count; i++)
            {
                Device d = devices[i];
                bool consumer = d.IsBuilt && !Registry.IsGenerator(d.AssetId) && d.IsEnabled && d.IsConfigured;
                SetIndicator(d, consumer && !IsConsumerPowered(d));
            }

            ImmutableList<Mine> mines = bm.GetAll<Mine>();
            for (int i = 0; i < mines.Count; i++)
            {
                Mine m = mines[i];
                bool consumer = m.IsBuilt && m.IsEnabled;
                SetIndicator(m, consumer && !IsConsumerPowered(m));
            }
        }

        private void SetIndicator(Building building, bool unpowered)
        {
            bool has = _indicators.TryGetValue(building, out Indicator existing);
            if (unpowered && !has)
            {
                Indicator ind = WarningIndicator.For(
                    building, new Vector3(0f, building.Size.Y, 0f), WarnBg, WarnFg, "");
                _indicators[building] = ind;
            }
            else if (!unpowered && has)
            {
                if (existing != null)
                    Manager<IndicatorManager>.Current.RemoveIndicator(existing);
                _indicators.Remove(building);
            }
        }

        // ---- Grid attachment ----

        /// <summary>Smallest grid id covering any of the building's footprint tiles, or -1.</summary>
        private int GetPrimaryGrid(Building building)
        {
            if (building == null || !building.IsBuilt)
                return -1;

            Xyz pos = building.Position;
            Xyz size = building.Size;
            int best = -1;

            for (int x = 0; x < size.X; x++)
            {
                for (int z = 0; z < size.Z; z++)
                {
                    var xz = new Xz(pos.X + x, pos.Z + z);
                    if (_coverage.TryGetValue(xz, out HashSet<int> grids))
                    {
                        foreach (int g in grids)
                            if (best < 0 || g < best)
                                best = g;
                    }
                }
            }
            return best;
        }

        private static void Add(Dictionary<int, double> map, int key, double value)
        {
            map.TryGetValue(key, out double cur);
            map[key] = cur + value;
        }
    }
}
