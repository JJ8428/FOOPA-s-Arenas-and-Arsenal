using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Tiles
{
    public class DummyTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            DustType = -1;
            HitSound = null;
        }

        public override bool CanDrop(int i, int j)
        {
            return false;
        }
    }
}