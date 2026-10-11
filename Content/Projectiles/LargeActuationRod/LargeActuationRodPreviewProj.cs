using FoopaArenaArsenal.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Projectiles
{
    public class LargeActuationRodPreviewProj : ModProjectile
    {
        private const int InnerLineThickness = 2;
        private const int OuterBoxThickness = 3;

        private static readonly Color InnerLineColor = Color.White * 0.25f;
        private static readonly Color OuterBoxColor = Color.LightGray * 0.5f;

        public override string Texture => $"FoopaArenaArsenal/Content/Projectiles/LargeActuationRod/LargeActuationRodPreviewProj";

        private static Texture2D actuatorSprite = ModContent.Request<Texture2D>("FoopaArenaArsenal/Content/Projectiles/LargeActuationRod/ActuatorTile", AssetRequestMode.ImmediateLoad).Value;
        private const float ActuatorSpriteOpacity = 0.5f;

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

            if (!player.active || player.dead || player.HeldItem?.ModItem is not LargeActuationRodBase)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        public bool IsTileInReach(Player player, int tileX, int tileY, int itemTileBoost)
        {
            bool inRangeX = player.position.X / 16f - Player.tileRangeX - itemTileBoost <= tileX &&
                            (player.position.X + player.width) / 16f + Player.tileRangeX + itemTileBoost - 1f >= tileX;

            bool inRangeY = player.position.Y / 16f - Player.tileRangeY - itemTileBoost <= tileY &&
                            (player.position.Y + player.height) / 16f + Player.tileRangeY + itemTileBoost - 2f >= tileY;

            return inRangeX && inRangeY;
        }

        public static bool IsRealBlock(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Framing.GetTileSafely(x, y);

            bool isPlatform = TileID.Search.GetName(tile.TileType).EndsWith("Platform") || TileID.Search.GetName(tile.TileType).EndsWith("Platforms");

            if (!tile.HasTile)
                return false;

            if (Main.tileFrameImportant[tile.TileType] && !isPlatform)
                return false;

            return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType] || isPlatform;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            if (player?.HeldItem?.ModItem is not LargeActuationRodBase largeActuationRod)
                return false;

            int sweep = largeActuationRod.Sweep;

            int offset = sweep / 2;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            int actuatorRangeX = 111;
            int actuatorRangeY = 51;

            int playerTileX = (int)(player.Center.X / 16f);
            int playerTileY = (int)(player.Center.Y / 16f);

            if (largeActuationRod.NeedActuator)
            {
                for (int x = playerTileX - actuatorRangeX; x <= playerTileX + actuatorRangeX; x++)
                {
                    for (int y = playerTileY - actuatorRangeY; y <= playerTileY + actuatorRangeY; y++)
                    {
                        if (!WorldGen.InWorld(x, y))
                            continue;

                        Tile tile = Main.tile[x, y];
                        if (tile == null || !tile.HasActuator)
                            continue;

                        Vector2 actuatorDrawPos = new Vector2(x * 16 + 8, y * 16 + 8) - Main.screenPosition;
                        Vector2 spriteOrigin = new Vector2(actuatorSprite.Width / 2f, actuatorSprite.Height / 2f);

                        float opacity = !tile.IsActuated && tile.HasTile && IsRealBlock(x, y) ? ActuatorSpriteOpacity: ActuatorSpriteOpacity * 0.4f;

                        Main.spriteBatch.Draw(actuatorSprite, actuatorDrawPos, null, Color.White * opacity, 0f, spriteOrigin, 1f, SpriteEffects.None, 0f);
                    }
                }
            }

            if (sweep == 1)
                return false;

            if (!IsTileInReach(player, centerX, centerY, player.HeldItem.tileBoost))
                return false;

            int topLeftTileX = centerX - offset;
            int topLeftTileY = centerY - offset;

            Vector2 origin = new Vector2(topLeftTileX * 16, topLeftTileY * 16) - Main.screenPosition;
            int gridSize = sweep * 16;

            for (int i = 1; i <= sweep - 1; i++)
            {
                float x = origin.X + i * 16;
                DrawRect(pixel, x - InnerLineThickness / 2f, origin.Y, InnerLineThickness, gridSize, InnerLineColor);

                float y = origin.Y + i * 16;
                DrawRect(pixel, origin.X, y - InnerLineThickness / 2f, gridSize, InnerLineThickness, InnerLineColor);
            }

            DrawRect(pixel, origin.X, origin.Y - 1, gridSize, OuterBoxThickness, OuterBoxColor); // top
            DrawRect(pixel, origin.X, origin.Y + gridSize - OuterBoxThickness + 1, gridSize, OuterBoxThickness, OuterBoxColor); // bottom
            DrawRect(pixel, origin.X - 1, origin.Y, OuterBoxThickness, gridSize, OuterBoxColor); // left
            DrawRect(pixel, origin.X + gridSize - OuterBoxThickness + 1, origin.Y, OuterBoxThickness, gridSize, OuterBoxColor); // right

            return false;
        }

        private static void DrawRect(Texture2D pixel, float x, float y, float width, float height, Color color)
        {
            Main.spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);
        }
    }
}