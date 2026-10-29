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
    public abstract class LargeTileBase : ModItem
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

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeTile/{ItemName}";

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
            // float sweepPenalty = MathF.Pow(1f + (Penalty * SweepRadius), 2);
            float sweepPenalty = 1f + (Penalty * (SweepRadius * 3.5f));
            item.useTime = Math.Max(1, (int)Math.Round(ToolSpeed * sweepPenalty * MathF.Pow(player.tileSpeed, 1f)));
            item.useAnimation = (int)(10 * player.tileSpeed * sweepPenalty * .5f);
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            (List<int> favoritedTileIndices, List<int> _, int _, int _) = ScanInventory(player);
            bool hasTiles = favoritedTileIndices.Count > 0;
            Item.UseSound = hasTiles ? SoundID.Item1 : null;
            Item.useStyle = hasTiles ? ItemUseStyleID.Swing : ItemUseStyleID.HiddenAnimation;
            Item.noUseGraphic = !hasTiles;

            if (SweepRadius != 0)
            {
                int previewType = ModContent.ProjectileType<LargeTilePreviewProj>();

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

            if (newItem.ModItem is LargeTileBase newLargeTile)
                newLargeTile.ApplyDynamicStats(newItem, player);

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

            if (player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown > 0)
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
            
            return Main.tileSolid[tile.TileType] || (Main.tileSolidTop[tile.TileType] && !Main.tileFrameImportant[tile.TileType]) || tile.WallType != WallID.None;
        }

        public static bool IsEffectivelyEmpty(Player player, int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return true;
 
            ushort tileType = tile.TileType;
 
            if (TileID.Sets.BreakableWhenPlacing[tileType])
                return true;
 
            if (Main.tileCut[tileType])
                return true;

            if (player.builderAccStatus[10] == 0 && (Main.tileSolid[tile.TileType] || (Main.tileSolidTop[tile.TileType] && !Main.tileFrameImportant[tile.TileType])) && WorldGen.CanKillTile(x, y))
                return true;
 
            return false;
        }

        public static bool CanSupportPlacingTile(Player player, int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!IsEffectivelyEmpty(player, x, y))
                return false;

            if (tile.WallType != WallID.None)
                return true;

            return CanSupport(x - 1, y) || CanSupport(x + 1, y) ||
                CanSupport(x, y - 1) || CanSupport(x, y + 1);
        }

        private (Dictionary<int, int>, Dictionary<int, int>, int, int, bool) AttemptPlacing(Player player, int centerX, int centerY, List<int> favoritedTileIndices, List<int> coatingIndices, int paintIndex, int actuatorIndex)
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

                    if (CanSupportPlacingTile(player, x, y))
                    {
                        supportExists = true;
                        break;
                    }
                }
            }

            if (!supportExists)
                return (null, null, -1, -1, false);

            bool anyTileModified = false;

            Dictionary<int, int> favoritedTileQuantities = new Dictionary<int, int>();
            List<int> favoritedTileTypes = new List<int>();
            foreach (int i in favoritedTileIndices)
            {
                favoritedTileQuantities[i] = player.inventory[i].stack;
                favoritedTileTypes.Add(player.inventory[i].createTile);
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
            int actuatorQuantity = 0;
            if (actuatorIndex != -1)
                actuatorQuantity = player.inventory[actuatorIndex].stack;

            for (int dx = -offset; dx <= offset; dx++)
            {
                for (int dy = -offset; dy <= offset; dy++)
                {   
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && favoritedTileTypes.Contains(tile.TileType) && !Main.tileAxe[tile.TileType] && !Main.tileHammer[tile.TileType])
                        continue;

                    int rolledIndex = favoritedTileIndices[Main.rand.Next(favoritedTileIndices.Count)];
                    Item selectedStack = player.inventory[rolledIndex];

                    if (selectedStack == null || selectedStack.IsAir || favoritedTileQuantities[rolledIndex] <= 0)
                        continue;

                    int tileType = selectedStack.createTile;

                    if (!TileLoader.CanPlace(x, y, tileType))
                        continue;

                    if (player.builderAccStatus[10] != 0)
                    {
                        if (!IsEffectivelyEmpty(player, x, y))
                            continue;

                        WorldGen.PlaceTile(x, y, ModContent.TileType<DummyTile>(), mute: true);

                        if (tile.HasTile && tile.TileType == ModContent.TileType<DummyTile>())
                        {
                            for (int a = 0; a < 2; a++)
                            {
                                if (tile.HasTile)
                                    player.PickTile(x, y, 1000);
                            }

                            if (tile.HasTile && tile.TileType == ModContent.TileType<DummyTile>())
                            {
                                tile.HasTile = false;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

                        if (!IsEffectivelyEmpty(player, x, y))
                            continue;

                        if (tile.HasTile && !Main.tileCut[tile.TileType] && !TileID.Sets.BreakableWhenPlacing[tile.TileType])
                        {
                            if (FAAToolsPlayer.BestPickItem == null || !WorldGen.CanKillTile(x, y))
                                continue;

                            int simX = Main.maxTilesX - 5;
                            int simY = 5;
                            int simType = tile.TileType;

                            Tile simTile = Main.tile[simX, simY];

                            WorldGen.PlaceTile(simX, simY, simType);

                            Tile tmpTile = Main.tile[simX, simY + 1];

                            WorldGen.PlaceTile(simX, simY + 1, ModContent.TileType<DummyTile>());

                            int slot = player.hitTile.TryFinding(simX, simY, 1);
                            if (slot != -1)
                                player.hitTile.Clear(slot);

                            for (int a = 0; a < 2; a++)
                            {
                                if (simTile.HasTile)
                                    player.PickTile(simX, simY, FAAToolsPlayer.BestPickItem.pick);
                            }

                            if (simTile.HasTile)
                            {
                                simTile.HasTile = false;
                                continue;
                            }
                            else
                            {
                                if (WorldGen.WouldTileReplacementWork((ushort)tileType, x, y))
                                    WorldGen.ReplaceTile(x, y, (ushort)tileType, selectedStack.placeStyle);
                                else
                                {
                                    player.PickTile(x, y, 10000);
                                    WorldGen.PlaceTile(x, y, (ushort)tileType);

                                }
                            }

                            simTile.HasTile = false;
                            tmpTile.HasTile = false;

                            Vector2 simCenter = new Vector2(simX * 16 + 8, simY * 16 + 8);
                            for (int i = 0; i < Main.maxItems; i++)
                            {
                                if (Main.item[i].active && Vector2.Distance(Main.item[i].Center, simCenter) < 64f)
                                {
                                    Main.item[i].active = false;
                                    if (Main.netMode == NetmodeID.Server)
                                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
                                }
                            }
                        }
                        else
                        {
                            WorldGen.PlaceTile(x, y, ModContent.TileType<DummyTile>(), mute: true);

                            if (tile.HasTile && tile.TileType == ModContent.TileType<DummyTile>())
                            {
                                for (int a = 0; a < 2; a++)
                                {
                                    if (tile.HasTile)
                                        player.PickTile(x, y, 1000);
                                }

                                if (tile.HasTile && tile.TileType == ModContent.TileType<DummyTile>())
                                {
                                    tile.HasTile = false;
                                    continue;
                                }
                            }
                        }
                    }

                    WorldGen.PlaceTile(x, y, tileType, mute: true);

                    if (!tile.HasTile || tile.TileType != tileType)
                        continue;

                    anyTileModified = true;
                    favoritedTileQuantities[rolledIndex]--;

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
                                tile.IsTileFullbright = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                            else if (coatingItem.type == ItemID.EchoCoating && !tile.IsTileInvisible)
                            {
                                tile.IsTileInvisible = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                        }
                    }

                    if (paintIndex != -1)
                    {
                        if (player.builderAccStatus[3] == 0 && player.autoPaint && paintQuantity > 0 &&
                            tile.TileColor != paintItem.paint)
                        {
                            WorldGen.paintTile(x, y, paintItem.paint);

                            if (consumePaintCoating)
                                paintQuantity--;
                        }
                    }

                    if (actuatorIndex != -1)
                    {
                        if (player.builderAccStatus[2] == 0 && player.autoActuator && actuatorQuantity > 0 &&
                            !tile.HasActuator)
                        {
                            WorldGen.PlaceActuator(x, y);

                            actuatorQuantity--;
                        }
                    }
                }
            }

            if (anyTileModified)
            {
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep + 1);
            }

            return (favoritedTileQuantities, coatingQuantities, paintQuantity, actuatorQuantity, anyTileModified);
        }

        private static (List<int>, List<int>, int, int) ScanInventory(Player player)
        {
            List<int> favoritedTileIndices = new List<int>();
            List<int> coatingIndices = new List<int>();
            int paintIndex = -1;
            int actuatorIndex = -1;
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

                if (item.type == ItemID.Actuator && actuatorIndex == -1)
                {
                    actuatorIndex = i;
                    continue;
                }
            }

            for (int i = 0; i < 50; i++)
            {
                Item item = player.inventory[i];

                if (item == null || item.IsAir)
                    continue;

                int tileType = item.createTile;

                if (item.createTile != -1 && item.favorited)
                {
                    bool isPlatform = TileID.Sets.Platforms[tileType];
                    // bool isPlanter = tileType == TileID.PlanterBox;

                    // if (!Main.tileFrameImportant[tileType] || isPlatform || isPlanter)
                    if (!Main.tileFrameImportant[tileType] || isPlatform)
                    {
                        favoritedTileIndices.Add(i);
                        continue;
                    }
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

                if (item.paint > 0 && paintIndex == -1 && item.favorited)
                {
                    paintIndex = i;
                    continue;
                }

                if (item.type == ItemID.Actuator && actuatorIndex == -1)
                {
                    actuatorIndex = i;
                    continue;
                }
            }

            return (favoritedTileIndices, coatingIndices, paintIndex, actuatorIndex);
        }

        public override bool? UseItem(Player player)
        {
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (Main.tile[centerX, centerY].TileType == ModContent.TileType<DummyTile>())
                WorldGen.KillTile(centerX, centerY, false, false, true);

            if (FAAToolsPlayer.LargeTileDisableCooldown > 0 || !player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            (List<int> favoritedTileIndices, List<int> coatingIndices, int paintIndex, int actuatorIndex) = ScanInventory(player);

            if (favoritedTileIndices.Count == 0)
            {
                FAAToolsPlayer.LargeTileDisableCooldown = 60;
                Main.playerInventory = true;

                return true;
            }

            if (FAAToolsPlayer.LargeTilePlaceTilesRequestPending)
                return true;

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                (Dictionary<int, int> updatedFavoritedTileIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, int updatedActuatorQuantity, bool anyTileModified) = AttemptPlacing(player, centerX, centerY, favoritedTileIndices, coatingIndices, paintIndex, actuatorIndex);
                
                if (anyTileModified)
                {
                    foreach (int i in favoritedTileIndices)
                    {
                        player.inventory[i].stack = updatedFavoritedTileIndicesQuantities[i];
                        if (updatedFavoritedTileIndicesQuantities[i] == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown = 60;
                            Main.playerInventory = true;
                        }
                    }
                    foreach (int i in coatingIndices)
                    {
                        player.inventory[i].stack = updatedCoatingIndicesQuantities[i];
                        if (updatedCoatingIndicesQuantities[i] == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown = 60;
                            Main.playerInventory = true;
                        }
                    }
                    if (paintIndex != -1)
                    {
                        player.inventory[paintIndex].stack = updatedPaintQuantity;
                        if (updatedPaintQuantity == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown = 60;
                            Main.playerInventory = true;
                        }
                    }
                    if (actuatorIndex != -1)
                    {
                        player.inventory[actuatorIndex].stack = updatedActuatorQuantity;
                        if (updatedActuatorQuantity == 0)
                        {
                            player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown = 60;
                            Main.playerInventory = true;
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Dig, player.Center);
                }  
            }
            else
            {
                FAAToolsPlayer.LargeTilePlaceTilesRequestPending = true;
                // FAAToolsPlayer.LargeTilePlaceTilesRequestCooldown = 5; // ~ 1/12 second
                FAAToolsPlayer.LargeTilePlaceTilesRequestTimeout = 30; // ~ 1/2 second

                SendLargeTilePlaceTilesRequest(centerX, centerY, Item.type, favoritedTileIndices, coatingIndices, paintIndex, actuatorIndex);
            }

            return true;
        }

        public static void HandleLargeTileSound(BinaryReader reader, int whoAmI)
        {
            float x = reader.ReadInt32();
            float y = reader.ReadInt32();

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
        }

        public static void SendLargeTilePlaceTilesRequest(int centerX, int centerY, int itemType, List<int> favoritedTileIndices, List<int> coatingIndices, int paintIndex, int actuatorIndex)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeTilePlaceTilesRequest);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(itemType);
            packet.Write((byte)favoritedTileIndices.Count);
            foreach (int i in favoritedTileIndices)
                packet.Write((byte)i);
            packet.Write((byte)coatingIndices.Count);
            foreach (int i in coatingIndices)
                packet.Write((byte)i);
            packet.Write(paintIndex);
            packet.Write(actuatorIndex);
            packet.Send();
        }

        public static void SendLargeTilePlaceTilesGrant(int whoAmI, Dictionary<int, int> updatedFavoritedTileIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int paintIndex, int updatedPaintQuantity, int actuatorIndex, int updatedActuatorQuantity, bool anyTileModified, int centerX, int centerY, int Sweep)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeTilePlaceTilesGrant);
            packet.Write((byte)updatedFavoritedTileIndicesQuantities.Count);
            foreach (var (slot, quantity) in updatedFavoritedTileIndicesQuantities)
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
            packet.Write(actuatorIndex);
            packet.Write(updatedActuatorQuantity);
            packet.Write(anyTileModified);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(Sweep);
            packet.Send(whoAmI);
        }

        public static void SendLargeTilePlaceTilesDeny(int whoAmI)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeTilePlaceTilesDeny);
            packet.Send(whoAmI);
        }

        public static void HandleLargeTilePlaceTilesRequest(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int claimedItemType = reader.ReadInt32();

            byte indexCount = reader.ReadByte();
            List<int> favoritedTileIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                favoritedTileIndices.Add(reader.ReadByte());

            indexCount = reader.ReadByte();
            List<int> coatingIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                coatingIndices.Add(reader.ReadByte());

            int paintIndex = reader.ReadInt32();

            int actuatorIndex = reader.ReadInt32();

            Player player = Main.player[whoAmI];
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            /*
            if (FAAToolsPlayer.LargeTilePlaceTilesRequestCooldown > 0)
            {
                SendLargeTilePlaceTilesDeny(whoAmI);
                return;
            }
            */

            Item heldItem = player.HeldItem;
            if (heldItem == null || heldItem.IsAir || heldItem.type != claimedItemType ||
                heldItem.ModItem is not LargeTileBase LargeTile)
            {
                SendLargeTilePlaceTilesDeny(whoAmI);
                return;
            }

            (Dictionary<int, int> updatedFavoritedTileIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, int updatedActuatorQuantity, bool anyTileModified) = LargeTile.AttemptPlacing(player, centerX, centerY, favoritedTileIndices, coatingIndices, paintIndex, actuatorIndex);

            if (anyTileModified)
            {
                ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                packet.Write((byte)FoopaArenaArsenal.PacketType.LargeTileSound);
                packet.Write(centerX);
                packet.Write(centerY);
                packet.Send();

                SendLargeTilePlaceTilesGrant(whoAmI, updatedFavoritedTileIndicesQuantities, updatedCoatingIndicesQuantities, paintIndex, updatedPaintQuantity, actuatorIndex, updatedActuatorQuantity, anyTileModified, centerX, centerY, LargeTile.Sweep);
            }
            else
                SendLargeTilePlaceTilesDeny(whoAmI);
        }

        public static void HandleLargeTilePlaceTilesGrant(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            byte indexCount = reader.ReadByte();
            Dictionary<int, int> updatedFavoritedTileIndicesQuantities = new Dictionary<int, int>();
            for (int i = 0; i < indexCount; i++)
            {
                int slot = reader.ReadByte();
                int quantity = reader.ReadInt32();
                updatedFavoritedTileIndicesQuantities[slot] = quantity;
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

            int actuatorIndex = reader.ReadInt32();
            int updatedActuatorQuantity = reader.ReadInt32();
            
            bool anyTileModified = reader.ReadBoolean();
            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int Sweep = reader.ReadInt32();

            Player player = Main.LocalPlayer;

            if (anyTileModified)
            {
                bool disable = false;
                foreach (int slot in updatedFavoritedTileIndicesQuantities.Keys)
                {
                    player.inventory[slot].stack = updatedFavoritedTileIndicesQuantities[slot];

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
                if (actuatorIndex != -1)
                {
                    player.inventory[actuatorIndex].stack = updatedActuatorQuantity;

                    if (player.inventory[actuatorIndex].stack <= 0)
                    {
                        player.inventory[actuatorIndex].TurnToAir();
                        disable = true;
                    }

                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, actuatorIndex);
                }

                if (disable)
                {
                    player.GetModPlayer<FAAToolsPlayer>().LargeTileDisableCooldown = 60;
                    Main.playerInventory = true;
                }

                NetMessage.SendTileSquare(-1, centerX, centerY, Sweep);
            }

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().LargeTilePlaceTilesRequestPending = false;
        }

        public static void HandleLargeTilePlaceTilesDeny(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().LargeTilePlaceTilesRequestPending = false;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeTile/{ItemName}";
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

            (List<int> favoritedTileIndices, List<int> coatingIndices, int paintIndex, int actuatorIndex) = ScanInventory(player);

            string chanceNotConsumePaintCoatingLine = $"\n{(int)((1f - PaintCoatingConsumptionChance) * 100)}% chance to not consume paint or coatings when applied";

            string stacksLine;
            if (favoritedTileIndices.Count == 0)
                stacksLine = "[c/FF6464:No favorited tiles found]";
            else
            {
                stacksLine = "[c/64FF64:Using :]";
                for (int n = 0; n < favoritedTileIndices.Count; n++)
                {
                    Item stack = player.inventory[favoritedTileIndices[n]];

                    if (n > 0 && n % 10 == 0)
                        stacksLine += "\n";

                    stacksLine += $" [i/s{stack.stack}:{stack.type}]";
                }
            }

            var itemTooltip = new TooltipLine(
                Mod,
                "LargeTileInfo",
                "Places a mosaic of favorited blocks in the highlighted area" +
                "\nEach stack is equally likely to be selected from" +
                $"\n{stacksLine}" +
                "\nReplaces existing blocks when Block Swap is enabled" +
                "\nIneffective in biomes that encourage navigation over destruction" +
                "\nApplies the first favorited paint, all favorited coatings and places actuators when their auto-placement is active" +
                $"{(PaintCoatingConsumptionChance != 0 ? chanceNotConsumePaintCoatingLine : "")}" +
                "\nRight Click to toggle sweep size"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    /*
    public class LargeTile1Dev : LargeTileBase
    {
        protected override string ItemName => "LargeTile1Dev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile11>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }
    */

    public class LargeTile11 : LargeTileBase
    {
        protected override string ItemName => "LargeTile11";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile12>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    public class LargeTile12 : LargeTileBase
    {
        protected override string ItemName => "LargeTile12";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile13>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    public class LargeTile13 : LargeTileBase
    {
        protected override string ItemName => "LargeTile13";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile11>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => 0f;
    }

    /*
    public class LargeTile21 : LargeTileBase
    {
        protected override string ItemName => "LargeTile21";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargeTileTier1)
                .AddIngredient(ItemID.SpectreBar, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class LargeTile22 : LargeTileBase
    {
        protected override string ItemName => "LargeTile22";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;
    }

    public class LargeTile23 : LargeTileBase
    {
        protected override string ItemName => "LargeTile23";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => 1f;
        protected override int ToolSpeed => 3;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeTile21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override int AnimationSpeedMultiplier => 5;
        protected override float PaintCoatingConsumptionChance => .5f;
    }
    */
}