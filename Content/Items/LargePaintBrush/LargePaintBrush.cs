using FoopaArenaArsenal.Common.Systems;
using FoopaArenaArsenal.Content.Projectiles;
using FoopaArenaArsenal.Content.Players;
using ReLogic.Content;
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
    public abstract class LargePaintBrushBase : ModItem
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

        protected abstract float PaintCoatingConsumptionChance { get; }
        protected abstract ModItem AltChangeInto { get; }

        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargePaintBrush/{ItemName}";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            float sweepPenalty = 1 + (Penalty * SweepRadius);

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

        public bool IsTileInReach(Player player, int tileX, int tileY)
        {
            bool inRangeX = player.position.X / 16f - Player.tileRangeX - Item.tileBoost <= tileX &&
                            (player.position.X + player.width) / 16f + Player.tileRangeX + Item.tileBoost - 1f >= tileX;

            bool inRangeY = player.position.Y / 16f - Player.tileRangeY - Item.tileBoost <= tileY &&
                            (player.position.Y + player.height) / 16f + Player.tileRangeY + Item.tileBoost - 2f >= tileY;

            return inRangeX && inRangeY;
        }

        private (Dictionary<int, int>, int, bool) AttemptPainting(Player player, int centerX, int centerY, List<int> coatingIndices, int paintIndex)
        {
            int offset = Sweep / 2;

            bool anyTileModified = false;

            Dictionary<int, int> coatingQuantities = new Dictionary<int, int>();
            foreach (int i in coatingIndices)
                coatingQuantities[i] = player.inventory[i].stack;
            Item paintItem = null;
            int paintQuantity = 0;
            if (paintIndex != -1)
            {
                paintItem = player.inventory[paintIndex];
                paintQuantity = paintItem.stack;
            }

            for (int dx = -offset; dx <= offset; dx++)
            {
                for (int dy = -offset; dy <= offset; dy++)
                {   
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile == null || !tile.HasTile)
                        continue;

                    bool blockDamaged = false;
                    if (!TileLoader.CanKillTile(x, y, tile.TileType, ref blockDamaged))
                        continue;
                    
                    bool consumePaintCoating = Main.rand.NextFloat() < (1f - PaintCoatingConsumptionChance);
                    if (coatingIndices.Count > 0)
                    {
                        foreach (int coatingIndex in coatingIndices)
                        {
                            Item coatingItem = player.inventory[coatingIndex];

                            if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                                continue;

                            if (coatingItem.type == ItemID.GlowPaint && !tile.IsTileFullbright)
                            {
                                if (WorldGen.paintCoatTile(x, y, PaintCoatingID.Glow, true))
                                {
                                    anyTileModified = true;
                                    if (consumePaintCoating)
                                        coatingQuantities[coatingIndex]--;
                                }
                            }
                            else if (coatingItem.type == ItemID.EchoCoating && !tile.IsTileInvisible)
                            {
                                if (WorldGen.paintCoatTile(x, y, PaintCoatingID.Echo, true))
                                {
                                    anyTileModified = true;
                                    if (consumePaintCoating)
                                        coatingQuantities[coatingIndex]--;
                                }
                            }
                        }
                    }

                    if (paintIndex != -1 && paintQuantity > 0)
                    {
                        if (WorldGen.paintTile(x, y, paintItem.paint, true))
                        {
                            anyTileModified = true;
                            if (consumePaintCoating)
                                paintQuantity--;
                        }
                    }
                }
            }

            /*
            if (anyTileModified)
            {
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep + 1);
            }
            */

            return (coatingQuantities, paintQuantity, anyTileModified);
        }

        private static (List<int>, int) ScanInventory(Player player)
        {
            List<int> coatingIndices = new List<int>();
            int paintIndex = -1;
            bool coating1 = false;
            bool coating2 = false;

            for (int i = 54; i < 58; i++)
            {
                Item item = player.inventory[i];

                if (item == null || item.IsAir)
                    continue;

                if (item.paintCoating == 1 && !coating1 && item.favorited)
                {
                    coatingIndices.Add(i);
                    coating1 = true;
                    continue;
                }

                if (item.paintCoating == 2 && !coating2 && item.favorited)
                {
                    coatingIndices.Add(i);
                    coating2 = true;
                    continue;
                }

                if (item.paint > 0 && paintIndex == -1 && item.favorited)
                {
                    paintIndex = i;
                    continue;
                }
            }

            for (int i = 0; i < 50; i++)
            {
                Item item = player.inventory[i];

                if (item == null || item.IsAir)
                    continue;

                if (item.paint > 0 && paintIndex == -1 && item.favorited)
                {
                    paintIndex = i;
                    continue;
                }

                if (item.paintCoating == 1 && !coating1 && item.favorited)
                {
                    coatingIndices.Add(i);
                    coating1 = true;
                    continue;
                }

                if (item.paintCoating == 2 && !coating2 && item.favorited)
                {
                    coatingIndices.Add(i);
                    coating2 = true;
                    continue;
                }
            }

            return (coatingIndices, paintIndex);
        }

        public override bool? UseItem(Player player)
        {
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            if (FAAToolsPlayer.LargePaintBrushDisableCooldown > 0 || !player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            (List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            if (coatingIndices.Count == 0 && paintIndex == -1)
            {
                FAAToolsPlayer.LargePaintBrushDisableCooldown = 60;
                Main.playerInventory = true;
                player.itemAnimation = 0;
                player.itemTime = 0;

                return true;
            }

            if (FAAToolsPlayer.LargePaintBrushPaintTilesRequestPending)
                return true;

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                (Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = AttemptPainting(player, centerX, centerY, coatingIndices, paintIndex);
                
                if (anyTileModified)
                {
                    foreach (int i in coatingIndices)
                    {
                        player.inventory[i].stack = updatedCoatingIndicesQuantities[i];
                        if (updatedCoatingIndicesQuantities[i] == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargePaintBrushDisableCooldown = 60;
                            Main.playerInventory = true;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    if (paintIndex != -1)
                    {
                        player.inventory[paintIndex].stack = updatedPaintQuantity;
                        if (updatedPaintQuantity == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargePaintBrushDisableCooldown = 60;
                            Main.playerInventory = true;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                }  
            }
            else
            {
                FAAToolsPlayer.LargePaintBrushPaintTilesRequestPending = true;
                FAAToolsPlayer.LargePaintBrushPaintTilesRequestTimeout = 30;

                SendLargePaintBrushPaintTilesRequest(centerX, centerY, Item.type, coatingIndices, paintIndex);
            }

            return true;
        }

        public static void SendLargePaintBrushPaintTilesRequest(int centerX, int centerY, int itemType, List<int> coatingIndices, int paintIndex)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargePaintBrushPaintTilesRequest);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(itemType);
            packet.Write((byte)coatingIndices.Count);
            foreach (int i in coatingIndices)
                packet.Write((byte)i);
            packet.Write(paintIndex);
            packet.Send();
        }

        public static void SendLargePaintBrushPaintTilesGrant(int whoAmI, Dictionary<int, int> updatedCoatingIndicesQuantities, int paintIndex, int updatedPaintQuantity, bool anyTileModified, int centerX, int centerY, int Sweep)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargePaintBrushPaintTilesGrant);
            packet.Write((byte)updatedCoatingIndicesQuantities.Count);
            foreach (var (slot, quantity) in updatedCoatingIndicesQuantities)
            {
                packet.Write((byte)slot);
                packet.Write(quantity);
            }
            packet.Write(paintIndex);
            packet.Write(updatedPaintQuantity);
            packet.Write(anyTileModified);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(Sweep);
            packet.Send(whoAmI);
        }

        public static void SendLargePaintBrushPaintTilesDeny(int whoAmI)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargePaintBrushPaintTilesDeny);
            packet.Send(whoAmI);
        }

        public static void HandleLargePaintBrushPaintTilesRequest(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int claimedItemType = reader.ReadInt32();

            byte indexCount = reader.ReadByte();
            List<int> coatingIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                coatingIndices.Add(reader.ReadByte());

            int paintIndex = reader.ReadInt32();

            Player player = Main.player[whoAmI];
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            Item heldItem = player.HeldItem;
            if (heldItem == null || heldItem.IsAir || heldItem.type != claimedItemType ||
                heldItem.ModItem is not LargePaintBrushBase largePaintBrush)
            {
                SendLargePaintBrushPaintTilesDeny(whoAmI);
                return;
            }

            (Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = largePaintBrush.AttemptPainting(player, centerX, centerY, coatingIndices, paintIndex);
            if (anyTileModified)
            {
                SendLargePaintBrushPaintTilesGrant(whoAmI, updatedCoatingIndicesQuantities, paintIndex, updatedPaintQuantity, anyTileModified, centerX, centerY, largePaintBrush.Sweep);
            }
            else
                SendLargePaintBrushPaintTilesDeny(whoAmI);
        }

        public static void HandleLargePaintBrushPaintTilesGrant(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            byte indexCount = reader.ReadByte();
            Dictionary<int, int> updatedCoatingIndicesQuantities = new Dictionary<int, int>();
            for (int i = 0; i < indexCount; i++)
            {
                int slot = reader.ReadByte();
                int quantity = reader.ReadInt32();
                updatedCoatingIndicesQuantities[slot] = quantity;
            }

            int paintIndex = reader.ReadInt32();
            int updatedPaintQuantity = reader.ReadInt32();
            
            bool anyTileModified = reader.ReadBoolean();
            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int Sweep = reader.ReadInt32();

            Player player = Main.LocalPlayer;

            if (anyTileModified)
            {
                bool disable = false;
                foreach (int slot in updatedCoatingIndicesQuantities.Keys)
                {
                    player.inventory[slot].stack = updatedCoatingIndicesQuantities[slot];

                    if (player.inventory[slot].stack <= 0)
                    {
                        player.inventory[slot].TurnToAir();
                        disable = true;
                    }
                    
                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
                }
                if (paintIndex != -1)
                {
                    player.inventory[paintIndex].stack = updatedPaintQuantity;

                    if (player.inventory[paintIndex].stack <= 0)
                    {
                        player.inventory[paintIndex].TurnToAir();
                        disable = true;
                    }

                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, paintIndex);
                }

                if (disable)
                {
                    player.GetModPlayer<FAAToolsPlayer>().LargePaintBrushDisableCooldown = 60;
                    Main.playerInventory = true;
                }

                // NetMessage.SendTileSquare(-1, centerX, centerY, Sweep);
            }

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().LargePaintBrushPaintTilesRequestPending = false;
        }

        public static void HandleLargePaintBrushPaintTilesDeny(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().LargePaintBrushPaintTilesRequestPending = false;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargePaintBrush/{ItemName}";
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
            Player player = Main.LocalPlayer;

            (List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            string chanceNotConsumePaintCoatingLine = $"\n{(int)((1f - PaintCoatingConsumptionChance) * 100)}% chance to not consume paint or coatings when applied";

            string stacksLine;
            if (coatingIndices.Count == 0 && paintIndex == -1)
                stacksLine = "[c/FF6464:No favorited paint or coatings found]";
            else
            {
                stacksLine = "[c/64FF64:Using :]";
                if (paintIndex != -1)
                {
                    Item paintStack = player.inventory[paintIndex];
                    stacksLine += $" [i/s{paintStack.stack}:{paintStack.type}]";
                }
                for (int n = 0; n < coatingIndices.Count; n++)
                {
                    Item stack = player.inventory[coatingIndices[n]];

                    if (n > 0 && n % 10 == 0)
                        stacksLine += "\n";

                    stacksLine += $" [i/s{stack.stack}:{stack.type}]";
                }
            }

            bool isSpectre = ItemName.Length >= 2 && ItemName[ItemName.Length - 2] == '2';

            int brushIcon = isSpectre ? ItemID.SpectrePaintbrush : ItemID.Paintbrush;
            int rollerIcon = isSpectre ? ItemID.SpectrePaintRoller : ItemID.PaintRoller;
            int scraperIcon = isSpectre ? ItemID.SpectrePaintScraper : ItemID.PaintScraper;

            var itemTooltip = new TooltipLine(
                Mod,
                "LargePaintBrushInfo",
                "Applies the first favorited paint and all favorited coatings on tiles in the cursor grid" +
                $"\n{stacksLine}" +
                $"{(PaintCoatingConsumptionChance != 0 ? chanceNotConsumePaintCoatingLine : "")}" +
                "\nRight Click to toggle sweep size" +
                "\nAlt + Right Click to cycle through painting tools" +
                $"\n[i:{brushIcon}] [c/64FF64:Paint Brush]" +
                $"\n[i:{rollerIcon}] [c/888888:Paint Roller]" +
                $"\n[i:{scraperIcon}] [c/888888:Paint Scraper]"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }

        public Point FindSmartCursorTarget(Player player)
        {
            Point centerTile = Main.MouseWorld.ToTileCoordinates();
            Point targetTile = new Point(-1, -1);
            float minDist = -1;

            (List<int> coatingIndices, int paintIndex) = ScanInventory(player);
            if (coatingIndices.Count == 0 && paintIndex == -1)
                return targetTile;

            int minX = (int)(player.position.X / 16f) - Player.tileRangeX - Item.tileBoost - 1;
            int maxX = (int)((player.position.X + player.width) / 16f) + Player.tileRangeX + Item.tileBoost + 1;
            int minY = (int)(player.position.Y / 16f) - Player.tileRangeY - Item.tileBoost - 1;
            int maxY = (int)((player.position.Y + player.height) / 16f) + Player.tileRangeY + Item.tileBoost + 1;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!WorldGen.InWorld(x, y) || !IsTileInReach(player, x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile == null || !tile.HasTile)
                        continue;

                    int dqCount = 0;
                    dqCount += (2 - coatingIndices.Count);
                    foreach (int coatingIndex in coatingIndices)
                    {
                        Item coatingItem = player.inventory[coatingIndex];

                        if (coatingItem == null || coatingItem.IsAir || coatingItem.stack <= 0)
                        {
                            dqCount += 1;
                            continue;
                        }

                        if (coatingItem.type == ItemID.GlowPaint && tile.IsTileFullbright)
                            dqCount += 1;
                        else if (coatingItem.type == ItemID.EchoCoating && tile.IsTileInvisible)
                            dqCount += 1;
                    }
                    if (paintIndex == -1)
                        dqCount += 1;
                    else if (tile.TileColor == player.inventory[paintIndex].paint)
                        dqCount += 1;

                    if (dqCount == 3)
                        continue;

                    int dx = x - centerTile.X;
                    int dy = y - centerTile.Y;
                    int dist = dx * dx + dy * dy;

                    if (minDist == -1 || dist < minDist)
                    {
                        minDist = dist;
                        targetTile = new Point(x, y);
                    }
                }
            }

            return targetTile;
        }
    }

    public class LargePaintBrush1Dev : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush1Dev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => 0f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller11>();
    }

    public class LargePaintBrush11 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush11";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush12>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => 0f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller11>();
    }

    public class LargePaintBrush12 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush12";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush13>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => 0f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller12>();
    }

    public class LargePaintBrush13 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush13";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => 0f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller13>();
    }

    public class LargePaintBrush21 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush21";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => .5f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller21>();

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargePaintBrushTier1)
                .AddIngredient(ItemID.SpectreBar, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class LargePaintBrush22 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush22";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => .5f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller22>();
    }

    public class LargePaintBrush23 : LargePaintBrushBase
    {
        protected override string ItemName => "LargePaintBrush23";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintBrush21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float PaintCoatingConsumptionChance => .5f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintRoller23>();
    }
}