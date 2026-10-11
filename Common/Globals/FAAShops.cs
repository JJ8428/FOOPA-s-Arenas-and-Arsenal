using FoopaArenaArsenal.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Common.Globals
{
    public class FAAShops : GlobalNPC
    {
        private static readonly Condition FiveAnglerQuests = new Condition(
            "Mods.FoopaArenaArsenal.Conditions.FiveAnglerQuests",
            () => Main.LocalPlayer.anglerQuestsFinished >= 5);

        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType != NPCID.GoblinTinkerer)
                return;

            // always available
            shop.Add<LargePick11>();
            shop.Add<LargeHammer11>();
            shop.Add<LargePaintBrush11>();
            shop.Add<LargeTile11>();
            shop.Add<LargeWall11>();
            shop.Add<ArenaBuilder1>();
            shop.Add<SlopeTool11>();

            shop.Add<LargeActuationRod11>(Condition.DownedSkeletron);

            shop.Add<LargeBottomlessWaterBucket1>(FiveAnglerQuests);
            shop.Add<LargeWaterSponge1>(FiveAnglerQuests);

            shop.Add<LargeBottomlessLavaBucket1>(Condition.Hardmode);
            shop.Add<LargeLavaSponge1>(Condition.Hardmode);

            shop.Add<LargeBottomlessHoneyBucket1>(Condition.DownedQueenBee);
            shop.Add<LargeHoneySponge1>(Condition.DownedQueenBee);

            shop.Add<LargeBottomlessShimmerBucket1>(Condition.DownedMoonLord);
        }
    }
}