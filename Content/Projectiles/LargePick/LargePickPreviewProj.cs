using FoopaArenaArsenal.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Projectiles
{
    public class LargePickPreviewProj : ModProjectile
    {
        private const int InnerLineThickness = 2;
        private const int OuterBoxThickness = 3;

        private static readonly Color InnerLineColor = Color.White * 0.25f;
        private static readonly Color OuterBoxColor = Color.LightGray * 0.5f;
        private static readonly Color OccupiedTileColor = Color.Yellow * 0.25f;

        public override string Texture => $"FoopaArenaArsenal/Content/Projectiles/LargePick/LargePickPreviewProj";

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

            if (!player.active || player.dead || player.HeldItem?.ModItem is not LargePickBase)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            if (player?.HeldItem?.ModItem is not LargePickBase largePick)
                return false;

            int sweep = largePick.Sweep;

            if (sweep == 1)
                return false;

            int offset = sweep / 2;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!largePick.IsTileInReach(player, centerX, centerY))
                return false;

            int topLeftTileX = centerX - offset;
            int topLeftTileY = centerY - offset;

            Vector2 origin = new Vector2(topLeftTileX * 16, topLeftTileY * 16) - Main.screenPosition;
            int gridSize = sweep * 16;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int dx = -offset; dx <= offset; dx++)
            {
                for (int dy = -offset; dy <= offset; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y) || !WorldGen.CanKillTile(x, y))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (tile == null || !tile.HasTile)
                        continue;

                    ushort type = tile.TileType;

                    if (Main.tileAxe[type] || Main.tileHammer[type])
                        continue;

                    float rectX = origin.X + (dx + offset) * 16;
                    float rectY = origin.Y + (dy + offset) * 16;
                    DrawRect(pixel, rectX, rectY, 16, 16, OccupiedTileColor);
                }
            }

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