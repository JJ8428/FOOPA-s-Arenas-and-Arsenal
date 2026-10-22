using FoopaArenaArsenal.Content.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Players
{
    public class FAAToolsPlayer : ModPlayer
    {
        public Item BestPickItem;
        public Item BestHammerItem;

        public bool LargeTilePlaceTilesRequestPending;
        // public int LargeTilePlaceTilesRequestCooldown;
        public int LargeTilePlaceTilesRequestTimeout;
        public int LargeTileDisableCooldown;

        public bool LargeWallPlaceWallsRequestPending;
        // public int LargeWallPlaceWallsRequestCooldown;
        public int LargeWallPlaceWallsRequestTimeout;
        public int LargeWallDisableCooldown;

        public bool ArenaBuilderPlaceTilesRequestPending;
        // public int ArenaBuilderPlaceTilesRequestCooldown;
        public int ArenaBuilderPlaceTilesRequestTimeout;
        public int ArenaBuilderDisableCooldown;

        public bool LargePaintBrushPaintTilesRequestPending;
        public int LargePaintBrushPaintTilesRequestTimeout;
        public int LargePaintBrushDisableCooldown;

        public bool LargePaintRollerPaintWallsRequestPending;
        public int LargePaintRollerPaintWallsRequestTimeout;
        public int LargePaintRollerDisableCooldown;

        public override bool PreItemCheck()
        {
            if (Player.whoAmI != Main.myPlayer || !Main.SmartCursorIsUsed)
                return true;

            Point targetTile = Player.HeldItem.ModItem switch
            {
                // LargePickBase pick => pick.FindSmartCursorTarget(Player),
                // LargeHammerBase hammer => hammer.FindSmartCursorTarget(Player),
                LargeWallBase wall => wall.FindSmartCursorTarget(Player),
                LargeActuationRodBase rod when !rod.AltVariant => rod.FindSmartCursorTarget(Player),
                LargePaintBrushBase brush => brush.FindSmartCursorTarget(Player),
                LargePaintRollerBase roller => roller.FindSmartCursorTarget(Player),
                LargePaintScraperBase scraper => scraper.FindSmartCursorTarget(Player),
                _ => new Point(-1, -1)
            };

            if (targetTile.X != -1)
            {
                Main.SmartCursorX = targetTile.X;
                Main.SmartCursorY = targetTile.Y;
                Main.SmartCursorShowing = true;

                Player.tileTargetX = targetTile.X;
                Player.tileTargetY = targetTile.Y;
            }

            return true;
        }

        public override void PreUpdate()
        {
            BestPickItem = null;
            BestHammerItem = null;

            Item[][] inventories = { Player.inventory, Player.bank.item, Player.bank2.item };

            foreach (Item[] inventory in inventories)
            {
                foreach (Item invItem in inventory)
                {
                    if (invItem == null || invItem.IsAir || invItem.ModItem is LargePickBase || invItem.ModItem is LargeHammerBase)
                        continue;

                    if (invItem.pick > 0 && (BestPickItem == null || invItem.pick > BestPickItem.pick))
                        BestPickItem = invItem;

                    if (invItem.hammer > 0 && (BestHammerItem == null || invItem.hammer > BestHammerItem.hammer))
                        BestHammerItem = invItem;
                }
            }

            foreach (Item[] inventory in inventories)
            {
                foreach (Item invItem in inventory)
                {
                    if (invItem == null || invItem.IsAir)
                        continue;

                    switch (invItem.ModItem)
                    {
                        case LargePickBase pick: pick.ApplyDynamicStats(invItem, Player); break;
                        case LargeHammerBase hammer: hammer.ApplyDynamicStats(invItem, Player); break;
                        case LargeTileBase tile: tile.ApplyDynamicStats(invItem, Player); break;
                        case LargeWallBase wall: wall.ApplyDynamicStats(invItem, Player); break;
                    }
                }
            }
        }

        public override void PostUpdate()
        {
            // CountDown(ref LargeTilePlaceTilesRequestCooldown);
            // CountDown(ref LargeWallPlaceWallsRequestCooldown);
            // CountDown(ref ArenaBuilderPlaceTilesRequestCooldown);

            CountDown(ref LargeTileDisableCooldown);
            CountDown(ref LargeWallDisableCooldown);
            CountDown(ref ArenaBuilderDisableCooldown);
            CountDown(ref LargePaintBrushDisableCooldown);
            CountDown(ref LargePaintRollerDisableCooldown);

            TimeOut(ref LargeTilePlaceTilesRequestPending, ref LargeTilePlaceTilesRequestTimeout);
            TimeOut(ref LargeWallPlaceWallsRequestPending, ref LargeWallPlaceWallsRequestTimeout);
            TimeOut(ref ArenaBuilderPlaceTilesRequestPending, ref ArenaBuilderPlaceTilesRequestTimeout);
            TimeOut(ref LargePaintBrushPaintTilesRequestPending, ref LargePaintBrushPaintTilesRequestTimeout);
            TimeOut(ref LargePaintRollerPaintWallsRequestPending, ref LargePaintRollerPaintWallsRequestTimeout);
        }

        private static void CountDown(ref int cooldown)
        {
            if (cooldown > 0)
                cooldown--;
        }

        private static void TimeOut(ref bool pending, ref int timeout)
        {
            if (!pending)
                return;

            timeout--;
            if (timeout <= 0)
                pending = false;
        }
    }
}