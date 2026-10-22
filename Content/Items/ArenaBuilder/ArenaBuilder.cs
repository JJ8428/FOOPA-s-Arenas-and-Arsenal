using FoopaArenaArsenal.Content.Players;
using FoopaArenaArsenal.Content.Projectiles;
using FoopaArenaArsenal.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class ArenaBuilderBase : ModItem
    {
        protected abstract string ItemName { get; }
        protected abstract string Badge { get; }
        protected abstract ModItem ChangeInto { get; }
        protected abstract int BonusRange { get; }
        protected abstract int Rarity { get; }
        protected abstract int Price { get; }

        protected abstract float PaintCoatingConsumptionChance { get; }

        public bool RespectObstacles { get; set; } = false;

        public abstract int Anchor { get; }

        public override string Texture => $"FoopaArenaArsenal/Content/Items/ArenaBuilder/{ItemName}";
        public int HoldTicks = 0;
        public int HoldTileX = -1;
        public int HoldTileY = -1;
        public bool holdFired = false;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.attackSpeedOnlyAffectsWeaponAnimation = true;
            Item.useStyle = ItemUseStyleID.HiddenAnimation;
            Item.noUseGraphic = true;
            Item.autoReuse = true;

            Item.tileBoost = BonusRange;
            Item.rare = Rarity;
            Item.value = Price;
        }

        public override void HoldItem(Player player)
        {
            // Main.NewText($"{HoldTicks} ${HoldTileX} ${HoldTileY}");
            
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            int previewType = ModContent.ProjectileType<ArenaBuilderPreviewProj>();

            if (Main.mouseLeft && !player.mouseInterface && Main.mouseItem.IsAir)
            {
                if (HoldTicks == 0)
                {
                    HoldTileX = Player.tileTargetX;
                    HoldTileY = Player.tileTargetY;
                }

                HoldTicks++;
            }
            else
            {
                HoldTicks = 0;
                holdFired = false;
                HoldTileX = -1;
                HoldTileY = -1;
            }

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

            if (newItem.ModItem is ArenaBuilderBase arenaBuilder)
                arenaBuilder.RespectObstacles = RespectObstacles;

            player.inventory[slotIndex] = newItem;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotIndex);
        }

        private void ToggleRespectObstacles(Player player)
        {
            RespectObstacles = !RespectObstacles;

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
                ToggleRespectObstacles(player);
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
                    ToggleRespectObstacles(player);
                else
                    ChangeMode(player);

                return false;
            }

            if (player.whoAmI == Main.myPlayer && (HoldTicks < 60 || holdFired))
                return false;

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

        public static (int, List<(int, int)>, List<(int, int)>, List<int>, int) ScanInventory(Player player)
        {
            int favoritedTileIndex = -1;
            List<(int, int)> groundFurnitureIndices = new List<(int, int)>();
            List<(int, int)> hangingFurnitureIndices = new List<(int, int)>();
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

                if (item.ModItem is ArenaBuilderBase || item.ModItem is LargeTileBase)
                    continue;

                if (item.createTile != -1 && item.favorited && favoritedTileIndex == -1)
                {
                    int t = item.createTile;
                    TileObjectData tileData = TileObjectData.GetTileData(t, item.placeStyle);
                    if ((Main.tileSolid[t] || Main.tileSolidTop[t]) && (tileData == null || (tileData.Width == 1 && tileData.Height == 1))
                        && (item.stack >= 170 || !item.consumable))
                    {
                        favoritedTileIndex = i;
                        continue;
                    }
                }

                if (item.createTile != -1 && item.favorited)
                {
                    int tileType = item.createTile;

                    bool hasNearbyEffect = false;

                    if (tileType == TileID.Campfire ||
                        tileType == TileID.CatBast ||
                        tileType == TileID.Sunflower ||
                        tileType == TileID.GardenGnome // ||
                        // tileType == TileID.ShadowCandle ||
                        // tileType == TileID.WaterCandle ||
                        // tileType == TileID.PeaceCandle
                    )
                        hasNearbyEffect = true;

                    const int heartLanternStyle = 9;
                    const int starBottleStyle = 7;
                    if (tileType == TileID.HangingLanterns &&
                        (item.placeStyle == heartLanternStyle || item.placeStyle == starBottleStyle))
                        hasNearbyEffect = true;

                    if (!hasNearbyEffect && TileLoader.GetTile(tileType) is ModTile modTile)
                    {
                        MethodInfo nearbyEffectsMethod = modTile.GetType().GetMethod(
                            nameof(ModTile.NearbyEffects),
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null,
                            new Type[] { typeof(int), typeof(int), typeof(bool) },
                            null
                        );

                        if (nearbyEffectsMethod != null && nearbyEffectsMethod.DeclaringType != typeof(ModTile))
                            hasNearbyEffect = true;
                    }

                    if (hasNearbyEffect)
                    {
                        TileObjectData data = TileObjectData.GetTileData(tileType, 0);
                        bool isHanging = data != null && data.AnchorTop.type != AnchorType.None;

                        if (isHanging)
                            hangingFurnitureIndices.Add((i, item.placeStyle));
                        else
                            groundFurnitureIndices.Add((i, item.placeStyle));
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

                if (item.paint > 0 && paintIndex == -1)
                {
                    paintIndex = i;
                    continue;
                }
            }

            return (favoritedTileIndex, groundFurnitureIndices, hangingFurnitureIndices, coatingIndices, paintIndex);
        }

        public static bool IsEffectivelyEmpty(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return true;
 
            ushort tileType = tile.TileType;
 
            if (TileID.Sets.BreakableWhenPlacing[tileType])
                return true;
 
            if (Main.tileCut[tileType])
                return true;
 
            return false;
        }

        private (int, Dictionary<int, int>, Dictionary<int, int>, Dictionary<int, int>, int, bool) AttemptPlacing(Player player, int centerX, int centerY, int favoritedTileIndex, List<(int, int)> groundFurnitureIndices, List<(int, int)> hangingFurnitureIndices, List<int> coatingIndices, int paintIndex)
        {
            int buildWidth = 170;
            int buildHeightGapTop = 4;
            int buildHeightGapBottom = 2;
            int buildHeight = buildHeightGapTop + buildHeightGapBottom + 1;

            int topLeftTileX = Anchor switch
            {
                -1 => centerX - (buildWidth - 1),
                0 => centerX - (buildWidth / 2),
                1 => centerX,
                _ => centerX
            };
            int topLeftTileY = centerY - buildHeightGapTop;

            int gridCenterX = topLeftTileX + (buildWidth / 2);
            int floorRowY = topLeftTileY + buildHeightGapTop;

            int groundWidthTiles = 0;
            foreach ((int i, int _) in groundFurnitureIndices)
            {
                TileObjectData data = TileObjectData.GetTileData(player.inventory[i].createTile, 0);
                int width = 1;

                if (data != null)
                    width = data.Width;

                groundWidthTiles += width;
            }
            int hangingWidthTiles = 0;
            foreach ((int i, int _) in hangingFurnitureIndices)
            {
                TileObjectData data = TileObjectData.GetTileData(player.inventory[i].createTile, 0);
                int width = 1;

                if (data != null)
                    width = data.Width;

                hangingWidthTiles += width;
            }

            int forcedPlatformWidth = Math.Max(groundWidthTiles, hangingWidthTiles);
            int rightForcedPlatformWidth = (forcedPlatformWidth - 1) / 2;
            int leftForcedPlatformWidth = forcedPlatformWidth / 2;

            Item selectedStack = player.inventory[favoritedTileIndex];
            int tileType = selectedStack.createTile;
            int favoritedTileQuantity = selectedStack.stack;
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

            Dictionary<int, int> groundFurnitureQuantities = new Dictionary<int, int>();
            foreach ((int i, int _) in groundFurnitureIndices)
                groundFurnitureQuantities[i] = player.inventory[i].stack;

            Dictionary<int, int> hangingFurnitureQuantities = new Dictionary<int, int>();
            foreach ((int i, int _) in hangingFurnitureIndices)
                hangingFurnitureQuantities[i] = player.inventory[i].stack;

            int dummyTile = ModContent.TileType<DummyTile>();

            bool anyTileModified = false;

            for (int x = gridCenterX; x < topLeftTileX + buildWidth; x++)
            {
                if (!WorldGen.InWorld(x, floorRowY) || (RespectObstacles && !IsEffectivelyEmpty(x, floorRowY) && x - gridCenterX > rightForcedPlatformWidth))
                    break;

                if (Main.tile[x, floorRowY].TileType == dummyTile)
                {
                    Tile tmp_tile = Main.tile[x, floorRowY];
                    tmp_tile.HasTile = false;
                }
                else if (!IsEffectivelyEmpty(x, floorRowY))
                    continue;

                if (selectedStack == null || selectedStack.IsAir || favoritedTileQuantity <= 0)
                    continue;

                if (!TileLoader.CanPlace(x, floorRowY, tileType))
                    continue;
                
                WorldGen.PlaceTile(x, floorRowY, dummyTile, mute: true);
                if (Main.tile[x, floorRowY].HasTile && Main.tile[x, floorRowY].TileType == dummyTile)
                {
                    player.PickTile(x, floorRowY, 1000);

                    if (Main.tile[x, floorRowY].HasTile && Main.tile[x, floorRowY].TileType == dummyTile)
                    {
                        WorldGen.KillTile(x, floorRowY, false, false, true);
                        continue;
                    }
                }

                WorldGen.PlaceTile(x, floorRowY, tileType, mute: true);
                
                Tile tile = Main.tile[x, floorRowY];
                if (tile.IsHalfBlock)
                    WorldGen.PoundTile(x, floorRowY);
                if (tile.Slope != SlopeType.Solid)
                    WorldGen.SlopeTile(x, floorRowY, 0);

                if (Main.tile[x, floorRowY].TileType != tileType)
                    continue;

                anyTileModified = true;
                favoritedTileQuantity--;

                bool consumePaintCoating = Main.rand.NextFloat() < PaintCoatingConsumptionChance;
                if (coatingIndices.Count > 0)
                {
                    foreach (int coatingIndex in coatingIndices)
                    {
                        Item coatingItem = player.inventory[coatingIndex];

                        if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                            continue;

                        Tile tmp_tile = Main.tile[x, floorRowY];

                        if (coatingItem.type == ItemID.GlowPaint && !tmp_tile.IsTileFullbright)
                        {
                            tmp_tile.IsTileFullbright = true;

                            if (consumePaintCoating)
                                coatingQuantities[coatingIndex]--;
                        }
                        else if (coatingItem.type == ItemID.EchoCoating && !tmp_tile.IsTileInvisible)
                        {
                            tmp_tile.IsTileInvisible = true;

                            if (consumePaintCoating)
                                coatingQuantities[coatingIndex]--;
                        }
                    }
                }

                if (paintIndex != -1)
                {
                    Tile tmp_tile = Main.tile[x, floorRowY];

                    if (player.builderAccStatus[3] == 0 && player.autoPaint && paintQuantity > 0 &&
                        tmp_tile.TileColor != paintItem.paint)
                    {
                        WorldGen.paintTile(x, floorRowY, paintItem.paint);

                        if (consumePaintCoating)
                            paintQuantity--;
                    }
                }
            }
            for (int x = gridCenterX - 1; x >= topLeftTileX; x--)
            {
                if (!WorldGen.InWorld(x, floorRowY) || (RespectObstacles && !IsEffectivelyEmpty(x, floorRowY) && gridCenterX - x > leftForcedPlatformWidth))
                    break;

                if (Main.tile[x, floorRowY].TileType == dummyTile)
                {
                    Tile tmp_tile = Main.tile[x, floorRowY];
                    tmp_tile.HasTile = false;
                }
                else if (!IsEffectivelyEmpty(x, floorRowY))
                    continue;

                if (selectedStack == null || selectedStack.IsAir || favoritedTileQuantity <= 0)
                    continue;

                if (!TileLoader.CanPlace(x, floorRowY, tileType))
                    continue;
                
                WorldGen.PlaceTile(x, floorRowY, dummyTile, mute: true);
                if (Main.tile[x, floorRowY].HasTile && Main.tile[x, floorRowY].TileType == dummyTile)
                {
                    player.PickTile(x, floorRowY, 1000);

                    if (Main.tile[x, floorRowY].HasTile && Main.tile[x, floorRowY].TileType == dummyTile)
                    {
                        WorldGen.KillTile(x, floorRowY, false, false, true);
                        continue;
                    }
                }

                WorldGen.PlaceTile(x, floorRowY, tileType, mute: true);

                Tile tile = Main.tile[x, floorRowY];
                if (tile.IsHalfBlock)
                    WorldGen.PoundTile(x, floorRowY);
                if (tile.Slope != SlopeType.Solid)
                    WorldGen.SlopeTile(x, floorRowY, 0);

                if (Main.tile[x, floorRowY].TileType != tileType)
                    continue;

                anyTileModified = true;
                favoritedTileQuantity--;

                bool consumePaintCoating = Main.rand.NextFloat() < PaintCoatingConsumptionChance;
                if (coatingIndices.Count > 0)
                {
                    foreach (int coatingIndex in coatingIndices)
                    {
                        Item coatingItem = player.inventory[coatingIndex];

                        if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                            continue;

                        Tile tmp_tile = Main.tile[x, floorRowY];

                        if (coatingItem.type == ItemID.GlowPaint && !tmp_tile.IsTileFullbright)
                        {
                            tile.IsTileFullbright = true;

                            if (consumePaintCoating)
                                coatingQuantities[coatingIndex]--;
                        }
                        else if (coatingItem.type == ItemID.EchoCoating && !tmp_tile.IsTileInvisible)
                        {
                            tile.IsTileInvisible = true;

                            if (consumePaintCoating)
                                coatingQuantities[coatingIndex]--;
                        }
                    }
                }

                if (paintIndex != -1)
                {
                    Tile tmp_tile = Main.tile[x, floorRowY];

                    if (player.builderAccStatus[3] == 0 && player.autoPaint && paintQuantity > 0 &&
                        tmp_tile.TileColor != paintItem.paint)
                    {
                        WorldGen.paintTile(x, floorRowY, paintItem.paint);

                        if (consumePaintCoating)
                            paintQuantity--;
                    }
                }
            }

            int prev_width = 0;
            int groundStartTileX = topLeftTileX + (buildWidth / 2) - (groundWidthTiles / 2);
            foreach ((int i, int _) in groundFurnitureIndices)
            {
                Item furnitureItem = player.inventory[i];
                int tmp_tileType = furnitureItem.createTile;
                int tmp_placeStyle = furnitureItem.placeStyle;
                bool isConsumable = furnitureItem.consumable;

                TileObjectData data = TileObjectData.GetTileData(tmp_tileType, tmp_placeStyle);
                int width = 1;
                int height = 1;

                if (data != null)
                {
                    width = data.Width;
                    height = data.Height;
                }

                int left = groundStartTileX + prev_width;
                prev_width += width;
                int top = floorRowY - height;

                if (furnitureItem == null || furnitureItem.IsAir || groundFurnitureQuantities[i] <= 0)
                    continue;

                bool canPlace = true;
                for (int tx = left; tx < left + width && canPlace; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        if (!WorldGen.InWorld(tx, ty, 1) || !IsEffectivelyEmpty(tx, ty))
                        {
                            canPlace = false;
                            break;
                        }
                    }
                }
                if (!canPlace)
                    continue;

                for (int tx = left; tx < left + width && canPlace; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        WorldGen.PlaceTile(tx, ty, dummyTile, mute: true);
                        if (Main.tile[tx, ty].HasTile && Main.tile[tx, ty].TileType == dummyTile)
                        {
                            player.PickTile(tx, ty, 1000);

                            if (Main.tile[tx, ty].HasTile && Main.tile[tx, ty].TileType == dummyTile)
                            {
                                WorldGen.KillTile(tx, ty, false, false, true);
                                canPlace = false;
                                break;
                            }
                        }
                    }
                }
                if (!canPlace)
                    continue;

                int originX = left + (data != null ? data.Origin.X : 0);
                int originY = top + (data != null ? data.Origin.Y : 0);

                if (!TileLoader.CanPlace(originX, originY, tmp_tileType))
                    continue;

                if (!WorldGen.PlaceObject(originX, originY, tmp_tileType, mute: true, style: tmp_placeStyle))
                    continue;

                if (!Main.tile[originX, originY].HasTile || Main.tile[originX, originY].TileType != tmp_tileType)
                    continue;

                for (int tx = left; tx < left + width; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        bool consumePaintCoating = Main.rand.NextFloat() < PaintCoatingConsumptionChance;

                        Tile tmp_tile = Main.tile[tx, ty];
                        if (!tmp_tile.HasTile || tmp_tile.TileType != tmp_tileType)
                            continue;

                        foreach (int coatingIndex in coatingIndices)
                        {
                            Item coatingItem = player.inventory[coatingIndex];

                            if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                                continue;

                            if (coatingItem.type == ItemID.GlowPaint && !tmp_tile.IsTileFullbright)
                            {
                                tmp_tile.IsTileFullbright = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                            else if (coatingItem.type == ItemID.EchoCoating && !tmp_tile.IsTileInvisible)
                            {
                                tmp_tile.IsTileInvisible = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                        }

                        if (paintItem != null && player.builderAccStatus[3] == 0 && player.autoPaint &&
                            paintQuantity > 0 && tmp_tile.TileColor != paintItem.paint)
                        {
                            WorldGen.paintTile(tx, ty, paintItem.paint);

                            if (consumePaintCoating)
                                paintQuantity--;
                        }
                    }
                }

                anyTileModified = true;
                if (isConsumable)
                    groundFurnitureQuantities[i]--;
            }

            prev_width = 0;
            int hangingStartTileX = topLeftTileX + (buildWidth / 2) - (hangingWidthTiles / 2);
            foreach ((int i, int _) in hangingFurnitureIndices)
            {
                Item furnitureItem = player.inventory[i];
                int tmp_tileType = furnitureItem.createTile;
                int tmp_placeStyle = furnitureItem.placeStyle;
                bool isConsumable = furnitureItem.consumable;

                TileObjectData data = TileObjectData.GetTileData(tmp_tileType, tmp_placeStyle);
                int width = 1;
                int height = 1;

                if (data != null)
                {
                    width = data.Width;
                    height = data.Height;
                }

                int left = hangingStartTileX + prev_width;
                prev_width += width;
                int top = floorRowY + 1;

                if (furnitureItem == null || furnitureItem.IsAir || hangingFurnitureQuantities[i] <= 0)
                    continue;

                bool canPlace = true;
                for (int tx = left; tx < left + width && canPlace; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        if (!WorldGen.InWorld(tx, ty, 1) || !IsEffectivelyEmpty(tx, ty))
                        {
                            canPlace = false;
                            break;
                        }
                    }
                }
                if (!canPlace)
                    continue;

                for (int tx = left; tx < left + width && canPlace; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        WorldGen.PlaceTile(tx, ty, dummyTile, mute: true);
                        if (Main.tile[tx, ty].HasTile && Main.tile[tx, ty].TileType == dummyTile)
                        {
                            player.PickTile(tx, ty, 1000);

                            if (Main.tile[tx, ty].HasTile && Main.tile[tx, ty].TileType == dummyTile)
                            {
                                WorldGen.KillTile(tx, ty, false, false, true);
                                canPlace = false;
                                break;
                            }
                        }
                    }
                }
                if (!canPlace)
                    continue;

                int originX = left + (data != null ? data.Origin.X : 0);
                int originY = top + (data != null ? data.Origin.Y : 0);

                if (!TileLoader.CanPlace(originX, originY, tmp_tileType))
                    continue;

                if (!WorldGen.PlaceObject(originX, originY, tmp_tileType, mute: true, style: tmp_placeStyle))
                    continue;

                if (!Main.tile[originX, originY].HasTile || Main.tile[originX, originY].TileType != tmp_tileType)
                    continue;

                for (int tx = left; tx < left + width; tx++)
                {
                    for (int ty = top; ty < top + height; ty++)
                    {
                        bool consumePaintCoating = Main.rand.NextFloat() < PaintCoatingConsumptionChance;

                        Tile tmp_tile = Main.tile[tx, ty];
                        if (!tmp_tile.HasTile || tmp_tile.TileType != tmp_tileType)
                            continue;

                        foreach (int coatingIndex in coatingIndices)
                        {
                            Item coatingItem = player.inventory[coatingIndex];

                            if (coatingItem == null || coatingItem.IsAir || coatingQuantities[coatingIndex] <= 0)
                                continue;

                            if (coatingItem.type == ItemID.GlowPaint && !tmp_tile.IsTileFullbright)
                            {
                                tmp_tile.IsTileFullbright = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                            else if (coatingItem.type == ItemID.EchoCoating && !tmp_tile.IsTileInvisible)
                            {
                                tmp_tile.IsTileInvisible = true;

                                if (consumePaintCoating)
                                    coatingQuantities[coatingIndex]--;
                            }
                        }

                        if (paintItem != null && player.builderAccStatus[3] == 0 && player.autoPaint &&
                            paintQuantity > 0 && tmp_tile.TileColor != paintItem.paint)
                        {
                            WorldGen.paintTile(tx, ty, paintItem.paint);

                            if (consumePaintCoating)
                                paintQuantity--;
                        }
                    }
                }

                anyTileModified = true;
                if (isConsumable)
                    hangingFurnitureQuantities[i]--;
            }

            if (anyTileModified)
            {
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, topLeftTileX - 1, topLeftTileY - 1, buildWidth + 2, buildHeight + 2);
            }

            return (favoritedTileQuantity, groundFurnitureQuantities, hangingFurnitureQuantities, coatingQuantities, paintQuantity, anyTileModified);
        }

        public override bool? UseItem(Player player)
        {       
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            holdFired = false;

            if (FAAToolsPlayer.ArenaBuilderDisableCooldown > 0 || !player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = HoldTileX != -1 ? HoldTileX : Player.tileTargetX;
            int centerY = HoldTileY != -1 ? HoldTileY : Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            (int favoritedTileIndex, List<(int, int)> groundFurnitureIndices, List<(int, int)> hangingFurnitureIndices, List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            if (favoritedTileIndex == -1 || player.inventory[favoritedTileIndex].stack < 170)
            {
                Main.playerInventory = true;
                player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                player.itemAnimation = 0;
                player.itemTime = 0;
                return true;
            }

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                (int updatedFavoritedTileQuantity, Dictionary<int, int> updatedGroundFurnitureIndicesQuantities, Dictionary<int, int> updatedHangingFurnitureIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = AttemptPlacing(player, centerX, centerY, favoritedTileIndex, groundFurnitureIndices, hangingFurnitureIndices, coatingIndices, paintIndex);

                if (anyTileModified)
                {
                    if (favoritedTileIndex != -1)
                    {
                        player.inventory[favoritedTileIndex].stack = updatedFavoritedTileQuantity;
                        if (updatedFavoritedTileQuantity == 0)
                        {
                            Main.playerInventory = true;
                            player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    foreach ((int i, int _) in groundFurnitureIndices)
                    {
                        player.inventory[i].stack = updatedGroundFurnitureIndicesQuantities[i];
                        if (updatedGroundFurnitureIndicesQuantities[i] == 0)
                        {
                            Main.playerInventory = true;
                            player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    foreach ((int i, int _) in hangingFurnitureIndices)
                    {
                        player.inventory[i].stack = updatedHangingFurnitureIndicesQuantities[i];
                        if (updatedHangingFurnitureIndicesQuantities[i] == 0)
                        {
                            Main.playerInventory = true;
                            player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    foreach (int i in coatingIndices)
                    {
                        player.inventory[i].stack = updatedCoatingIndicesQuantities[i];
                        if (updatedCoatingIndicesQuantities[i] == 0)
                        {
                            Main.playerInventory = true;
                            player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }
                    if (paintIndex != -1)
                    {
                        player.inventory[paintIndex].stack = updatedPaintQuantity;
                        if (updatedPaintQuantity == 0)
                        {
                            Main.playerInventory = true;
                            player.GetModPlayer<FAAToolsPlayer>().ArenaBuilderDisableCooldown = 180;
                            player.itemAnimation = 0;
                            player.itemTime = 0;
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Dig, player.Center);
                }
            }
            else
            {
                FAAToolsPlayer.ArenaBuilderPlaceTilesRequestPending = true;
                // FAAToolsPlayer.ArenaBuilderPlaceTilesRequestCooldown = 5; // ~ 1/12 second
                FAAToolsPlayer.ArenaBuilderPlaceTilesRequestTimeout = 30; // ~ 1/2 second

                SendArenaBuilderPlaceTilesRequest(centerX, centerY, Item.type, favoritedTileIndex, groundFurnitureIndices, hangingFurnitureIndices, coatingIndices, paintIndex);
            }

            return true;
        }

        public static void HandleArenaBuilderSound(BinaryReader reader, int whoAmI)
        {
            float x = reader.ReadInt32();
            float y = reader.ReadInt32();

            SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
        }

        public static void SendArenaBuilderPlaceTilesRequest(int centerX, int centerY, int itemType, int favoritedTileIndex, List<(int, int)> groundFurnitureIndices, List<(int, int)> hangingFurnitureIndices, List<int> coatingIndices, int paintIndex)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.ArenaBuilderPlaceTilesRequest);
            packet.Write(centerX);
            packet.Write(centerY);
            packet.Write(itemType);
            packet.Write(favoritedTileIndex);
            packet.Write((byte)groundFurnitureIndices.Count);
            foreach ((int index, int placeStyle) in groundFurnitureIndices)
            {
                packet.Write((byte)index);
                packet.Write(placeStyle);
            }
            packet.Write((byte)hangingFurnitureIndices.Count);
            foreach ((int index, int placeStyle) in hangingFurnitureIndices)
            {
                packet.Write((byte)index);
                packet.Write(placeStyle);
            }
            packet.Write((byte)coatingIndices.Count);
            foreach (int i in coatingIndices)
                packet.Write((byte)i);
            packet.Write(paintIndex);
            packet.Send();
        }

        public static void SendArenaBuilderPlaceTilesGrant(int whoAmI, int favoritedTileIndex, int updatedFavoritedTileQuantity, Dictionary<int, int> updatedGroundFurnitureIndicesQuantities, Dictionary<int, int> updatedHangingFurnitureIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int paintIndex, int updatedPaintQuantity, bool anyTileModified, int centerX, int centerY, int Anchor)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.ArenaBuilderPlaceTilesGrant);
            packet.Write(favoritedTileIndex);
            packet.Write(updatedFavoritedTileQuantity);
            packet.Write((byte)updatedGroundFurnitureIndicesQuantities.Count);
            foreach (var (slot, quantity) in updatedGroundFurnitureIndicesQuantities)
            {
                packet.Write((byte)slot);
                packet.Write(quantity);
            }
            packet.Write((byte)updatedHangingFurnitureIndicesQuantities.Count);
            foreach (var (slot, quantity) in updatedHangingFurnitureIndicesQuantities)
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
            packet.Write(Anchor);
            packet.Send(whoAmI);
        }

        public static void SendArenaBuilderPlaceTilesDeny(int whoAmI)
        {
            ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
            packet.Write((byte)FoopaArenaArsenal.PacketType.LargeTilePlaceTilesDeny);
            packet.Send(whoAmI);
        }
        public static void HandleArenaBuilderPlaceTilesRequest(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            int centerX = reader.ReadInt32();
            int centerY = reader.ReadInt32();
            int claimedItemType = reader.ReadInt32();
            int favoritedTileIndex = reader.ReadInt32();

            byte indexCount = reader.ReadByte();
            List<(int, int)> groundFurnitureIndices = new List<(int, int)>();
            for (int i = 0; i < indexCount; i++)
            {
                int index = reader.ReadByte();
                int placeStyle = reader.ReadInt32();
                groundFurnitureIndices.Add((index, placeStyle));
            }

            indexCount = reader.ReadByte();
            List<(int, int)> hangingFurnitureIndices = new List<(int, int)>();
            for (int i = 0; i < indexCount; i++)
            {
                int index = reader.ReadByte();
                int placeStyle = reader.ReadInt32();
                hangingFurnitureIndices.Add((index, placeStyle));
            }

            indexCount = reader.ReadByte();
            List<int> coatingIndices = new List<int>();
            for (int i = 0; i < indexCount; i++)
                coatingIndices.Add(reader.ReadByte());

            int paintIndex = reader.ReadInt32();

            Player player = Main.player[whoAmI];
            var FAAToolsPlayer = player.GetModPlayer<FAAToolsPlayer>();

            /*
            if (FAAToolsPlayer.ArenaBuilderPlaceTilesRequestCooldown > 0)
            {
                SendArenaBuilderPlaceTilesDeny(whoAmI);
                return;
            }
            */

            Item heldItem = player.HeldItem;
            if (heldItem == null || heldItem.IsAir || heldItem.type != claimedItemType ||
                heldItem.ModItem is not ArenaBuilderBase arenaBuilder)
            {
                SendArenaBuilderPlaceTilesDeny(whoAmI);
                return;
            }

            (int updatedFavoritedTileQuantity, Dictionary<int, int> updatedGroundFurnitureIndicesQuantities, Dictionary<int, int> updatedHangingFurnitureIndicesQuantities, Dictionary<int, int> updatedCoatingIndicesQuantities, int updatedPaintQuantity, bool anyTileModified) = arenaBuilder.AttemptPlacing(player, centerX, centerY, favoritedTileIndex, groundFurnitureIndices, hangingFurnitureIndices, coatingIndices, paintIndex);

            if (anyTileModified)
            {
                ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                packet.Write((byte)FoopaArenaArsenal.PacketType.ArenaBuilderSound);
                packet.Write(centerX);
                packet.Write(centerY);
                packet.Send();

                SendArenaBuilderPlaceTilesGrant(whoAmI, favoritedTileIndex, updatedFavoritedTileQuantity, updatedGroundFurnitureIndicesQuantities, updatedHangingFurnitureIndicesQuantities, updatedCoatingIndicesQuantities, paintIndex, updatedPaintQuantity, anyTileModified, centerX, centerY, arenaBuilder.Anchor);
            }
            else
                SendArenaBuilderPlaceTilesDeny(whoAmI);
        }

        public static void HandleArenaBuilderPlaceTilesGrant(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int favoritedTileIndex = reader.ReadInt32();
            int updatedFavoritedTileQuantity = reader.ReadInt32();

            byte indexCount = reader.ReadByte();
            Dictionary<int, int> updatedGroundFurnitureIndicesQuantities = new Dictionary<int, int>();
            for (int i = 0; i < indexCount; i++)
            {
                int slot = reader.ReadByte();
                int quantity = reader.ReadInt32();
                updatedGroundFurnitureIndicesQuantities[slot] = quantity;
            }

            indexCount = reader.ReadByte();
            Dictionary<int, int> updatedHangingFurnitureIndicesQuantities = new Dictionary<int, int>();
            for (int i = 0; i < indexCount; i++)
            {
                int slot = reader.ReadByte();
                int quantity = reader.ReadInt32();
                updatedHangingFurnitureIndicesQuantities[slot] = quantity;
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
            int Anchor = reader.ReadInt32();

            Player player = Main.LocalPlayer;
            if (favoritedTileIndex != -1)
            {
                player.inventory[favoritedTileIndex].stack = updatedFavoritedTileQuantity;

                if (player.inventory[favoritedTileIndex].stack <= 0)
                    player.inventory[favoritedTileIndex].TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, favoritedTileIndex);
            }
            foreach (int slot in updatedGroundFurnitureIndicesQuantities.Keys)
            {
                player.inventory[slot].stack = updatedGroundFurnitureIndicesQuantities[slot];

                if (player.inventory[slot].stack <= 0)
                    player.inventory[slot].TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
            }
            foreach (int slot in updatedHangingFurnitureIndicesQuantities.Keys)
            {
                player.inventory[slot].stack = updatedHangingFurnitureIndicesQuantities[slot];

                if (player.inventory[slot].stack <= 0)
                    player.inventory[slot].TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
            }
            foreach (int slot in updatedCoatingIndicesQuantities.Keys)
            {
                player.inventory[slot].stack = updatedCoatingIndicesQuantities[slot];

                if (player.inventory[slot].stack <= 0)
                    player.inventory[slot].TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
            }
            if (paintIndex != -1)
            {
                player.inventory[paintIndex].stack = updatedPaintQuantity;

                if (player.inventory[paintIndex].stack <= 0)
                    player.inventory[paintIndex].TurnToAir();

                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, paintIndex);
            }

            if (anyTileModified)
            {
                int buildWidth = 170;
                int buildHeightGapTop = 4;
                int buildHeightGapBottom = 2;
                int buildHeight = buildHeightGapTop + buildHeightGapBottom + 1;

                int topLeftTileX = Anchor switch
                {
                    -1 => centerX - (buildWidth - 1),
                    0 => centerX - (buildWidth / 2),
                    1 => centerX,
                    _ => centerX
                };
                int topLeftTileY = centerY - buildHeightGapTop;

                NetMessage.SendTileSquare(-1, topLeftTileX - 1, topLeftTileY - 1, buildWidth + 2, buildHeight + 2);
            }

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().ArenaBuilderPlaceTilesRequestPending = false;
        }

        public static void HandleArenaBuilderPlaceTilesDeny(BinaryReader reader, int whoAmI)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>().ArenaBuilderPlaceTilesRequestPending = false;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(RespectObstacles);
        }

        public override void NetReceive(BinaryReader reader)
        {
            RespectObstacles = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag)
        {
            tag["RespectObstacles"] = RespectObstacles;
        }

        public override void LoadData(TagCompound tag)
        {
            RespectObstacles = tag.GetBool("RespectObstacles");
        }
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/ArenaBuilder/{ItemName}";
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
            
                string respectObstaclesPath = $"FoopaArenaArsenal/Content/Items/ItemUI/RespectObstacles{(RespectObstacles ? 1 : 0)}";
                Texture2D respectObstacles = ModContent.Request<Texture2D>(respectObstaclesPath, AssetRequestMode.ImmediateLoad).Value;
                Rectangle respectObstaclesFrame = new Rectangle(0, 0, respectObstacles.Width, respectObstacles.Height);
                Vector2 respectObstaclesOrigin = new Vector2(respectObstacles.Width / 2f, respectObstacles.Height / 2f);

                Vector2 badgeOffset2 = isSelectedHotbarSlot
                    ? new Vector2(-3.0f, 1.5f)
                    : new Vector2(0.5f, -0.5f);

                spriteBatch.Draw(respectObstacles, position + badgeOffset2, respectObstaclesFrame, drawColor, 0f, respectObstaclesOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;

            (int favoritedTileIndex, List<(int, int)> groundFurnitureIndices, List<(int, int)> hangingFurnitureIndices, List<int> coatingIndices, int paintIndex) = ScanInventory(player);

            string tileLine = "[c/64FF64:Platform:] ";
            if (favoritedTileIndex == -1)
                tileLine += "[c/FF6464:None - favorite a stack of at least 170 solid blocks (required)]";
            else
            {
                Item tileItem = player.inventory[favoritedTileIndex];
                tileLine += $"[i/s{tileItem.stack}:{tileItem.type}]";

                if (tileItem.stack < 170)
                    tileLine += " [c/FF6464:(need 170)]";
            }

            string furnitureLine = "[c/64FF64:Furniture:] ";
            if (groundFurnitureIndices.Count == 0 && hangingFurnitureIndices.Count == 0)
                furnitureLine += "[c/FF6464:None (optional)]";
            else
            {
                string furnitureIcons = "";

                foreach ((int idx, int _) in groundFurnitureIndices)
                {
                    Item stack = player.inventory[idx];
                    furnitureIcons += $"[i/s{stack.stack}:{stack.type}] ";
                }

                foreach ((int idx, int _) in hangingFurnitureIndices)
                {
                    Item stack = player.inventory[idx];
                    furnitureIcons += $"[i/s{stack.stack}:{stack.type}] ";
                }

                furnitureLine += furnitureIcons.TrimEnd();
            }

            string chanceNotConsumePaintCoatingLine = $"{(int)((1f - PaintCoatingConsumptionChance) * 100)}% chance not to consume paint or coatings";

            string info =
                "Builds a 170-tile platform with your favorited furniture on and under it" +
                "\nHold Left Click for 1 second to build; the location locks when you start holding" +
                "\nFavorite furniture that grants a nearby effect to include it in the preview and build" +
                $"\n{tileLine}" +
                $"\n{furnitureLine}" +
                "\nApplies the first favorited paint all favorited coatings when auto-paint is active" +
                $"\n{chanceNotConsumePaintCoatingLine}" +
                "\nRight Click to toggle where the arena is anchored to the cursor" +
                $"\nAlt + Right Click to {(RespectObstacles ? "disable" : "enable")} stopping the platform at terrain obstacles";

            var itemTooltip = new TooltipLine(Mod, "ArenaBuilderInfo", info);

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }

    public class ArenaBuilder1 : ArenaBuilderBase
    {
        protected override string ItemName => "ArenaBuilder1";
        protected override string Badge => "Anchor1";
        protected override ModItem ChangeInto => ModContent.GetInstance<ArenaBuilder2>();
        protected override int BonusRange => 85;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override float PaintCoatingConsumptionChance => .5f;

        public override int Anchor => -1;
    }

    public class ArenaBuilder2 : ArenaBuilderBase
    {
        protected override string ItemName => "ArenaBuilder2";
        protected override string Badge => "Anchor2";
        protected override ModItem ChangeInto => ModContent.GetInstance<ArenaBuilder3>();
        protected override int BonusRange => 85;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override float PaintCoatingConsumptionChance => .5f;

        public override int Anchor => 0;
    }

    public class ArenaBuilder3 : ArenaBuilderBase
    {
        protected override string ItemName => "ArenaBuilder3";
        protected override string Badge => "Anchor3";
        protected override ModItem ChangeInto => ModContent.GetInstance<ArenaBuilder1>();
        protected override int BonusRange => 85;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override float PaintCoatingConsumptionChance => .5f;

        public override int Anchor => 1;
    }
}