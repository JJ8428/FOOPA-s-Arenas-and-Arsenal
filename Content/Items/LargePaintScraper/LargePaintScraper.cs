using FoopaArenaArsenal.Content.Projectiles;
using FoopaArenaArsenal.Common.Systems;
using ReLogic.Content;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargePaintScraperBase : ModItem
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

        protected abstract float MossCollectionChance { get; }
        protected abstract ModItem AltChangeInto { get; }

        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargePaintScraper/{ItemName}";

        private bool wasLeftClickHeld = false;
        private int? scraperState1;
        private int? scraperState2;

        protected virtual ModItem DecraftAs => null;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            if (DecraftAs != null)
                ItemID.Sets.ShimmerCountsAsItem[Type] = DecraftAs.Type;
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

            if (!player.controlUseItem && wasLeftClickHeld)
                scraperState1 = scraperState2 = null;

            wasLeftClickHeld = player.controlUseItem;
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

        private static readonly int[] MossDropsByStyle =
        {
            ItemID.GreenMoss,
            ItemID.BrownMoss,
            ItemID.RedMoss,
            ItemID.BlueMoss,
            ItemID.PurpleMoss,
            ItemID.LavaMoss,
            ItemID.KryptonMoss,
            ItemID.XenonMoss,
            ItemID.ArgonMoss,
            ItemID.VioletMoss,
            ItemID.RainbowMoss,
        };

        private bool AttemptScraping(Player player, int x, int y, int? scraperState1, int? scraperState2)
        {
            Tile tile = Main.tile[x, y];

            if (tile == null)
                return false;
            
            if (scraperState1 == 0 && tile.HasTile)
            {
                if (scraperState2 == 0)
                    WorldGen.paintTile(x, y, PaintID.None, true);
                else if (scraperState2 == 1)
                    WorldGen.paintCoatTile(x, y, PaintCoatingID.None, true);            
                return false;
            }

            if (scraperState1 == 1 && tile.WallType != WallID.None)
            {
                if (scraperState2 == 0)
                    WorldGen.paintWall(x, y, PaintID.None, true);
                else if (scraperState2 == 1)
                    WorldGen.paintCoatWall(x, y, PaintCoatingID.None, true);
                return false;
            }

            if (scraperState1 == 2 && tile.HasTile && tile.TileType == TileID.LongMoss)
            {
                int placeStyle = tile.TileFrameX / 22;

                WorldGen.KillTile(x, y, false, false, true);

                if (placeStyle >= 0 && placeStyle < MossDropsByStyle.Length)
                {
                    if (Main.rand.NextFloat() < MossCollectionChance)
                        player.QuickSpawnItem(new EntitySource_TileBreak(x, y), MossDropsByStyle[placeStyle]);
                }

                return true;
            }

            return false;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            Tile centerTile = Main.tile[centerX, centerY];
            
            if (scraperState1 == null && scraperState2 == null)
            {
                int tileType = centerTile.TileType;
                if (centerTile.TileColor != PaintID.None || centerTile.IsTileFullbright || centerTile.IsTileInvisible)
                {
                    scraperState1 = 0;
                    if (centerTile.TileColor != PaintID.None)
                        scraperState2 = 0;
                    else
                        scraperState2 = 1;
                }
                else if (centerTile.WallColor != PaintID.None || centerTile.IsWallFullbright || centerTile.IsWallInvisible)
                {
                    scraperState1 = 1;
                    if (centerTile.WallColor != PaintID.None)
                        scraperState2 = 0;
                    else
                        scraperState2 = 1;
                }
                else if (centerTile.HasTile && tileType == TileID.LongMoss)
                    scraperState1 = 2;
            }

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

                    if (tile == null || (!tile.HasTile && tile.WallType == WallID.None))
                        continue;

                    bool blockDamaged = false;
                    if (!TileLoader.CanKillTile(x, y, tile.TileType, ref blockDamaged))
                        continue;

                    if(AttemptScraping(player, x, y, scraperState1, scraperState2))
                        anyTileModified = true;
                }
            }

            if (anyTileModified)
            {
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep);
            }
            
            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargePaintScraper/{ItemName}";
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
            string mossLine = MossCollectionChance > 0
                ? $"\n{MossCollectionChance * 100:0.##}% chance to collect moss from mossy tiles"
                : "";
            
            bool isSpectre = ItemName.Length >= 2 && ItemName[ItemName.Length - 2] == '2';

            int brushIcon = isSpectre ? ItemID.SpectrePaintbrush : ItemID.Paintbrush;
            int rollerIcon = isSpectre ? ItemID.SpectrePaintRoller : ItemID.PaintRoller;
            int scraperIcon = isSpectre ? ItemID.SpectrePaintScraper : ItemID.PaintScraper;

            var itemTooltip = new TooltipLine(
                Mod,
                "LargePaintScraperInfo",
                "Scrapes paint and coatings off tiles and walls in the grid" +
                mossLine +
                "\nRight Click to toggle sweep size" + 
                "\nAlt + Right Click to cycle through painting tools" +
                $"\n[i:{brushIcon}] [c/888888:Paint Brush]" +
                $"\n[i:{rollerIcon}] [c/888888:Paint Roller]" +
                $"\n[i:{scraperIcon}] [c/64FF64:Paint Scraper]"
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

                    bool hasWall = tile.WallType != WallID.None;
                    bool tilePaint = tile.HasTile && tile.TileColor != PaintID.None;
                    bool tileCoat = tile.HasTile && (tile.IsTileFullbright || tile.IsTileInvisible);
                    bool wallPaint = hasWall && tile.WallColor != PaintID.None;
                    bool wallCoat = hasWall && (tile.IsWallFullbright || tile.IsWallInvisible);
                    bool longMoss = tile.HasTile && tile.TileType == TileID.LongMoss;

                    bool anyCheck = scraperState1 switch
                    {
                        null => tilePaint || tileCoat || wallPaint || wallCoat || longMoss,
                        0 => scraperState2 == 0 ? tilePaint : tileCoat,
                        1 => scraperState2 == 0 ? wallPaint : wallCoat,
                        2 => longMoss,
                        _ => false
                    };

                    if (!anyCheck)
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

    public class LargePaintScraper1Dev : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper1Dev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .1111f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush11>();
    }

    public class LargePaintScraper11 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper11";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper12>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .1111f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush11>();
    }

    public class LargePaintScraper12 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper12";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper13>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .1111f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush12>();
    }

    public class LargePaintScraper13 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper13";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .1111f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush13>();
    }

    public class LargePaintScraper21 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper21";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .3333f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush21>();

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargePaintScraperTier1)
                .AddIngredient(ItemID.SpectreBar, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class LargePaintScraper22 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper22";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .3333f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush22>();

        protected override ModItem DecraftAs => ModContent.GetInstance<LargePaintScraper21>();
    }

    public class LargePaintScraper23 : LargePaintScraperBase
    {
        protected override string ItemName => "LargePaintScraper23";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .3f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePaintScraper21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 1, 25, 0);

        protected override float MossCollectionChance => .3333f;
        protected override ModItem AltChangeInto => ModContent.GetInstance<LargePaintBrush23>();

        protected override ModItem DecraftAs => ModContent.GetInstance<LargePaintScraper21>();
    }
}