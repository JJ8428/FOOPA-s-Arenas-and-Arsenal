using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using FoopaArenaArsenal.Content.Items;

namespace FoopaArenaArsenal.Common.Systems
{
    public class FAARecipeSystem : ModSystem
    {
        public static RecipeGroup LargePickTier1;
        public static RecipeGroup LargeHammerTier1;
        // public static RecipeGroup LargeTileTier1;
        // public static RecipeGroup LargeWallTier1;

        public static RecipeGroup LargeWaterSponge;
        public static RecipeGroup LargeLavaSponge;
        public static RecipeGroup LargeHoneySponge;

        public static RecipeGroup LargeBottomlessWaterBucket;
        public static RecipeGroup LargeBottomlessLavaBucket;
        public static RecipeGroup LargeBottomlessHoneyBucket;
        public static RecipeGroup LargeBottomlessShimmerBucket;

        public static RecipeGroup LargePaintBrushTier1;
        public static RecipeGroup LargePaintRollerTier1;
        public static RecipeGroup LargePaintScraperTier1;

        public override void AddRecipeGroups()
        {
            string any = Language.GetTextValue("LegacyMisc.37");

            LargePickTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargePick1Dev>())}",
                ModContent.ItemType<LargePick1Dev>(),
                ModContent.ItemType<LargePick11>(),
                ModContent.ItemType<LargePick12>(),
                ModContent.ItemType<LargePick13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargePickTier1", LargePickTier1);

            LargeHammerTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeHammer1Dev>())}",
                ModContent.ItemType<LargeHammer1Dev>(),
                ModContent.ItemType<LargeHammer11>(),
                ModContent.ItemType<LargeHammer12>(),
                ModContent.ItemType<LargeHammer13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeHammerTier1", LargeHammerTier1);

            /*
            LargeTileTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeTile1Dev>())}",
                ModContent.ItemType<LargeTile1Dev>(),
                ModContent.ItemType<LargeTile11>(),
                ModContent.ItemType<LargeTile12>(),
                ModContent.ItemType<LargeTile13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeTileTier1", LargeTileTier1);
            */

            /*
            LargeWallTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeWall1Dev>())}",
                ModContent.ItemType<LargeWall1Dev>(),
                ModContent.ItemType<LargeWall11>(),
                ModContent.ItemType<LargeWall12>(),
                ModContent.ItemType<LargeWall13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeWallTier1", LargeWallTier1);
            */

            LargeWaterSponge = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeWaterSpongeDev>())}",
                ModContent.ItemType<LargeWaterSpongeDev>(),
                ModContent.ItemType<LargeWaterSponge1>(),
                ModContent.ItemType<LargeWaterSponge2>(),
                ModContent.ItemType<LargeWaterSponge3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeWaterSponge", LargeWaterSponge);

            LargeLavaSponge = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeLavaSpongeDev>())}",
                ModContent.ItemType<LargeLavaSpongeDev>(),
                ModContent.ItemType<LargeLavaSponge1>(),
                ModContent.ItemType<LargeLavaSponge2>(),
                ModContent.ItemType<LargeLavaSponge3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeLavaSponge", LargeLavaSponge);

            LargeHoneySponge = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeHoneySpongeDev>())}",
                ModContent.ItemType<LargeHoneySpongeDev>(),
                ModContent.ItemType<LargeHoneySponge1>(),
                ModContent.ItemType<LargeHoneySponge2>(),
                ModContent.ItemType<LargeHoneySponge3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeHoneySponge", LargeHoneySponge);

            LargeBottomlessWaterBucket = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeBottomlessWaterBucketDev>())}",
                ModContent.ItemType<LargeBottomlessWaterBucketDev>(),
                ModContent.ItemType<LargeBottomlessWaterBucket1>(),
                ModContent.ItemType<LargeBottomlessWaterBucket2>(),
                ModContent.ItemType<LargeBottomlessWaterBucket3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeBottomlessWaterBucket", LargeBottomlessWaterBucket);

            LargeBottomlessLavaBucket = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeBottomlessLavaBucketDev>())}",
                ModContent.ItemType<LargeBottomlessLavaBucketDev>(),
                ModContent.ItemType<LargeBottomlessLavaBucket1>(),
                ModContent.ItemType<LargeBottomlessLavaBucket2>(),
                ModContent.ItemType<LargeBottomlessLavaBucket3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeBottomlessLavaBucket", LargeBottomlessLavaBucket);

            LargeBottomlessHoneyBucket = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeBottomlessHoneyBucketDev>())}",
                ModContent.ItemType<LargeBottomlessHoneyBucketDev>(),
                ModContent.ItemType<LargeBottomlessHoneyBucket1>(),
                ModContent.ItemType<LargeBottomlessHoneyBucket2>(),
                ModContent.ItemType<LargeBottomlessHoneyBucket3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeBottomlessHoneyBucket", LargeBottomlessHoneyBucket);

            LargeBottomlessShimmerBucket = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargeBottomlessShimmerBucketDev>())}",
                ModContent.ItemType<LargeBottomlessShimmerBucketDev>(),
                ModContent.ItemType<LargeBottomlessShimmerBucket1>(),
                ModContent.ItemType<LargeBottomlessShimmerBucket2>(),
                ModContent.ItemType<LargeBottomlessShimmerBucket3>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargeBottomlessShimmerBucket", LargeBottomlessShimmerBucket);

            LargePaintBrushTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargePaintBrush1Dev>())}",
                ModContent.ItemType<LargePaintBrush1Dev>(),
                ModContent.ItemType<LargePaintBrush11>(),
                ModContent.ItemType<LargePaintBrush12>(),
                ModContent.ItemType<LargePaintBrush13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargePaintBrushTier1", LargePaintBrushTier1);

            LargePaintRollerTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargePaintRoller1Dev>())}",
                ModContent.ItemType<LargePaintRoller1Dev>(),
                ModContent.ItemType<LargePaintRoller11>(),
                ModContent.ItemType<LargePaintRoller12>(),
                ModContent.ItemType<LargePaintRoller13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargePaintRollerTier1", LargePaintRollerTier1);
        
            LargePaintScraperTier1 = new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ModContent.ItemType<LargePaintScraper1Dev>())}",
                ModContent.ItemType<LargePaintScraper1Dev>(),
                ModContent.ItemType<LargePaintScraper11>(),
                ModContent.ItemType<LargePaintScraper12>(),
                ModContent.ItemType<LargePaintScraper13>());
            RecipeGroup.RegisterGroup("FoopaArenaArsenal:LargePaintScraperTier1", LargePaintScraperTier1);
        }
    }
}