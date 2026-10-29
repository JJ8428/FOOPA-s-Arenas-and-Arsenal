using FoopaArenaArsenal.Common.Systems;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargeOmniSpongeBase : LargeSpongeBase
    {
        protected override int LiquidType => 0;

        protected virtual ModItem DecraftAs => null;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            if (DecraftAs != null)
                ItemID.Sets.ShimmerCountsAsItem[Type] = DecraftAs.Type;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            if (player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            bool anyTileModified = false;
            int offset = Sweep / 2;

            for (int dx = -offset; dx <= offset; dx++)
            {
                for (int dy = -offset; dy <= offset; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile == null || (tile.HasTile && Main.tileSolid[tile.TileType]) || tile.LiquidAmount <= 0)
                        continue;

                    WorldGen.EmptyLiquid(x, y);
                    for (int a = 1; a <= 5; a++)
                    {
                        if (WorldGen.InWorld(x - a, y))
                        {
                            Tile leftAdjTile = Main.tile[x - a, y];
                            if (leftAdjTile != null && !(leftAdjTile.HasTile && Main.tileSolid[leftAdjTile.TileType]) && leftAdjTile.LiquidAmount < 255 && leftAdjTile.LiquidAmount > 0)
                                WorldGen.EmptyLiquid(x - a, y);
                            else
                                break;
                        }
                    }
                    for (int a = 0; a <= 3; a++)
                    {
                        if (WorldGen.InWorld(x + a, y))
                        {
                            Tile rightAdjTile = Main.tile[x + a, y];
                            if (rightAdjTile != null && !(rightAdjTile.HasTile && Main.tileSolid[rightAdjTile.TileType]) && rightAdjTile.LiquidAmount < 255 && rightAdjTile.LiquidAmount > 0)
                                WorldGen.EmptyLiquid(x + a, y);
                            else
                                break;
                        }
                    }

                    anyTileModified = true;
                }
            }

            if (anyTileModified)
            {
                SoundEngine.PlaySound(Splash1, player.Center);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep + 3);

                    ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                    packet.Write((byte)FoopaArenaArsenal.PacketType.LargeSpongeSound);
                    packet.Write(centerX);
                    packet.Write(centerY);
                    packet.Send();
                }
            }

            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var itemTooltip = new TooltipLine(
                Mod,
                "LargeSponge",
                $"Capable of soaking up MORE endless liquid" +
                "\nRight Click to toggle sweep size"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    public class LargeOmniSponge1 : LargeOmniSpongeBase
    {
        protected override string ItemName => "LargeOmniSponge1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeOmniSponge2>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
        
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargeWaterSponge)
                .AddRecipeGroup(FAARecipeSystem.LargeLavaSponge)
                .AddRecipeGroup(FAARecipeSystem.LargeHoneySponge)
                .AddTile(TileID.LunarCraftingStation)
                .AddCustomShimmerResult(ModContent.ItemType<LargeWaterSponge1>())
                .AddCustomShimmerResult(ModContent.ItemType<LargeLavaSponge1>())
                .AddCustomShimmerResult(ModContent.ItemType<LargeHoneySponge1>())
                .Register();
        }
    }

    public class LargeOmniSponge2 : LargeOmniSpongeBase
    {
        protected override string ItemName => "LargeOmniSponge2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeOmniSponge3>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargeOmniSponge1>();  

    }

    public class LargeOmniSponge3 : LargeOmniSpongeBase
    {
        protected override string ItemName => "LargeOmniSponge3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeOmniSponge1>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargeOmniSponge1>();  
    }
}