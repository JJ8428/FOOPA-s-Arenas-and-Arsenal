using FoopaArenaArsenal.Content.Projectiles;
using FoopaArenaArsenal.Content.Players;
using FoopaArenaArsenal.Common.Systems;
using ReLogic.Content;
using System;
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
    public abstract class LargeHammerBase : ModItem
    {
        protected abstract string ItemName { get; }
        protected abstract string Badge { get; }
        public abstract int SweepRadius { get; }
        protected abstract float Penalty { get; }
        protected abstract ModItem ChangeInto { get; }
        protected abstract int BonusRange { get; }
        protected abstract int Rarity { get; }
        protected abstract int Price { get; }

        // public bool KillHiddenWalls { get; set; } = false;

        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeHammer/{ItemName}";

        protected virtual ModItem DecraftAs => null;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            if (DecraftAs != null)
                ItemID.Sets.ShimmerCountsAsItem[Type] = DecraftAs.Type;
        }

        public override void SetDefaults()
        {
            Item.damage = 24;
            Item.crit = 4;
            Item.useAnimation = 24;
            Item.useTime = 14;
            Item.knockBack = 6f;
            Item.hammer = 1;

            Item.attackSpeedOnlyAffectsWeaponAnimation = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.useTurn = true;

            Item.DamageType = DamageClass.Melee;
            Item.tileBoost = BonusRange;
            Item.rare = Rarity;
            Item.value = Price;
            Item.UseSound = SoundID.Item1;

            ItemID.Sets.CanGetPrefixes[Type] = false;
        }

        public override bool CanReforge() => false;

        private int EffectiveHammerPower;

        public void ApplyDynamicStats(Item item, Player player)
        {
            var faaPlayer = player.GetModPlayer<FAAToolsPlayer>();
            float sweepPenalty = 1f + (Penalty * (SweepRadius * 3f));

            if (faaPlayer.BestHammerItem != null)
            {
                item.hammer = Math.Max(70, faaPlayer.BestHammerItem.hammer);
                EffectiveHammerPower = Math.Max(70, faaPlayer.BestHammerItem.hammer);
                item.damage = faaPlayer.BestHammerItem.damage;
                item.knockBack = faaPlayer.BestHammerItem.knockBack;
                item.useTime = (int)(faaPlayer.BestHammerItem.useTime * sweepPenalty * .75f);
                item.useAnimation = (int)(faaPlayer.BestHammerItem.useAnimation * sweepPenalty * .5f);
            }
            else
            {
                item.hammer = 70;
                EffectiveHammerPower = 70;
                item.damage = 24;
                item.knockBack = 6f;
                item.useTime = (int)(14 * sweepPenalty * .8f);
                item.useAnimation = (int)(15 * sweepPenalty * .5f);
            }
        }

        public static bool IsWholeBlock(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return false;

            if (Main.tileFrameImportant[tile.TileType])
                return false;

            return Main.tileSolid[tile.TileType];
        }

        public static bool IsHiddenWall(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!IsWholeBlock(x + dx, y + dy))
                        return false;
                }
            }

            return true;
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            bool cursorOnSuppressedHiddenWall = IsHiddenWall(centerX, centerY);
            // !KillHiddenWalls && IsHiddenWall(centerX, centerY);

            Item.hammer = cursorOnSuppressedHiddenWall ? 0 : EffectiveHammerPower;

            if (SweepRadius != 0)
            {
                int previewType = ModContent.ProjectileType<LargeHammerPreviewProj>();

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

            if (newItem.ModItem is LargeHammerBase newHammer)
            {
                newHammer.ApplyDynamicStats(newItem, player);
                // newHammer.KillHiddenWalls = KillHiddenWalls;
            }

            player.inventory[slotIndex] = newItem;

            SoundEngine.PlaySound(SoundID.Unlock, player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slotIndex);
        }

        /*
        private void ToggleKillHiddenWalls(Player player)
        {
            KillHiddenWalls = !KillHiddenWalls;

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
        */

        public override void RightClick(Player player)
        {
            /*
            if (AltHeld())
                ToggleKillHiddenWalls(player);
            else
                ChangeMode(player);
            */
            ChangeMode(player);
        }

        /*
        private static bool AltHeld()
        {
            return Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)
                || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt);
        }
        */

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                /*
                if (AltHeld())
                    ToggleKillHiddenWalls(player);
                else
                    ChangeMode(player);
                */
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

        private static void AttemptPicking(Player player, int x, int y, int hammerPower)
        {
            if (Main.tile[x, y].WallType == WallID.None)
                return;

            for (int a = 0; a < 5; a++)
            {
                if (Main.tile[x, y].WallType == WallID.None)
                    return;
                
                player.PickWall(x, y, hammerPower);
            }
        }

        private bool HasEntryPoint(int centerX, int centerY)
        {
            int scanRadius = Sweep / 2;

            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                for (int dy = -scanRadius; dy <= scanRadius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (!WorldGen.InWorld(x, y))
                        continue;

                    ushort wall = Main.tile[x, y].WallType;
                    if (wall == WallID.None || Main.wallHouse[wall])
                        return true;
                }
            }

            return false;
        }

        private bool isProcessingSweep = false;

        public void ExpandBreakFromCenter(Player player, int centerX, int centerY)
        {
            if (isProcessingSweep)
                return;

            if (!player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return;

            isProcessingSweep = true;
            try
            {
                bool anyTileModified = false;
                int offset = Sweep / 2;

                if (!HasEntryPoint(centerX, centerY))
                    return;

                for (int dx = -offset; dx <= offset; dx++)
                {
                    for (int dy = -offset; dy <= offset; dy++)
                    {
                        int x = centerX + dx;
                        int y = centerY + dy;

                        if (!WorldGen.InWorld(x, y))
                            continue;

                        Tile tile = Main.tile[x, y];

                        if (tile == null || tile.WallType == WallID.None)
                            continue;

                        if (!WallLoader.CanPlace(x, y, tile.WallType))
                            continue;

                        int bufferSlot = player.hitTile.TryFinding(x, y, 2);
                        if (bufferSlot != -1)
                            player.hitTile.Clear(bufferSlot);

                        // if (!KillHiddenWalls)
                        if (Main.SmartCursorWanted)
                        {
                            if (IsHiddenWall(x, y))
                                continue;
                        }

                        AttemptPicking(player, x, y, EffectiveHammerPower);

                        bufferSlot = player.hitTile.TryFinding(x, y, 2);
                        if (bufferSlot != -1)
                            player.hitTile.Clear(bufferSlot);

                        if (!anyTileModified && Main.tile[x, y].WallType == WallID.None)
                            anyTileModified = true;
                    }
                } 

                if (anyTileModified)
                {
                    SoundEngine.PlaySound(SoundID.Dig, player.Center.ToWorldCoordinates());
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendTileSquare(Main.myPlayer, centerX, centerY, Sweep);

                        ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                        packet.Write((byte)FoopaArenaArsenal.PacketType.LargeHammerSound);
                        packet.Write(centerX);
                        packet.Write(centerY);
                        packet.Send();
                    }
                }
            }
            finally
            {
                isProcessingSweep = false;
            }
        }

        public override bool? UseItem(Player player)
        {       
            if (!player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return false;

            if (WorldGen.InWorld(centerX, centerY))
            {
                Tile tile = Main.tile[centerX, centerY];

                ushort type = tile.TileType;
                if (tile != null && tile.HasTile && Main.tileHammer[type])
                {
                    WorldGen.KillTile(centerX, centerY, fail: false);
                    return true;
                }
            }

            ExpandBreakFromCenter(player, centerX, centerY);

            return true;
        }
        
        public static void HandleLargeHammerSound(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket relay = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                relay.Write((byte)FoopaArenaArsenal.PacketType.LargeHammerSound);
                relay.Write(x);
                relay.Write(y);
                relay.Send(-1, whoAmI);
            }
            else
            {
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
            }
        }

        /*
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(KillHiddenWalls);
        }

        public override void NetReceive(BinaryReader reader)
        {
            KillHiddenWalls = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag)
        {
            tag["KillHiddenWalls"] = KillHiddenWalls;
        }

        public override void LoadData(TagCompound tag)
        {
            KillHiddenWalls = tag.GetBool("KillHiddenWalls");
        }
        */

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeHammer/{ItemName}";
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

                Vector2 badgeOffset1 = isSelectedHotbarSlot
                    ? new Vector2(2.5f, 1.5f)
                    : new Vector2(-0.5f, -0.5f);

                spriteBatch.Draw(badge, position + badgeOffset1, badgeFrame, drawColor, 0f, badgeOrigin, 1.0f, SpriteEffects.None, 0f);
            
                /*
                string wallTogglePath = $"FoopaArenaArsenal/Content/Items/ItemUI/HiddenWallsToggle{(KillHiddenWalls ? 1 : 0)}";
                Texture2D wallToggle = ModContent.Request<Texture2D>(wallTogglePath, AssetRequestMode.ImmediateLoad).Value;
                Rectangle wallToggleFrame = new Rectangle(0, 0, wallToggle.Width, wallToggle.Height);
                Vector2 wallToggleOrigin = new Vector2(wallToggle.Width / 2f, wallToggle.Height / 2f);

                Vector2 badgeOffset2 = isSelectedHotbarSlot
                    ? new Vector2(-3.0f, 1.5f)
                    : new Vector2(0.5f, -0.5f);

                spriteBatch.Draw(wallToggle, position + badgeOffset2, wallToggleFrame, drawColor, 0f, wallToggleOrigin, 1.0f, SpriteEffects.None, 0f);
                */
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var faaPlayer = Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>();
            string bestHammerLine = faaPlayer.BestHammerItem != null
                ? $"[c/64FF64:Matching:] {faaPlayer.BestHammerItem.Name} ([i:{faaPlayer.BestHammerItem.type}])"
                : "[c/64FF64:Matching:] [c/FF6464:None - no hammer found in inventories]";

            string materialsLine = ItemName[^2] == '1' ? "Materials\n" : "";

            tooltips.RemoveAll(line => line.Mod == "Terraria" &&
                (line.Name == "TileBoost" || line.Name == "Material" || line.Name == "HammerPower"));

            var itemTooltip = new TooltipLine(
                Mod,
                "LargeHammerInfo",
                $"{(faaPlayer.BestHammerItem != null ? faaPlayer.BestHammerItem.hammer : 70)}% hammer power\n" +
                $"+{Item.tileBoost} range" +
                $"\n{materialsLine}" +
                "Traits scale with the strongest hammer in your inventory, piggy bank, and safe" +
                $"\n{bestHammerLine}" +
                "\nRight Click to toggle sweep size" // +
                // $"\nAlt + Right Click to {(KillHiddenWalls ? "disable" : "enable")} destorying hidden walls"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }

        /*
        private static List<Point> GetLinePoints(Point from, Point to)
        {
            List<Point> points = new List<Point>();
            Utils.PlotLine(new Point16(from.X, from.Y), new Point16(to.X, to.Y), (x, y) =>
            {
                points.Add(new Point(x, y));
                return true;
            });
            return points;
        }

        private bool HasClearWallPath(Point from, Point to) // Make STATIC
        {
            List<Point> points = GetLinePoints(from, to);

            foreach (Point p in points)
            {
                if (p == from || p == to)
                    continue;

                if (!WorldGen.InWorld(p.X, p.Y))
                    continue;

                Tile tile = Main.tile[p.X, p.Y];

                if (tile.WallType != WallID.None && !IsHiddenWall(p.X, p.Y) && Main.wallHouse[tile.WallType])
                    return false;
            }

            return true;
        }

        public Point FindSmartCursorTarget(Player player)
        {
            Point centerTile = Main.MouseWorld.ToTileCoordinates();
            Point playerCenterTile = player.Center.ToTileCoordinates();
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

                    Point p = new Point(x, y);
                    if (tile.WallType == WallID.None || IsHiddenWall(x, y) || !HasEntryPoint(x, y, 1) || !HasClearWallPath(playerCenterTile, p))
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
        */
    }

    public class LargeHammer1Dev : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer1Dev";
        protected override string Badge => "";
        public override int SweepRadius => 0;
        protected override float Penalty => .5f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHammer11 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer11";
        protected override string Badge => "Bar31";
        public override int SweepRadius => 0;
        protected override float Penalty => .25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer12>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHammer12 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer12";
        protected override string Badge => "Bar32";
        public override int SweepRadius => 1;
        protected override float Penalty => .25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer13>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHammer13 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer13";
        protected override string Badge => "Bar33";
        public override int SweepRadius => 2;
        protected override float Penalty => .25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargeHammer21 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer21";
        protected override string Badge => "Bar41";
        public override int SweepRadius => 0;
        protected override float Penalty => 0.1f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargeHammerTier1)
                .AddIngredient(ItemID.ShroomiteBar, 12)
                .AddTile(TileID.MythrilAnvil)
                .AddCustomShimmerResult(ModContent.ItemType<LargeHammer11>())
                .AddCustomShimmerResult(ItemID.ShroomiteBar, 12)
                .Register();
        }
    }

    public class LargeHammer22 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer22";
        protected override string Badge => "Bar42";
        public override int SweepRadius => 1;
        protected override float Penalty => 0.1f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargeHammer21>();
    }

    public class LargeHammer23 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer23";
        protected override string Badge => "Bar43";
        public override int SweepRadius => 2;
        protected override float Penalty => .1f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer24>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargeHammer21>();
    }

    public class LargeHammer24 : LargeHammerBase
    {
        protected override string ItemName => "LargeHammer24";
        protected override string Badge => "Bar44";
        public override int SweepRadius => 3;
        protected override float Penalty => .1f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeHammer21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargeHammer21>();
    }
}