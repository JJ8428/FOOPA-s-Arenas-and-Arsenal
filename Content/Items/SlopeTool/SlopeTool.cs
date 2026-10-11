using FoopaArenaArsenal.Content.Projectiles;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class SlopeToolBase : ModItem
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

        public int CurrentSlopeType { get; set; } = 0;
        
        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/SlopeTool/{ItemName}";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

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
            Item.UseSound = SoundID.Item1;

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
                int previewType = ModContent.ProjectileType<SlopeToolPreviewProj>();

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

            if (newItem.ModItem is SlopeToolBase slopeTool)
                slopeTool.CurrentSlopeType = CurrentSlopeType;

            player.inventory[slotIndex] = newItem;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotIndex);
        }

        private void CycleSlopeType(Player player)
        {
            CurrentSlopeType = (CurrentSlopeType + 1) % 8;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < player.inventory.Length; i++)
                {
                    if (player.inventory[i] == Item)
                    {
                        NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, i);
                        break;
                    }
                }
            }
        }

        public override void RightClick(Player player)
        {
            if (AltHeld())
                CycleSlopeType(player);
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
                    CycleSlopeType(player);
                else
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

        private static void EnsureSlope(int x, int y, int slope)
        {
            Tile tile = Main.tile[x, y];
            tile.IsHalfBlock = false;
            WorldGen.SlopeTile(x, y, slope);
        }

        private static void EnsureHalfSlab(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            tile.IsHalfBlock = false;
            WorldGen.SlopeTile(x, y, 0);
            WorldGen.PoundTile(x, y);
        }

        public static bool IsRealBlock(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Framing.GetTileSafely(x, y);

            ushort type = tile.TileType;
            bool isPlatform = TileID.Sets.Platforms[type];

            if (!tile.HasTile)
                return false;

            if (Main.tileFrameImportant[type] && !isPlatform)
                return false;

            return Main.tileSolid[type] || Main.tileSolidTop[type] || isPlatform;
        }

        public static bool IsRealNaturalBlock(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Framing.GetTileSafely(x, y);

            if (!tile.HasTile)
                return false;

            if (Main.tileFrameImportant[tile.TileType])
                return false;

            return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
        }

        private static bool ApplyNaturalSlope(int x, int y, bool forced = true)
        {
            if (!WorldGen.InWorld(x, y - 1) || !WorldGen.InWorld(x, y + 1) ||
                !WorldGen.InWorld(x - 1, y) || !WorldGen.InWorld(x + 1, y))
                return false;

            bool hasAbove = IsRealNaturalBlock(x, y - 1);
            bool hasBelow = IsRealNaturalBlock(x, y + 1);
            bool hasLeft = IsRealNaturalBlock(x - 1, y);
            bool hasRight = IsRealNaturalBlock(x + 1, y);

            if (hasBelow && !hasLeft && !hasRight && !hasAbove)
            {
                switch (Main.rand.Next(2))
                {
                    case 0: EnsureHalfSlab(x, y); break;
                    case 1: EnsureSlope(x, y, 0); break;
                }
                return true;
            }

            if (hasBelow && hasLeft && !hasAbove && !hasRight)
            {
                switch (Main.rand.Next(3))
                {
                    case 0: EnsureHalfSlab(x, y); break;
                    case 1: EnsureSlope(x, y, 0); break;
                    case 2: EnsureSlope(x, y, 1); break;
                }
                return true;
            }

            if (hasBelow && hasRight && !hasAbove && !hasLeft)
            {
                switch (Main.rand.Next(3))
                {
                    case 0: EnsureHalfSlab(x, y); break;
                    case 1: EnsureSlope(x, y, 0); break;
                    case 2: EnsureSlope(x, y, 2); break;
                }
                return true;
            }

            if (hasAbove && hasLeft && !hasBelow && !hasRight)
            {
                switch (Main.rand.Next(2))
                {
                    case 0: EnsureSlope(x, y, 0); break;
                    case 1: EnsureSlope(x, y, 3); break;
                }
                return true;
            }

            if (hasAbove && hasRight && !hasBelow && !hasLeft)
            {
                switch (Main.rand.Next(2))
                {
                    case 0: EnsureSlope(x, y, 0); break;
                    case 1: EnsureSlope(x, y, 4); break;
                }
                return true;
            }

            if (forced)
            {
                EnsureSlope(x, y, 0);
                return true;
            }

            return false;
        }

        private static bool ApplyOnlySlopes(int x, int y, bool forced = true)
        {
            if (!WorldGen.InWorld(x, y - 1) || !WorldGen.InWorld(x, y + 1) ||
                !WorldGen.InWorld(x - 1, y) || !WorldGen.InWorld(x + 1, y))
                return false;

            bool hasAbove = IsRealBlock(x, y - 1);
            bool hasBelow = IsRealBlock(x, y + 1);
            bool hasLeft = IsRealBlock(x - 1, y);
            bool hasRight = IsRealBlock(x + 1, y);

            if (hasBelow && hasLeft && !hasAbove && !hasRight)
            {
                EnsureSlope(x, y, 1);
                return true;
            }

            if (hasBelow && hasRight && !hasAbove && !hasLeft)
            {
                EnsureSlope(x, y, 2);
                return true;
            }

            if (hasAbove && hasLeft && !hasBelow && !hasRight)
            {
                EnsureSlope(x, y, 3);
                return true;
            }

            if (hasAbove && hasRight && !hasBelow && !hasLeft)
            {
                EnsureSlope(x, y, 4);
                return true;
            }

            if (forced)
            {
                EnsureSlope(x, y, 0);
                return true;
            }

            return false;
        }

        private bool ApplySlope(int x, int y)
        {
            bool modified = false;
            switch (CurrentSlopeType)
            {
                case 0: EnsureSlope(x, y, 0); modified = true; break;
                case 1: EnsureHalfSlab(x, y); modified = true; break;
                case 2: EnsureSlope(x, y, 2); modified = true; break;
                case 3: EnsureSlope(x, y, 1); modified = true; break;
                case 4: EnsureSlope(x, y, 4); modified = true; break;
                case 5: EnsureSlope(x, y, 3); modified = true; break;
                case 6: modified = ApplyNaturalSlope(x, y); break;
                case 7: modified = ApplyOnlySlopes(x, y); break;
                default: break;
            }

            if (modified)
                WorldGen.SquareTileFrame(x, y);

            return modified;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
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

                    if (!WorldGen.InWorld(x, y) || !WorldGen.CanKillTile(x, y) || !WorldGen.CanPoundTile(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile == null || !tile.HasTile)
                        continue;

                    bool blockDamaged = false;
                    if (!TileLoader.CanKillTile(x, y, tile.TileType, ref blockDamaged))
                        continue;

                    ushort type = tile.TileType;

                    if (Main.tileAxe[type] || Main.tileHammer[type] || !IsRealBlock(x, y))
                        continue;

                    bool modifiedThisTile = ApplySlope(x, y);

                    if (modifiedThisTile)
                        anyTileModified = true;
                }
            }

            if (anyTileModified)
            {
                SoundEngine.PlaySound(SoundID.Dig, player.Center.ToWorldCoordinates());
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep + 1);

                    ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                    packet.Write((byte)FoopaArenaArsenal.PacketType.SlopeToolSound);
                    packet.Write(centerX);
                    packet.Write(centerY);
                    packet.Send();
                }
            }

            return true;
        }

        public static void HandleSlopeToolSound(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket relay = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                relay.Write((byte)FoopaArenaArsenal.PacketType.SlopeToolSound);
                relay.Write(x);
                relay.Write(y);
                relay.Send(-1, whoAmI);
            }
            else
            {
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
            }
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((byte)CurrentSlopeType);
        }

        public override void NetReceive(BinaryReader reader)
        {
            CurrentSlopeType = reader.ReadByte();
        }

        public override void SaveData(TagCompound tag)
        {
            tag["CurrentSlopeType"] = CurrentSlopeType;
        }

        public override void LoadData(TagCompound tag)
        {
            CurrentSlopeType = tag.GetInt("CurrentSlopeType");
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/SlopeTool/{ItemName}";
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

                Vector2 badgeOffset1 = isSelectedHotbarSlot
                    ? new Vector2(2.5f, 1.5f)
                    : new Vector2(-0.5f, -0.5f);

                spriteBatch.Draw(badge, position + badgeOffset1, badgeFrame, drawColor, 0f, badgeOrigin, 1.0f, SpriteEffects.None, 0f);

                string slopeOverlayPath = $"FoopaArenaArsenal/Content/Items/ItemUI/SlopeMode{CurrentSlopeType + 1}";
                Texture2D slopeOverlay = ModContent.Request<Texture2D>(slopeOverlayPath, AssetRequestMode.ImmediateLoad).Value;
                Rectangle slopeOverlayFrame = new Rectangle(0, 0, slopeOverlay.Width, slopeOverlay.Height);
                Vector2 slopeOverlayOrigin = new Vector2(slopeOverlay.Width / 2f, slopeOverlay.Height / 2f);

                Vector2 badgeOffset2 = isSelectedHotbarSlot
                    ? new Vector2(-3.0f, 1.5f)
                    : new Vector2(0.5f, -0.5f);

                spriteBatch.Draw(slopeOverlay, position + badgeOffset2, slopeOverlayFrame, drawColor, 0f, slopeOverlayOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            return false;
        }

        private static readonly string[] SlopeModeLabels =
        {
            "█ Full Block",
            "▄ Half Slab",
            "◢ Slope (bottom-right)",
            "◣ Slope (bottom-left)",
            "◥ Slope (top-right)",
            "◤ Slope (top-left)",
            "(Natural)",
            "(Smooth)"
        };

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string modeListLine = "";
            for (int i = 0; i < SlopeModeLabels.Length; i++)
            {
                string entry = SlopeModeLabels[i];

                if (i == CurrentSlopeType)
                    entry = $"[c/64FF64:{entry}]";
                else
                    entry = $"[c/888888:{entry}]";

                modeListLine += entry;

                if (i < SlopeModeLabels.Length - 1)
                    modeListLine += "\n";
            }

            string info =
                "Pounds tiles in the highlighted area into the selected slope" + 
                "\nRight Click to toggle sweep size" +
                "\nAlt + Right Click to cycle slope modes" +
                "\n" +
                // "\nSlope Modes:\n" +
                modeListLine;

            var itemTooltip = new TooltipLine(Mod, "LargeSlopeToolInfo", info);

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    public class SlopeTool11 : SlopeToolBase
    {
        protected override string ItemName => "SlopeTool11";
        protected override string Badge => "Bar41";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<SlopeTool12>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
    }

    public class SlopeTool12 : SlopeToolBase
    {
        protected override string ItemName => "SlopeTool12";
        protected override string Badge => "Bar42";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<SlopeTool13>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
    }

    public class SlopeTool13 : SlopeToolBase
    {
        protected override string ItemName => "SlopeTool13";
        protected override string Badge => "Bar43";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<SlopeTool14>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
    }

    public class SlopeTool14 : SlopeToolBase
    {
        protected override string ItemName => "SlopeTool14";
        protected override string Badge => "Bar44";
        protected override int SweepRadius => 3;
        protected override float Penalty => 0f;
        protected override int ToolSpeed => 12;
        protected override ModItem ChangeInto => ModContent.GetInstance<SlopeTool11>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
    }
}