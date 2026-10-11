using FoopaArenaArsenal.Content.Projectiles;
using ReLogic.Content;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items
{
    public abstract class LargeActuationRodBase : ModItem
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

        protected abstract ModItem AltChangeInto { get; }
        public abstract bool NeedActuator { get; }

        public virtual int Sweep => 1 + 2 * SweepRadius;
        public virtual bool AltVariant => ItemName.EndsWith("Alt");

        public override string Texture => $"FoopaArenaArsenal/Content/Items/LargeActuationRod/{ItemName}";

        private bool wasLeftClickHeld = false;
        private bool? paintActuatorState;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            float sweepPenalty = 1 + (Penalty * SweepRadius);

            Item.attackSpeedOnlyAffectsWeaponAnimation = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.useTurn = true;

            Item.tileBoost = BonusRange;
            Item.rare = Rarity;
            Item.value = Price;

            Item.useTime = (int)(ToolSpeed * sweepPenalty);
            Item.useAnimation = (int)(ToolSpeed * 2.5);
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = Item.type;

            // if (SweepRadius != 0)
            // {
                int previewType = ModContent.ProjectileType<LargeActuationRodPreviewProj>();

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
            // }

            if (!player.controlUseItem && wasLeftClickHeld)
                paintActuatorState = null;

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
            else if (!AltVariant)
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
                else if (!AltVariant)
                    ChangeMode(player);

                return false;
            }

            return true;
        }

        private bool IsTileInReach(Player player, int tileX, int tileY)
        {
            bool inRangeX = player.position.X / 16f - Player.tileRangeX - Item.tileBoost <= tileX &&
                            (player.position.X + player.width) / 16f + Player.tileRangeX + Item.tileBoost - 1f >= tileX;

            bool inRangeY = player.position.Y / 16f - Player.tileRangeY - Item.tileBoost <= tileY &&
                            (player.position.Y + player.height) / 16f + Player.tileRangeY + Item.tileBoost - 2f >= tileY;

            return inRangeX && inRangeY;
        }

        private bool AttemptActuating(Player player, int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return false;

            if (player.ZoneLihzhardTemple && !NPC.downedGolemBoss)
                return false;

            if (NeedActuator && (tile == null || !tile.HasActuator))
                return false;

            if (tile.IsActuated == paintActuatorState.Value)
                return false;

            tile.IsActuated = paintActuatorState.Value;
            WorldGen.SquareTileFrame(x, y);
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (!player.controlUseItem || player.whoAmI != Main.myPlayer || player.HasBuff(BuffID.NoBuilding) || player.noBuilding)
                return true;

            int centerX = Player.tileTargetX;
            int centerY = Player.tileTargetY;

            if (!IsTileInReach(player, centerX, centerY))
                return true;

            Tile centerTile = Main.tile[centerX, centerY];

            if (centerTile == null)
                return true;

            if (paintActuatorState == null)
            {
                if (NeedActuator)
                {
                    if (centerTile.HasActuator)
                        paintActuatorState = !centerTile.IsActuated;
                    else
                        return true;
                }
                else
                    paintActuatorState = !centerTile.IsActuated;
            }

            bool anyTileModified = false;
            int offset = Sweep / 2;

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
                    bool isPlatform = TileID.Sets.Platforms[type];
                    bool isPlanter = type == TileID.PlanterBox;

                    if (Main.tileAxe[type] || Main.tileHammer[type] || (Main.tileFrameImportant[type] && !isPlatform && !isPlanter))
                        continue;

                    bool blockDamaged = false;
                    if (!TileLoader.CanKillTile(x, y, tile.TileType, ref blockDamaged))
                        continue;

                    if (AttemptActuating(player, x, y))
                        anyTileModified = true;
                }
            }

            if (anyTileModified)
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendTileSquare(Main.myPlayer, centerX, centerY, Sweep);

            return true;
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            string basePath = $"FoopaArenaArsenal/Content/Items/LargeActuationRod/{ItemName}";
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

                if (!AltVariant)
                {
                    string badgePath1 = $"FoopaArenaArsenal/Content/Items/ItemUI/{Badge}";
                    Texture2D badge1 = ModContent.Request<Texture2D>(badgePath1, AssetRequestMode.ImmediateLoad).Value;
                    Rectangle badgeFrame1 = new Rectangle(0, 0, badge1.Width, badge1.Height);
                    Vector2 badgeOrigin1 = new Vector2(badge1.Width / 2f, badge1.Height / 2f);
                    
                    Vector2 badgeOffset1 = isSelectedHotbarSlot
                        ? new Vector2(2.5f, 1.5f)
                        : new Vector2(-0.5f, -0.5f);

                    spriteBatch.Draw(badge1, position + badgeOffset1, badgeFrame1, drawColor, 0f, badgeOrigin1, 1.0f, SpriteEffects.None, 0f);
                }

                string actuatorReqOverlayPath = $"FoopaArenaArsenal/Content/Items/ItemUI/ActuatorToggle{(NeedActuator ? 1 : 0)}";
                Texture2D actuatorReqOverlay = ModContent.Request<Texture2D>(actuatorReqOverlayPath, AssetRequestMode.ImmediateLoad).Value;
                Rectangle actuatorReqOverlayFrame = new Rectangle(0, 0, actuatorReqOverlay.Width, actuatorReqOverlay.Height);
                Vector2 actuatorReqOverlayOrigin = new Vector2(actuatorReqOverlay.Width / 2f, actuatorReqOverlay.Height / 2f);

                Vector2 badgeOffset2 = isSelectedHotbarSlot
                    ? new Vector2(-3.0f, 1.5f)
                    : new Vector2(0.5f, -0.5f);

                spriteBatch.Draw(actuatorReqOverlay, position + badgeOffset2, actuatorReqOverlayFrame, drawColor, 0f, actuatorReqOverlayOrigin, 1.0f, SpriteEffects.None, 0f);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string actuatorReqLine = NeedActuator
                ? "with"
                : "without";

            string sweepLine = AltVariant
                ? "\n[c/FF6464:Sweep size locked to 1x1 while Actuators are not required]"
                : "\nRight Click to toggle sweep size";

            var itemTooltip = new TooltipLine(
                Mod,
                "LargeActuatorInfo",
                $"Actuate tiles {actuatorReqLine} Actuators" +
                sweepLine + 
                "\nAlt + Right Click to toggle if Actuators are required"
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

                    if (!tile.HasTile || !tile.HasActuator)
                        continue;

                    if (paintActuatorState == false && !tile.IsActuated)
                        continue;
                    if (paintActuatorState == true && tile.IsActuated)
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

    public class LargeActuationRod11 : LargeActuationRodBase
    {
        protected override string ItemName => "LargeActuationRod11";
        protected override string Badge => "Bar41";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.4f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeActuationRod12>();
        protected override int BonusRange => 24;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeActuationRod1Alt>();
        public override bool NeedActuator => true;
    }

    public class LargeActuationRod12 : LargeActuationRodBase
    {
        protected override string ItemName => "LargeActuationRod12";
        protected override string Badge => "Bar42";
        protected override int SweepRadius => 1;
        protected override float Penalty => 0.2f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeActuationRod13>();
        protected override int BonusRange => 24;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeActuationRod1Alt>();
        public override bool NeedActuator => true;
    }

    public class LargeActuationRod13 : LargeActuationRodBase
    {
        protected override string ItemName => "LargeActuationRod13";
        protected override string Badge => "Bar43";
        protected override int SweepRadius => 2;
        protected override float Penalty => 0.2f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeActuationRod14>();
        protected override int BonusRange => 24;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeActuationRod1Alt>();
        public override bool NeedActuator => true;
    }

    public class LargeActuationRod14 : LargeActuationRodBase
    {
        protected override string ItemName => "LargeActuationRod14";
        protected override string Badge => "Bar44";
        protected override int SweepRadius => 3;
        protected override float Penalty => 0.2f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => ModContent.GetInstance<LargeActuationRod11>();
        protected override int BonusRange => 24;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeActuationRod1Alt>();
        public override bool NeedActuator => true;
    }

    public class LargeActuationRod1Alt : LargeActuationRodBase
    {
        protected override string ItemName => "LargeActuationRod1Alt";
        protected override string Badge => "";
        protected override int SweepRadius => 0;
        protected override float Penalty => 0.2f;
        protected override int ToolSpeed => 5;
        protected override ModItem ChangeInto => null;
        protected override int BonusRange => 24;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Price => Item.sellPrice(0, 5, 0, 0);

        protected override ModItem AltChangeInto => ModContent.GetInstance<LargeActuationRod11>();
        public override bool NeedActuator => false;
    }

}