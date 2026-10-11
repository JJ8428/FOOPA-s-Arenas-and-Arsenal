using FoopaArenaArsenal.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ObjectData;

using Terraria.ID;
using System;

namespace FoopaArenaArsenal.Content.Projectiles
{
    public class ArenaBuilderPreviewProj : ModProjectile
    {
        private const int InnerLineThickness = 2;
        private const int OuterBoxThickness = 3;

        private static readonly Color InnerLineColor = Color.White * 0.25f;
        private static readonly Color OuterBoxColor = Color.LightGray * 0.5f;
        private static readonly Color PlaceableTileColor = Color.Yellow * 0.25f;

        public override string Texture => $"FoopaArenaArsenal/Content/Projectiles/LargeGrid/LargeGridPreviewProj";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.hide = false;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = false;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead || player.HeldItem?.ModItem is not ArenaBuilderBase)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        private static int DrawCenteredFurnitureRow(List<(int tileType, int placeStyle)> tileTypes, int gridLeftTileX, int rowY, int buildWidth)
        {            
            if (tileTypes.Count == 0)
                return 0;

            List<int> widths = new List<int>();
            List<int> heights = new List<int>();
            int totalWidthTiles = 0;

            for (int i = 0; i < tileTypes.Count; i++)
            {
                int tileType = tileTypes[i].tileType;

                TileObjectData data = TileObjectData.GetTileData(tileType, 0);
                int width = 1;
                int height = 1;

                if (data != null)
                {
                    width = data.Width;
                    height = data.Height;
                }

                widths.Add(width);
                heights.Add(height);
                totalWidthTiles += width;
            }

            int startTileX = gridLeftTileX + (buildWidth / 2) - (totalWidthTiles / 2);
            int cursorX = startTileX;

            for (int i = 0; i < tileTypes.Count; i++)
            {
                int tileType = tileTypes[i].tileType;
                int placeStyle = tileTypes[i].placeStyle;
                int width = widths[i];
                int height = heights[i];

                int topRowY = rowY - (height - 1);

                Main.instance.LoadTiles(tileType);

                if (tileType == TileID.HangingLanterns)
                {
                    Texture2D lanternTexture = TextureAssets.Tile[tileType].Value;

                    int frameYTop = placeStyle * 36;
                    int frameYBottom = placeStyle * 36 + 18;

                    for (int row = 0; row < 2; row++)
                    {
                        int worldY = topRowY + row;
                        int frameY = (row == 0) ? frameYTop : frameYBottom;

                        Vector2 drawPos = new Vector2(cursorX * 16, worldY * 16 - 2) - Main.screenPosition;
                        Rectangle sourceFrame = new Rectangle(0, frameY, 16, 16);

                        Main.spriteBatch.Draw(lanternTexture, drawPos, sourceFrame, Color.White * 0.5f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                    }

                    bool lanternClear = true;
                    for (int row = 0; row < 2; row++)
                    {
                        int worldX = cursorX;
                        int worldY = topRowY + row;

                        if (!WorldGen.InWorld(worldX, worldY))
                            continue;

                        Tile tile = Main.tile[worldX, worldY];
                        if (tile.HasTile && !Main.tileCut[tile.TileType])
                            lanternClear = false;
                    }

                    if (!lanternClear)
                    {
                        Texture2D pixel = TextureAssets.MagicPixel.Value;

                        for (int row = 0; row < 2; row++)
                        {
                            int worldX = cursorX;
                            int worldY = topRowY + row;

                            Vector2 drawPos = new Vector2(worldX * 16, worldY * 16) - Main.screenPosition;
                            Main.spriteBatch.Draw(pixel, new Rectangle((int)drawPos.X, (int)drawPos.Y, 16, 16), Color.Red * 0.5f);
                        }
                    }

                    cursorX += width;
                    continue;
                }

                Texture2D tileTexture = TextureAssets.Tile[tileType].Value;

                for (int col = 0; col < width; col++)
                {
                    for (int row = 0; row < height; row++)
                    {
                        int worldX = cursorX + col;
                        int worldY = topRowY + row;

                        Vector2 drawPos = new Vector2(worldX * 16, worldY * 16 + 2) - Main.screenPosition;
                        Rectangle sourceFrame = new Rectangle(col * 18, row * 18, 16, 16);

                        Main.spriteBatch.Draw(tileTexture, drawPos, sourceFrame, Color.White * 0.5f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                    }
                }

                bool clear = true;
                for (int col = 0; col < width; col++)
                {
                    for (int row = 0; row < height; row++)
                    {
                        int worldX = cursorX + col;
                        int worldY = topRowY + row;

                        if (!WorldGen.InWorld(worldX, worldY))
                            continue;

                        Tile tile = Main.tile[worldX, worldY];
                        if (tile.HasTile && !Main.tileCut[tile.TileType])
                            clear = false;
                    }
                }

                if (!clear)
                {
                    Texture2D pixel = TextureAssets.MagicPixel.Value;

                    for (int col = 0; col < width; col++)
                    {
                        for (int row = 0; row < height; row++)
                        {
                            int worldX = cursorX + col;
                            int worldY = topRowY + row;

                            Vector2 drawPos = new Vector2(worldX * 16, worldY * 16) - Main.screenPosition;
                            Main.spriteBatch.Draw(pixel, new Rectangle((int)drawPos.X, (int)drawPos.Y, 16, 16), Color.Red * 0.5f);
                        }
                    }
                }

                cursorX += width;
            }
            
            return totalWidthTiles;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            if (player?.HeldItem?.ModItem is not ArenaBuilderBase arenaBuilder)
                return false;

            if (arenaBuilder.holdFired)
                return false;

            int anchor = arenaBuilder.Anchor;

            // int centerX = Player.tileTargetX;
            // int centerY = Player.tileTargetY;

            int centerX = arenaBuilder.HoldTileX != -1 ? arenaBuilder.HoldTileX : Player.tileTargetX;
            int centerY = arenaBuilder.HoldTileY != -1 ? arenaBuilder.HoldTileY : Player.tileTargetY;

            if (!arenaBuilder.IsTileInReach(player, centerX, centerY))
                return false;

            int buildWidth = 170;
            int buildHeightGapTop = 4;
            int buildHeightGapBottom = 2;
            int buildHeight = buildHeightGapTop + buildHeightGapBottom + 1;

            int topLeftTileX = anchor switch
            {
                -1 => centerX - buildWidth + 1,
                0 => centerX - (buildWidth / 2),
                1 => centerX,
                _ => centerX
            };
            
            int topLeftTileY = centerY - buildHeightGapTop;

            (int _, List<(int, int)> groundFurnitureIndices, List<(int, int)> hangingFurnitureIndices, List<int> _, int _) = ArenaBuilderBase.ScanInventory(player);

            List<(int tileType, int placeStyle)> groundFurnitureTypes = new List<(int, int)>();
            foreach ((int i, int placeStyle) in groundFurnitureIndices)
                groundFurnitureTypes.Add((player.inventory[i].createTile, placeStyle));

            List<(int tileType, int placeStyle)> hangingFurnitureTypes = new List<(int, int)>();
            foreach ((int i, int placeStyle) in hangingFurnitureIndices)
                hangingFurnitureTypes.Add((player.inventory[i].createTile, placeStyle));

            Vector2 origin = new Vector2(topLeftTileX * 16, topLeftTileY * 16) - Main.screenPosition;
            int gridWidth = buildWidth * 16;
            int gridHeight = buildHeight * 16;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            if (arenaBuilder.HoldTicks > 0)
            {
                float fillOpacity = MathHelper.Min(arenaBuilder.HoldTicks / 60f, 1f) * 0.4f;
                DrawRect(pixel, origin.X, origin.Y, gridWidth, gridHeight, Color.LimeGreen * fillOpacity);
            }

            int gridCenterX = topLeftTileX + (buildWidth / 2);
            int floorRowY = topLeftTileY + buildHeightGapTop;

            int groundWidth = DrawCenteredFurnitureRow(groundFurnitureTypes, topLeftTileX, floorRowY - 1, buildWidth);
            int hangingWidth = DrawCenteredFurnitureRow(hangingFurnitureTypes, topLeftTileX, floorRowY + 2, buildWidth);
            int forcedPlatformWidth = Math.Max(groundWidth, hangingWidth);
            int rightForcedPlatformWidth = (forcedPlatformWidth - 1) / 2;
            int leftForcedPlatformWidth = forcedPlatformWidth / 2;

            for (int x = gridCenterX; x < topLeftTileX + buildWidth; x++)
            {
                if (!WorldGen.InWorld(x, floorRowY) || (arenaBuilder.RespectObstacles && !ArenaBuilderBase.IsEffectivelyEmpty(x, floorRowY) && x - gridCenterX >= rightForcedPlatformWidth))
                    break;

                if (ArenaBuilderBase.IsEffectivelyEmpty(x, floorRowY))
                {
                    Vector2 tileDrawPos = new Vector2(x * 16, floorRowY * 16) - Main.screenPosition;
                    DrawRect(pixel, tileDrawPos.X, tileDrawPos.Y, 16, 16, PlaceableTileColor);
                }
            }
            for (int x = gridCenterX; x >= topLeftTileX; x--)
            {
                if (!WorldGen.InWorld(x, floorRowY) || (arenaBuilder.RespectObstacles && !ArenaBuilderBase.IsEffectivelyEmpty(x, floorRowY) && gridCenterX - x >= leftForcedPlatformWidth))
                    break;

                if (ArenaBuilderBase.IsEffectivelyEmpty(x, floorRowY) && x != gridCenterX)
                {
                    Vector2 tileDrawPos = new Vector2(x * 16, floorRowY * 16) - Main.screenPosition;
                    DrawRect(pixel, tileDrawPos.X, tileDrawPos.Y, 16, 16, PlaceableTileColor);
                }
            }

            for (int i = 1; i <= buildWidth - 1; i++)
            {
                float x = origin.X + i * 16;
                DrawRect(pixel, x - InnerLineThickness / 2f, origin.Y, InnerLineThickness, gridHeight, InnerLineColor);
            }
            for (int i = 1; i <= buildHeight - 1; i++)
            {
                float y = origin.Y + i * 16;
                DrawRect(pixel, origin.X, y - InnerLineThickness / 2f, gridWidth, InnerLineThickness, InnerLineColor);
            }

            DrawRect(pixel, origin.X, origin.Y - 1, gridWidth, OuterBoxThickness, OuterBoxColor); // top
            DrawRect(pixel, origin.X, origin.Y + gridHeight - OuterBoxThickness + 1, gridWidth, OuterBoxThickness, OuterBoxColor); // bottom
            DrawRect(pixel, origin.X - 1, origin.Y, OuterBoxThickness, gridHeight, OuterBoxColor); // left
            DrawRect(pixel, origin.X + gridWidth - OuterBoxThickness + 1, origin.Y, OuterBoxThickness, gridHeight, OuterBoxColor); // right

            return false;
        }

        private static void DrawRect(Texture2D pixel, float x, float y, float width, float height, Color color)
        {
            Main.spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);
        }
    }
}