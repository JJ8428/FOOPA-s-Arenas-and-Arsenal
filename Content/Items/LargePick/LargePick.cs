using FoopaArenaArsenal.Common.Systems;
using FoopaArenaArsenal.Content.Players;
using FoopaArenaArsenal.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargePickBase : ModItem
    {
        protected abstract string ItemName { get; }
        protected abstract string Badge { get; }
        protected abstract int SweepRadius { get; }
        protected abstract float Penalty { get; }
        protected abstract ModItem ChangeInto { get; }
        protected abstract int BonusRange { get; }
        protected abstract int Rarity { get; }
        protected abstract int Price { get; }

        public virtual int Sweep => 1 + 2 * SweepRadius;

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargePick/{ItemName}";

        protected virtual ModItem DecraftAs => null;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            if (DecraftAs != null)
                ItemID.Sets.ShimmerCountsAsItem[Type] = DecraftAs.Type;
        }

        public override void SetDefaults()
        {
            Item.damage = 8;
            Item.crit = 4;
            Item.useAnimation = 19;
            Item.useTime = 11;
            Item.knockBack = 3f;
            Item.pick = 1;

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

        private int EffectivePickPower;

        public void ApplyDynamicStats(Item item, Player player)
        {
            var faaPlayer = player.GetModPlayer<FAAToolsPlayer>();
            float sweepPenalty = 1f + (Penalty * (SweepRadius * 3f));

            if (faaPlayer.BestPickItem != null)
            {
                item.pick = (Main.SmartCursorWanted ? Math.Max(55, faaPlayer.BestPickItem.pick) : 0);
                EffectivePickPower = Math.Max(55, faaPlayer.BestPickItem.pick);
                item.damage = faaPlayer.BestPickItem.damage;
                item.knockBack = faaPlayer.BestPickItem.knockBack;
                item.useTime = (int)(faaPlayer.BestPickItem.useTime * sweepPenalty * .75f);
                item.useAnimation = (int)(faaPlayer.BestPickItem.useAnimation * sweepPenalty * .5f);
            }
            else
            {
                item.pick = (Main.SmartCursorWanted ? 55 : 0);
                EffectivePickPower = 55;
                item.damage = 8;
                item.knockBack = 3f;
                item.useTime = (int)(11 * sweepPenalty * .75f);
                item.useAnimation = (int)(15 * sweepPenalty * .5f);
            }
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            if (SweepRadius != 0)
            {
                int previewType = ModContent.ProjectileType<LargePickPreviewProj>();

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

            if (newItem.ModItem is LargePickBase newPick)
                newPick.ApplyDynamicStats(newItem, player);

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

        static int nightmarePickPower = ContentSamples.ItemsByType[ItemID.NightmarePickaxe].pick;
        static List<int> potentialEvilTiles = ModContent.GetContent<ModTile>()
            .Where(tile => tile.MinPick == nightmarePickPower)
            .Select(tile => (int)tile.Type)
            .ToList();

        private static void AttemptPicking(Player player, int x, int y, int pickPower)
        {
            if (!Main.tile[x, y].HasTile)
                return;

            for (int a = 0; a < 2; a++)
            {
                if (!Main.tile[x, y].HasTile)
                    return;

                Tile tile = Main.tile[x, y];
                bool isEvilStone = tile.TileType == TileID.Ebonstone
                    || tile.TileType == TileID.Crimstone
                    || potentialEvilTiles.Contains(tile.TileType);
                player.PickTile(x, y, isEvilStone ? 100 : pickPower);
            }
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding || !Main.mouseItem.IsAir)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            bool anyTileModified = false;
            int offset = Sweep / 2;

            if (player.controlUseItem)
            {
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

                        bool blockDamaged = false;
                        if (!TileLoader.CanKillTile(x, y, tile.TileType, ref blockDamaged))
                            continue;

                        ushort type = tile.TileType;

                        if (Main.tileAxe[type] || Main.tileHammer[type])
                            continue;

                        int bufferSlot = player.hitTile.TryFinding(x, y, 1);
                        if (bufferSlot != -1)
                            player.hitTile.Clear(bufferSlot);

                        AttemptPicking(player, x, y, EffectivePickPower);

                        bufferSlot = player.hitTile.TryFinding(x, y, 1);
                        if (bufferSlot != -1)
                            player.hitTile.Clear(bufferSlot);

                        if (!anyTileModified)
                            if (!Main.tile[x, y].HasTile)
                                anyTileModified = true;
                    }
                }
            }

            if (anyTileModified)
            {
                SoundEngine.PlaySound(SoundID.Dig, player.Center.ToWorldCoordinates());
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendTileSquare(-1, centerX, centerY, Sweep);

                    ModPacket packet = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                    packet.Write((byte)FoopaArenaArsenal.PacketType.LargePickSound);
                    packet.Write(centerX);
                    packet.Write(centerY);
                    packet.Send();
                }
            }

            return true;
        }

        public static void HandleLargePickSound(BinaryReader reader, int whoAmI)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket relay = ModContent.GetInstance<FoopaArenaArsenal>().GetPacket();
                relay.Write((byte)FoopaArenaArsenal.PacketType.LargePickSound);
                relay.Write(x);
                relay.Write(y);
                relay.Send(-1, whoAmI);
            }
            else
            {
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(x * 16, y * 16));
            }
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargePick/{ItemName}";
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
            var faaPlayer = Main.LocalPlayer.GetModPlayer<FAAToolsPlayer>();
            string bestPickLine = faaPlayer.BestPickItem != null
                ? $"[c/64FF64:Matching:] {faaPlayer.BestPickItem.Name} ([i:{faaPlayer.BestPickItem.type}])"
                : "[c/64FF64:Matching:] [c/FF6464:None - no pickaxe found in inventories]";

            List<int> iconItems = new List<int> { ItemID.EbonstoneBlock, ItemID.CrimstoneBlock };
            foreach (int tileType in potentialEvilTiles)
            {
                int itemType = TileLoader.GetItemDropFromTypeAndStyle(tileType);
                if (itemType > 0 && !iconItems.Contains(itemType))
                    iconItems.Add(itemType);
            }

            string evilTilesLine = "Able to mine: ";
            foreach (int itemType in iconItems)
                evilTilesLine += $"[i:{itemType}]";

            string materialsLine = ItemName[^2] == '1' ? "Materials\n" : "";

            tooltips.RemoveAll(line => line.Mod == "Terraria" &&
                (line.Name == "TileBoost" || line.Name == "Material" || line.Name == "PickPower"));

            var itemTooltip = new TooltipLine(
                Mod,
                "LargePickInfo",
                $"{(faaPlayer.BestPickItem != null ? faaPlayer.BestPickItem.pick : 55)}% pickaxe power\n" +
                $"+{Item.tileBoost} range" +
                $"\n{materialsLine}" +
                $"{evilTilesLine}" +
                "\nTraits scale with the strongest pickaxe in your inventory, piggy bank, and safe" +
                $"\n{bestPickLine}" +
                "\nIneffective in biomes that encourage navigation over destruction" +
                "\nRight Click to toggle sweep size"
            );

            int journeyIndex = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "JourneyResearch");

            if (journeyIndex != -1)
                tooltips.Insert(journeyIndex, itemTooltip);
            else
                tooltips.Add(itemTooltip);
        }
    }
    
    public class LargePick1Dev : LargePickBase
    {
        protected override string ItemName => "LargePick1Dev";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => .35f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargePick11 : LargePickBase
    {
        protected override string ItemName => "LargePick11";
        protected override string Badge => "Bar31";
        protected override int SweepRadius => 0;
        protected override float Penalty => .35f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick12>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargePick12 : LargePickBase
    {
        protected override string ItemName => "LargePick12";
        protected override string Badge => "Bar32";
        protected override int SweepRadius => 1;
        protected override float Penalty => .35f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick13>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargePick13 : LargePickBase
    {
        protected override string ItemName => "LargePick13";
        protected override string Badge => "Bar33";
        protected override int SweepRadius => 2;
        protected override float Penalty => .35f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick11>();
        protected override int BonusRange => 6;
        protected override int Rarity => ItemRarityID.Blue;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);
    }

    public class LargePick21 : LargePickBase
    {
        protected override string ItemName => "LargePick21";
        protected override string Badge => "Bar41";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick22>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(FAARecipeSystem.LargePickTier1)
                .AddIngredient(ItemID.ShroomiteBar, 12)
                .AddTile(TileID.MythrilAnvil)
                .AddCustomShimmerResult(ModContent.ItemType<LargePick11>())
                .AddCustomShimmerResult(ItemID.ShroomiteBar, 12)
                .Register();
        }
    }

    public class LargePick22 : LargePickBase
    {
        protected override string ItemName => "LargePick22";
        protected override string Badge => "Bar42";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick23>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargePick21>();
    }

    public class LargePick23 : LargePickBase
    {
        protected override string ItemName => "LargePick23";
        protected override string Badge => "Bar43";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick24>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargePick21>();
    }

    public class LargePick24 : LargePickBase
    {
        protected override string ItemName => "LargePick24";
        protected override string Badge => "Bar44";
        protected override int SweepRadius => 3;
        protected override float Penalty => 0.25f;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargePick21>();
        protected override int BonusRange => 12;
        protected override int Rarity => ItemRarityID.Red;
        protected override int Price => Item.sellPrice(0, 7, 50, 0);

        protected override ModItem DecraftAs => ModContent.GetInstance<LargePick21>();
    }
}