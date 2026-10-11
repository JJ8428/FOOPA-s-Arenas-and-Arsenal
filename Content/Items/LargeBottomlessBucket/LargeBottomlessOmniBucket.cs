using FoopaArenaArsenal.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargeBottomlessOmniBucketBase : LargeBottomlessBucketBase
    {
        protected abstract ModItem AltChangeInto { get; }

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeBottomlessBucket/{ItemName}";

        private void ChangeMode(Player player)
        {
            bool wasFavorited = Item.favorited;
            int slotIndex = -1;
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i] == Item)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex == -1)
                return;

            int newType = ChangeInto.Item.type;
            Item newItem = new Item();
            newItem.SetDefaults(newType);
            newItem.favorited = wasFavorited;

            player.inventory[slotIndex] = newItem;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotIndex);
        }

        private void AltChangeMode(Player player)
        {
            bool wasFavorited = Item.favorited;
            int slotIndex = -1;
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i] == Item)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex == -1)
                return;

            int newType = AltChangeInto.Item.type;
            Item newItem = new Item();
            newItem.SetDefaults(newType);
            newItem.favorited = wasFavorited;

            player.inventory[slotIndex] = newItem;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotIndex);
        }
        
        public override void RightClick(Player player)
        {
            if (AltHeld())
                AltChangeMode(player);
            else
                ChangeMode(player);
        }

        private static bool AltHeld()
        {
            return Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)
                || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt);
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                if (AltHeld())
                    AltChangeMode(player);
                else
                    ChangeMode(player);

                return false;
            }

            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeBottomlessBucket/{ItemName}";
            Texture2D baseTexture = ModContent.Request<Texture2D>(basePath, AssetRequestMode.ImmediateLoad).Value;
            Rectangle baseFrame = new Rectangle(0, 0, baseTexture.Width, baseTexture.Height);
            Vector2 baseOrigin = new Vector2(baseTexture.Width / 2f, baseTexture.Height / 2f);

            spriteBatch.Draw(baseTexture, position, baseFrame, drawColor, 0f, baseOrigin, scale, SpriteEffects.None, 0f);

            Player player = Main.LocalPlayer;

            bool isCursorIcon = Item == Main.mouseItem || Item == ContentSamples.ItemsByType[Type];

            if (!isCursorIcon) 
            {
                bool isSelectedHotbarSlot = player.selectedItem >= 0 &&
                    player.selectedItem < player.inventory.Length &&
                    player.inventory[player.selectedItem] == Item &&
                    !Main.playerInventory;

                string badgePath = $"FoopaArenaArsenal/Content/Items/ItemUI/{Badge}";
                Texture2D badge = ModContent.Request<Texture2D>(badgePath, AssetRequestMode.ImmediateLoad).Value;
                Rectangle badgeFrame = new Rectangle(0, 0, badge.Width, badge.Height);
                Vector2 badgeOrigin = new Vector2(badge.Width / 2f, badge.Height / 2f);
                
                Vector2 badgeOffset = isSelectedHotbarSlot
                    ? new Vector2(2.5f, 1.5f)
                    : new Vector2(-0.5f, -0.5f);

                spriteBatch.Draw(badge, position + badgeOffset, badgeFrame, drawColor, 0f, badgeOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool ModifyItemDraw(ref PlayerDrawSet drawInfo, ref DrawData drawData, ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData)
        {
            Player player = drawInfo.drawPlayer;
            Vector2 playerCenter = player.Center - Main.screenPosition;

            Vector2 offsetFromPlayer = drawData.position - playerCenter;
            drawData.position = playerCenter + (offsetFromPlayer * 0.5f);

            return true;
        }

        private static readonly string[] LiquidTypeLabels =
        {
            $"[i:{ItemID.BottomlessBucket}] Water",
            $"[i:{ItemID.BottomlessLavaBucket}] Lava",
            $"[i:{ItemID.BottomlessHoneyBucket}] Honey",
            $"[i:{ItemID.BottomlessShimmerBucket}] Shimmer"
        };

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string liquidListLine = "";
            for (int i = 0; i < LiquidTypeLabels.Length; i++)
            {
                string label = LiquidTypeLabels[i];

                // Split the icon tag from the text so we only color the text part
                int closeBracketIndex = label.IndexOf(']') + 1;
                string iconTag = label.Substring(0, closeBracketIndex);
                string text = label.Substring(closeBracketIndex);

                string color = (i == LiquidType) ? "64FF64" : "888888";
                string entry = $"{iconTag}[c/{color}:{text}]";

                liquidListLine += entry;

                if (i < LiquidTypeLabels.Length - 1)
                    liquidListLine += "\n";
            }

            string info =
                "Contains MORE endless amount of all liquids" +
                "\nRight Click to toggle sweep size" +
                "\nAlt + Right Click to cycle liquid type" +
                "\n" +
                liquidListLine;

            var itemTooltip = new TooltipLine(Mod, "LargeSponge", info);

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    public class LargeBottomlessOmniWaterBucket1 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniWaterBucket1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket2>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 0;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket1>();

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargeBottomlessWaterBucket)
                .AddRecipeGroup(FAARecipeSystem.LargeBottomlessLavaBucket)
                .AddRecipeGroup(FAARecipeSystem.LargeBottomlessHoneyBucket)
                .AddRecipeGroup(FAARecipeSystem.LargeBottomlessShimmerBucket)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class LargeBottomlessOmniWaterBucket2 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniWaterBucket2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket3>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 0;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket2>();
    }

    public class LargeBottomlessOmniWaterBucket3 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniWaterBucket3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket1>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 0;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket3>();
    }

        public class LargeBottomlessOmniLavaBucket1 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniLavaBucket1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket2>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 1;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket1>();
    }

    public class LargeBottomlessOmniLavaBucket2 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniLavaBucket2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket3>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 1;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket2>();
    }

    public class LargeBottomlessOmniLavaBucket3 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniLavaBucket3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniLavaBucket1>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 1;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket3>();
    }

        public class LargeBottomlessOmniHoneyBucket1 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniHoneyBucket1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket2>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 2;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket1>();
    }

    public class LargeBottomlessOmniHoneyBucket2 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniHoneyBucket2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket3>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 2;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket2>();
    }

    public class LargeBottomlessOmniHoneyBucket3 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniHoneyBucket3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniHoneyBucket1>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 2;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket3>();
    }

        public class LargeBottomlessOmniShimmerBucket1 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniShimmerBucket1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket2>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 3;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket1>();
    }

    public class LargeBottomlessOmniShimmerBucket2 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniShimmerBucket2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket3>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 3;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket2>();
    }

    public class LargeBottomlessOmniShimmerBucket3 : LargeBottomlessOmniBucketBase
    {
        protected override string ItemName => "LargeBottomlessOmniShimmerBucket3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.15f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeBottomlessOmniShimmerBucket1>();
        protected override int BonusRange => 12;
        protected override byte LiquidType => 3;
        protected override int Rarity => ItemRarityID.Yellow;
        protected override int Price => Item.sellPrice(0, 15, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeBottomlessOmniWaterBucket3>();
    }
}