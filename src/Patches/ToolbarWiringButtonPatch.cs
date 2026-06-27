using System.Reflection;
using HarmonyLib;
using VoxelTycoon;
using VoxelTycoon.Buildings;
using VoxelTycoon.Game.UI.ModernUI;
using VoxelTycoon.Tools;
using VoxelTycoon.Tools.Builder;
using VoxelTycoon.UI;

namespace TVVTM.Electricity.Patches
{
    /// <summary>
    /// Adds a top-level "Electricity" toolbar group (available immediately, NO research gate) with
    /// two buttons: build poles/wires (<see cref="WiringTool"/>) and build the Coal Generator.
    ///
    /// The base game ships the WiringTool but never exposes it in any toolbar, and our generator is
    /// a normal Device that would otherwise be buried in Industry → Factories. Grouping both here
    /// makes the whole electricity workflow discoverable in one place. (The vanilla "Electrification"
    /// button is unrelated — that's rail catenary for electric trains.)
    /// </summary>
    [HarmonyPatch(typeof(Toolbar), "Awake")]
    internal static class ToolbarWiringButtonPatch
    {
        private const string GeneratorUri = "tvvtm_electricity/coalgenerator.device";

        private static readonly MethodInfo AddMethod = AccessTools.Method(
            typeof(Toolbar), "Add",
            new[] { typeof(FontIcon), typeof(string), typeof(ToolbarAction) });

        private static void Postfix(Toolbar __instance)
        {
            if (AddMethod == null)
                return;

            var buttons = new[]
            {
                new SubToolbarButton(
                    FontIcon.Ketizoloto(I.Electrification), "Poles & Wires",
                    new ToolToolbarAction(() => new WiringTool())),
                new SubToolbarButton(
                    FontIcon.Ketizoloto(I.Factory), "Coal Generator",
                    new ToolToolbarAction(BuildGeneratorTool)),
            };

            AddMethod.Invoke(__instance, new object[]
            {
                FontIcon.Ketizoloto(I.Electrification),
                "Electricity",
                new ShowSubToolbarAction(buttons),
            });
        }

        private static ITool BuildGeneratorTool()
        {
            BuildingRecipe recipe = FindGeneratorRecipe();
            if (recipe == null)
                return null;
            return LazyManager<BuilderToolManager>.Current.GetTool(recipe);
        }

        private static BuildingRecipe FindGeneratorRecipe()
        {
            AssetLibrary lib = Manager<AssetLibrary>.Current;
            if (!lib.TryGetAssetId(GeneratorUri, out int id))
                return null;

            ImmutableList<BuildingRecipe> all = LazyManager<BuildingRecipeManager>.Current.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRecipe recipe = all[i];
                if (recipe.Building != null && recipe.Building.AssetId == id)
                    return recipe;
            }
            return null;
        }
    }
}
