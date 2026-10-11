using FoopaArenaArsenal.Content.Players;
using FoopaArenaArsenal.Content.Projectiles;
using FoopaArenaArsenal.Content.Tiles;
using FoopaArenaArsenal.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargeWallBase : ModItem
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

        protected abstract int AnimationSpeedMultiplier { get; }
        protected abstract float PaintCoatingConsumptionChance { get; }

        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeWall/{ItemName}";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.useTurn = true;

            Item.tileBoost = BonusRange;
            Item.rare = Rarity;
            Item.value = Price;
            Item.UseSound = SoundID.Item1;

            Item.useTime = ToolSpeed;
            Item.useAnimation = ToolSpeed;
        }

        public void ApplyDynamicStats(Item item, Player player)
        {
            float sweepPenalty = MathF.Pow(1f + (Penalty * SweepRadius), 2);
            item.useTime = Math.Max(1, (int)Math.Round(ToolSpeed * sweepPenalty * MathF.Pow(player.wallSpeed, 1.5f)));
            item.useAnimation =  (int)(10 * player.wallSpeed);
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            (List<int> favoritedWallIndices, List<int> _, int _) = ScanInventory(player);
            bool hasWalls = favoritedWallIndices.Count > 0;
            Item.UseSound = hasWalls ? SoundID.Item1 : null;
            Item.useStyle = hasWalls ? ItemUseStyleID.Swing : ItemUseStyleID.HiddenAnimation;
            Item.noUseGraphic = !hasWalls;

            // float sweepPenalty = 1 + (Penalty * SweepRadius);
            // Item.useTime = Math.Max(1, (int)(ToolSpeed * sweepPenalty * player.tileSpeed));
            // Item.useAnimation = Math.Max(1, (int)(ToolSpeed * AnimationSpeedMultiplier * sweepPenalty * player.tileSpeed));

            if (SweepRadius != 0)
            {
                int previewType = ModContent.ProjectileType<LargeWallPreviewProj>();

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

            if (newItem.ModItem is LargeWallBase newLargeWall)
                newLargeWall.ApplyDynamicStats(newItem, player);

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

            if (player.GetModPlayer<FAAPlayer>().LargeWallDisableCooldown > 0)
            {
                Main.playerInventory = true;

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

        private static bool CanSupport(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Main.tile[x, y];
            if (tile == null || (!tile.HasTile && tile.WallType == WallID.None))
                return false;

            return true;
        }

        public static bool CanSupportPlacingWall(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (tile.WallType != WallID.None)
                return true;

            return CanSupport(x - 1, y) || CanSupport(x + 1, y) ||
                CanSupport(x, y - 1) || CanSupport(x, y + 1);
        }

        public static bool HasLOSPlayer(Player player, int tileX, int tileY)
        {
            Vector2 tileWorldPos = new Vector2(tileX * 16, tileY * 16);

            return Collision.CanHit(
                player.position, player.width, player.height,
                tileWorldPos, 16, 16
            );
        }

        private (Dictionary<int, int>, Dictionary<int, int>, int, bool) AttemptPlacing(Player player, int centerX, int centerY, List<int> favoritedWallIndices, List<int> coatingIndices, int paintIndex)
        {
            int offset = Sweep / 2;

            bool supportExists = false;
            for (int dx = -offset; dx <= offset; dx++)
            {
                for (int dy = -offset; dy <= offset; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y))
                        continue;

                    if (CanSupportPlacingWall(x, y))
                    {
                        supportExists = true;
                        break;
                    }
                }
            }

            if (!supportExists)
                return (null, null, -1, false);

            bool anyTileModified = false;

            Dictionary<int, int> favoritedWallQuantities = new Dictionary<int, int>();
            List<int> favoritedWallTypes = new List<int>();
            foreach (int i in favoritedWallIndices)
            {
                favoritedWallQuantities[i] = player.inventory[i].stack;
                favoritedWallTypes.Add(player.inventory[i].createWall);
            }
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

                    if (favoritedWallTypes.Contains(tile.WallType))
                        continue;

                    int rolledIndex = favoritedWallIndices[Main.rand.Next(favoritedWallIndices.Count)];
                    Item selectedStack = player.inventory[rolledIndex];
                    
                    if (selectedStack == null || selectedStack.IsAir || favoritedWallQuantities[rolledIndex] <= 0)
                        continue;

                    int wallType = selectedStack.createWall;

                    if (!WallLoader.CanPlace(x, y, wallType))
                        continue;

                    if (Main.SmartCursorWanted)
                    {
                        if (!HasLOSPlayer(player, x, y))
                            continue;
                    }

                    if (player.builderAccStatus[10] != 0)
                    {
                        if (tile.WallType != WallID.None)
                            continue;
                        WorldGen.PlaceWall(x, y, ModContent.WallType<DummyWall>(), mute: true);
                        if (tile.WallType == ModContent.WallType<DummyWall>())
                        {
                            for (int a = 0; a < 2; a++)
                            {
                                if (tile.WallType != WallID.None)
                                    player.PickWall(x, y, 1000);
                            }

                            if (tile.WallType == ModContent.WallType<DummyWall>())
                            {
                                tile.WallType = WallID.None;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var faaPlayer = player.GetModPlayer<FAAPlayer>();
                        if (tile.WallType != WallID.None && faaPlayer.BestHammerItem != null)
                        {
                            for (int a = 0; a < 2; a++)
                            {
                                if (tile.WallType != WallID.None)
                                    player.PickWall(x, y, faaPlayer.BestHammerItem.hammer);
                            }

                            if (tile.WallType != WallID.None)
                                continue;
                        }
                    }

                    WorldGen.PlaceWall(x, y, wallType, mute: true);

                    if (tile.WallType != wallType)
                        continue;

                    anyTileModified = true;
                    favoritedWallQuantities[rolledIndex]--;

                    bool consumePaintCoating = Main.rand.NextFloat() < (1f - PaintCoatingConsumptionChance);
                    if (coatingIndices.Count > 0)
                    {
                        foreach (int coatingIndex in coatingIndices)
                        {
                            Item coatingItem = player.inventory[coatingIndex];

                            if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                                continue;

                            if (coatingItem.type == ItemID.GlowPaint && !tile.IsWallFullbright)
                            {
                                tile.IsWallFullbright = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                            else if (coatingItem.type == ItemID.EchoCoating && !tile.IsWallInvisible)
                            {
                                tile.IsWallInvisible = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                        }
                    }

                    if (paintIndex != -1)
                    {
                        if (player.builderAccStatus[3] == 0 && player.autoPaint && paintQuantity > 0 &&
                            tile.WallColor != paintItem.paint)
                        {
                            WorldGen.paintWall(x, y, paintItem.paint);

                            if (consumePaintCoating)
                                paintQuantity--;
                        }
                    }
                }
            }

            if (anyTileModified)
            {
                for (int dx = -offset - 1; dx <= offset + 1; dx++)
                {
                    for (int dy = -offset - 1; dy <= offset + 1; dy++)
                    {
                        int x = centerX + dx;
                        int y = centerY + dy;

                        if (!WorldGen.InWorld(x, y))
                            continue;

                        if (Main.tile[x, y].WallType != WallID.None)
                            WorldGen.SquareWallFrame(x, y);
                    }
                }

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep + 1);
            }

            return (favoritedWallQuantities, coatingQuantities, paintQuantity, anyTileModified);
        }

        private static (List<int>, List<int>, int) ScanInventory(Player player)
        {
            List<int> favoritedWallIndices = new List<int>();
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

                if (item.createWall != -1 && item.favorited)
                {
                    favoritedWallIndices.Add(i);
                    continue;
                }
            }

            return (favoritedWallIndices, coatingIndices, paintIndex);
        }

        public override bool? UseItem(Player player)
        {
            var faaPlayer = player.GetModPlayer<FAAPlayer>();

            if (faaPlayer.LargeWallDisableCooldown > 0 || !player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            (List<int> favoritedWallIndices, List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            if (favoritedWallIndices.Count == 0)
            {
                faaPlayer.LargeWallDisableCooldown = 60;
                Main.playerInventory = true;
                player.itemAnimation = 0;
                player.itemTime = 0;

                return true;
            }

            if (faaPlayer.LargeWallPlaceWallsRequestPending)
                return true;

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                (Dictionary<int, int> updatedFavoritedWallIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = AttemptPlacing(player, centerX, centerY, favoritedWallIndices, coatingIndices, paintIndex);
                
                if (anyTileModified)
                {
                    foreach (int i in favoritedWallIndices)
                    {
                        player.inventory[i].stack = updatedFavoritedWallIndicesQuantities[i];
                        if (updatedFavoritedWallIndicesQuantities[i] == 0)
                        {
                            player.GetModPlayer<FAAPlayer>().LargeWallDisableCooldown = 60;
                            Main.playerInventory = true;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    foreach (int i in coatingIndices)
                    {
                        player.inventory[i].stack = updatedCoatingIndicesQuantities[i];
                        if (updatedCoatingIndicesQuantities[i] == 0)
                        {
                            player.GetModPlayer<FAAPlayer>().LargeWallDisableCooldown = 60;
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
                            player.GetModPlayer<FAAPlayer>().LargeWallDisableCooldown = 60;
                            Main.playerInventory = true;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    
                    SoundEngine.PlaySound(SoundID.Dig, player.Center);
                }  
            }
            else
            {
                faaPlayer.LargeWallPlaceWallsRequestPending = true;
                // faaPlayer.LargeWallPlaceWallsRequestCooldown = 5;
                faaPlayer.LargeWallPlaceWallsRequestTimeout = 30;

                SendLargeWallPlaceWallsRequest(centerX, centerY, Item.type, favoritedWallIndices, coatingIndices, paintIndex);
            }

            return true;
        }

        public static void HandleLargeWallSound(BinaryReader reader, int whoAmI)
        {
            float x = reader.ReadInt32();
            float y = reader.ReadInt32();

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
        }

        public static void SendLargeWallPlaceWallsRequest(int centerX, int centerY, int itemType, List<int> favoritedWallIndices, List<int> coatingIndices, int paintIndex)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeWallPlaceWallsRequest);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(itemType);
            packet.Write((byte)favoritedWallIndices.Count);
            foreach (int i in favoritedWallIndices)
                packet.Write((byte)i);
            packet.Write((byte)coatingIndices.Count);
            foreach (int i in coatingIndices)
                packet.Write((byte)i);
            packet.Write(paintIndex);
            packet.Send();
        }

        public static void SendLargeWallPlaceWallsGrant(int whoAmI, Dictionary<int, int> updatedFavoritedWallIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int paintIndex, int updatedPaintQuantity, bool anyTileModified, int centerX, int centerY, int Sweep)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeWallPlaceWallsGrant);
            packet.Write((byte)updatedFavoritedWallIndicesQuantities.Count);
            foreach (var (slot, quantity) in updatedFavoritedWallIndicesQuantities)
            {
                packet.Write((byte)slot);
                packet.Write(quantity);
            }
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

        public static void SendLargeWallPlaceWallsDeny(int whoAmI)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeWallPlaceWallsDeny);
            packet.Send(whoAmI);
        }

        public static void HandleLargeWallPlaceWallsRequest(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int claimedItemType = reader.ReadInt32();

            byte indexCount = reader.ReadByte();
            List<int> favoritedWallIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                favoritedWallIndices.Add(reader.ReadByte());

            indexCount = reader.ReadByte();
            List<int> coatingIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                coatingIndices.Add(reader.ReadByte());

            int paintIndex = reader.ReadInt32();

            Player player = Main.player[whoAmI];
            var faaPlayer = player.GetModPlayer<FAAPlayer>();

            /*
            if (faaPlayer.LargeWallPlaceWallsRequestCooldown > 0)
            {
                SendLargeWallPlaceWallsDeny(whoAmI);
                return;
            }
            */

            Item heldItem = player.HeldItem;
            if (heldItem == null || heldItem.IsAir || heldItem.type != claimedItemType ||
                heldItem.ModItem is not LargeWallBase largeWall)
            {
                SendLargeWallPlaceWallsDeny(whoAmI);
                return;
            }

            (Dictionary<int, int> updatedFavoritedWallIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = largeWall.AttemptPlacing(player, centerX, centerY, favoritedWallIndices, coatingIndices, paintIndex);
            if (anyTileModified)
            {
                ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                packet.Write((byte)FoopaArenaArsenal.PacketType.LargeWallSound);
                packet.Write(centerX);
                packet.Write(centerY);
                packet.Send();

                SendLargeWallPlaceWallsGrant(whoAmI, updatedFavoritedWallIndicesQuantities, updatedCoatingIndicesQuantities, paintIndex, updatedPaintQuantity, anyTileModified, centerX, centerY, largeWall.Sweep);
            }
            else
                SendLargeWallPlaceWallsDeny(whoAmI);
        }

        public static void HandleLargeWallPlaceWallsGrant(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            byte indexCount = reader.ReadByte();
            Dictionary<int, int> updatedFavoritedWallIndicesQuantities = new Dictionary<int, int>();
            for (int i = 0; i < indexCount; i++)
            {
                int slot = reader.ReadByte();
                int quantity = reader.ReadInt32();
                updatedFavoritedWallIndicesQuantities[slot] = quantity;
            }

            indexCount = reader.ReadByte();
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
                foreach (int slot in updatedFavoritedWallIndicesQuantities.Keys)
                {
                    player.inventory[slot].stack = updatedFavoritedWallIndicesQuantities[slot];

                    if (player.inventory[slot].stack <= 0)
                    {
                        player.inventory[slot].TurnToAir();
                        disable = true;
                    }
                    
                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
                }
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
                    player.GetModPlayer<FAAPlayer>().LargeWallDisableCooldown = 60;
                    Main.playerInventory = true;
                }

                int offset = Sweep / 2;

                for (int dx = -offset - 1; dx <= offset + 1; dx++)
                {
                    for (int dy = -offset - 1; dy <= offset + 1; dy++)
                    {
                        int x = centerX + dx;
                        int y = centerY + dy;

                        if (!WorldGen.InWorld(x, y))
                            continue;

                        if (Main.tile[x, y].WallType != WallID.None)
                            WorldGen.SquareWallFrame(x, y);
                    }
                }

                NetMessage.SendTileSquare(-1, centerX, centerY, Sweep);
            }

            Main.LocalPlayer.GetModPlayer<FAAPlayer>().LargeWallPlaceWallsRequestPending = false;
        }

        public static void HandleLargeWallPlaceWallsDeny(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Main.LocalPlayer.GetModPlayer<FAAPlayer>().LargeWallPlaceWallsRequestPending = false;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeWall/{ItemName}";
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

            (List<int> favoritedWallIndices, List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            string chanceNotConsumePaintCoatingLine = $"\n{(int)((1f - PaintCoatingConsumptionChance) * 100)}% chance to not consume paint or coatings when applied";

            string stacksLine;
            if (favoritedWallIndices.Count == 0)
                stacksLine = "[c/FF6464:No favorited walls found]";
            else
            {
                stacksLine = "[c/64FF64:Using:]";
                for (int n = 0; n < favoritedWallIndices.Count; n++)
                {
                    Item stack = player.inventory[favoritedWallIndices[n]];

                    if (n > 0 && n % 10 == 0)
                        stacksLine += "\n";

                    stacksLine += $" [i/s{stack.stack}:{stack.type}]";
                }
            }

            var itemTooltip = new TooltipLine(
                Mod,
                "LargeWallInfo",
                "Places a random assortment of favorited walls in the highlighted area" +
                "\nEach stack is equally likely to be selected from" +
                $"\n{stacksLine}" +
                "\nReplaces existing walls when Block Swap is enabled" +
                "\nApplies the first favorited paint and all favorited coatings when auto paint is active" +
                $"{(PaintCoatingConsumptionChance != 0 ? chanceNotConsumePaintCoatingLine : "")}" +
                "\nRight Click to toggle sweep size"
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

            (List<int> favoritedWallIndices, List<int> _, int _) = ScanInventory(player);
            if (favoritedWallIndices.Count == 0)
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

                    if (tile.HasTile || tile.WallType != WallID.None || !LargeWallBase.HasLOSPlayer(player, x, y) || !LargeWallBase.CanSupportPlacingWall(x, y))
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

    public class LargeWall1Dev : LargeWallBase
    {
        protected override string ItemName => "LargeWall1Dev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall11>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;
    }

    public class LargeWall11 : LargeWallBase
    {
        protected override string ItemName => "LargeWall11";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall12>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    public class LargeWall12 : LargeWallBase
    {
        protected override string ItemName => "LargeWall12";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall13>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    public class LargeWall13 : LargeWallBase
    {
        protected override string ItemName => "LargeWall13";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall11>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    public class LargeWall21 : LargeWallBase
    {
        protected override string ItemName => "LargeWall21";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargeWallTier1)
                .AddIngredient(ItemID.SpectreBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class LargeWall22 : LargeWallBase
    {
        protected override string ItemName => "LargeWall22";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);
        
        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;
    }

    public class LargeWall23 : LargeWallBase
    {
        protected override string ItemName => "LargeWall23";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .25f;
        protected override int ToolSpeed => 4;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeWall21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;
    }
}