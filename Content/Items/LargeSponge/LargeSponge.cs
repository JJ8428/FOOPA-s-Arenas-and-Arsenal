using FoopaArenaArsenal.Content.Projectiles;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargeSpongeBase : ModItem
    {
        protected abstract string ItemName { get; }
        protected abstract string Badge { get; }
        protected abstract int SweepRadius { get; }
        protected abstract float Penalty { get; }
        protected abstract int ToolSpeed { get; }
        protected abstract ModItem ChangeInto { get; }
        protected abstract int BonusRange { get; }
        protected abstract int Rarity { get; }
        protected abstract int Price { get; }

        protected abstract int LiquidType { get; }
        
        private string LiquidTypeText => LiquidType switch
        {
            0 => "water",
            1 => "lava",
            2 => "honey",
            _ => ""
        };
    
        public virtual int Sweep => 1 + 2 * SweepRadius;

        public static SoundStyle Splash1 = new SoundStyle("Terraria/Sounds/Splash_1");

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeSponge/{ItemName}";

        public override void SetDefaults()
        {
            float sweepPenalty = 1 + (Penalty * SweepRadius);

            Item.attackSpeedOnlyAffectsWeaponAnimation = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.useTurn = true;

            Item.tileBoost = BonusRange;
            Item.rare = Rarity;
            Item.value = Price;

            Item.useTime = (int)(ToolSpeed * sweepPenalty);
            Item.useAnimation = (int)(ToolSpeed * sweepPenalty);
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            if (SweepRadius != 0)
            {
                int previewType = ModContent.ProjectileType<LargeGridPreviewProj>();

                if (player.ownedProjectileCounts[previewType] == 0)
                {
                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        player.Center,
                        Vector2.Zero,
                        previewType,
                        0,
                        0f,
                        player.whoAmI);
                }
            }
        }

        public override bool CanRightClick() => true;

        public override bool AltFunctionUse(Player player) => true;

        public override bool ConsumeItem(Player player) => false;

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

        public override void RightClick(Player player)
        {
            ChangeMode(player);
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                ChangeMode(player);
                return false;
            }

            return true;
        }

        public bool IsTileInReach(Player player, int tileX, int tileY)
        {
            bool inRangeX = player.position.X / 16f - Player.tileRangeX - Item.tileBoost <= tileX &&
                            (player.position.X + player.width) / 16f + Player.tileRangeX + Item.tileBoost - 1f >= tileX;

            bool inRangeY = player.position.Y / 16f - Player.tileRangeY - Item.tileBoost <= tileY &&
                            (player.position.Y + player.height) / 16f + Player.tileRangeY + Item.tileBoost - 2f >= tileY;

            return inRangeX && inRangeY;
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

                    if (tile == null || (tile.HasTile && Main.tileSolid[tile.TileType]) || tile.LiquidAmount <= 0 || tile.LiquidType != LiquidType)
                        continue;

                    WorldGen.EmptyLiquid(x, y);
                    for (int a = 1; a <= 5; a++)
                    {
                        if (WorldGen.InWorld(x - a, y))
                        {
                            Tile leftAdjTile = Main.tile[x - a, y];
                            if (leftAdjTile != null && !(leftAdjTile.HasTile && Main.tileSolid[leftAdjTile.TileType]) && leftAdjTile.LiquidAmount < 255 && leftAdjTile.LiquidAmount > 0 && leftAdjTile.LiquidType == LiquidType)
                                WorldGen.EmptyLiquid(x - a, y);
                            else
                                break;
                        }
                    }
                    for (int a = 0; a <= 5; a++)
                    {
                        if (WorldGen.InWorld(x + a, y))
                        {
                            Tile rightAdjTile = Main.tile[x + a, y];
                            if (rightAdjTile != null && !(rightAdjTile.HasTile && Main.tileSolid[rightAdjTile.TileType]) && rightAdjTile.LiquidAmount < 255 && rightAdjTile.LiquidAmount > 0 && rightAdjTile.LiquidType == LiquidType)
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

        public static void HandleLargeSpongeSound(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket relay = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                relay.Write((byte)FoopaArenaArsenal.PacketType.LargeSpongeSound);
                relay.Write(x);
                relay.Write(y);
                relay.Send(-1, whoAmI);
            }
            else
            {
                SoundEngine.PlaySound(Splash1, new Vector2(x * 16, y * 16));
            }
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeSponge/{ItemName}";
            Texture2D baseTexture = ModContent.Request<Texture2D>(basePath, AssetRequestMode.ImmediateLoad).Value;
            Rectangle baseFrame = new Rectangle(0, 0, baseTexture.Width, baseTexture.Height);
            Vector2 baseOrigin = new Vector2(baseTexture.Width / 2f, baseTexture.Height / 2f);

            spriteBatch.Draw(baseTexture, position, baseFrame, drawColor, 0f, baseOrigin, scale, SpriteEffects.None, 0f);

            Player player = Main.LocalPlayer;

            bool isCursorIcon = Item == Main.mouseItem || Item == ContentSamples.ItemsByType[Type];

            if (!isCursorIcon && Badge != "") 
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

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var itemTooltip = new TooltipLine(
                Mod,
                "LargeSponge",
                $"Capable of soaking up MORE endless {LiquidTypeText}" +
                "\nRight Click to toggle sweep size"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    public class LargeWaterSpongeDev : LargeSpongeBase
    {
        protected override string ItemName => "LargeWaterSpongeDev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWaterSponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 0;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeWaterSponge1 : LargeSpongeBase
    {
        protected override string ItemName => "LargeWaterSponge1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWaterSponge2>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 0;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeWaterSponge2 : LargeSpongeBase
    {
        protected override string ItemName => "LargeWaterSponge2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWaterSponge3>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 0;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeWaterSponge3 : LargeSpongeBase
    {
        protected override string ItemName => "LargeWaterSponge3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWaterSponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 0;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeLavaSpongeDev : LargeSpongeBase
    {
        protected override string ItemName => "LargeLavaSpongeDev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeLavaSponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 1;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeLavaSponge1 : LargeSpongeBase
    {
        protected override string ItemName => "LargeLavaSponge1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeLavaSponge2>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 1;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeLavaSponge2 : LargeSpongeBase
    {
        protected override string ItemName => "LargeLavaSponge2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeLavaSponge3>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 1;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeLavaSponge3 : LargeSpongeBase
    {
        protected override string ItemName => "LargeLavaSponge3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeLavaSponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 1;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHoneySpongeDev : LargeSpongeBase
    {
        protected override string ItemName => "LargeHoneySpongeDev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHoneySponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 2;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHoneySponge1 : LargeSpongeBase
    {
        protected override string ItemName => "LargeHoneySponge1";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHoneySponge2>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 2;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHoneySponge2 : LargeSpongeBase
    {
        protected override string ItemName => "LargeHoneySponge2";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHoneySponge3>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 2;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHoneySponge3 : LargeSpongeBase
    {
        protected override string ItemName => "LargeHoneySponge3";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 10;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHoneySponge1>();
        protected override int BonusRange => 6;
        protected override int LiquidType => 2;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }
}