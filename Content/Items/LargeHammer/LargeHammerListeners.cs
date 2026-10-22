using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Items.LargeHammer
{
    public class WallBreakListener : GlobalWall
    {
        public override void KillWall(int i, int j, int type, ref bool fail)
        {
            Player player = Main.LocalPlayer;

            if (!player.controlUseItem ||
                player.HeldItem?.ModItem is not LargeHammerBase largeHammer ||
                player.whoAmI != Main.myPlayer ||
                player.HasBuff(BuffID.NoBuilding) ||
                player.noBuilding)
                return;

            largeHammer.ExpandBreakFromCenter(player, i, j);
        }
    }

    public class DisableHammerPoundListener : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        {
            Player player = Main.LocalPlayer;

            if (player.HeldItem?.ModItem is not LargeHammerBase || Main.tileHammer[type] || type == TileID.ShadowOrbs)
                return true;

            return false;
        }
    }
}