using FoopaArenaArsenal.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Projectiles
{
    public class LargeGridPreviewProj : ModProjectile
    {
        private const int InnerLineThickness = 2;
        private const int OuterBoxThickness = 3;

        private static readonly Color InnerLineColor = Color.White * 0.25f;
        private static readonly Color OuterBoxColor = Color.LightGray * 0.5f;

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

            if (!player.active || player.dead ||
                (
                    player.HeldItem?.ModItem is not LargePaintScraperBase &&
                    player.HeldItem?.ModItem is not LargePaintBrushBase &&
                    player.HeldItem?.ModItem is not LargePaintRollerBase &&
                    player.HeldItem?.ModItem is not LargeBottomlessBucketBase &&
                    player.HeldItem?.ModItem is not LargeSpongeBase
                )
            )
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
        }

        private bool IsTileInReach(Player player, int tileX, int tileY, int itemTileBoost)
        {
            bool inRangeX = player.position.X / 16f - Player.tileRangeX - itemTileBoost <= tileX &&
                            (player.position.X + player.width) / 16f + Player.tileRangeX + itemTileBoost - 1f >= tileX;

            bool inRangeY = player.position.Y / 16f - Player.tileRangeY - itemTileBoost <= tileY &&
                            (player.position.Y + player.height) / 16f + Player.tileRangeY + itemTileBoost - 2f >= tileY;

            return inRangeX && inRangeY;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            ModItem heldModItem = player?.HeldItem?.ModItem;

            int sweep;
            if (heldModItem is LargeSpongeBase largeSponge)
                sweep = largeSponge.Sweep;
            else if (heldModItem is LargeBottomlessBucketBase largeBucket)
                sweep = largeBucket.Sweep;
            else if (heldModItem is LargePaintBrushBase largePaintBrush)
                sweep = largePaintBrush.Sweep;
            else if (heldModItem is LargePaintRollerBase largePaintRoller)
                sweep = largePaintRoller.Sweep;
            else if (heldModItem is LargePaintScraperBase largePaintScraper)
                sweep = largePaintScraper.Sweep;
            else
                return false;

            if (sweep == 1)
                return false;

            int offset = sweep / 2;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY, player.HeldItem.tileBoost))
                return false;

            int topLeftTileX = centerX - offset;
            int topLeftTileY = centerY - offset;

            Vector2 origin = new Vector2(topLeftTileX * 16, topLeftTileY * 16) - Main.screenPosition;
            int gridSize = sweep * 16;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

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