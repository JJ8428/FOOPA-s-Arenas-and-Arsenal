using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FoopaArenaArsenal.Content.Tiles
{
    public class DummyWall : ModWall
    {
        public override void SetStaticDefaults()
        {
            DustType = -1;
            HitSound = null; 
        }

        public override bool Drop(int i, int j, ref int dropType)
        {
            dropType = 0;
            return true;
        }

        /*
        public override bool CanPlace(int i, int j)
        {
            return Main.tile[i, j].WallType == WallID.None;
        }

        public override void PlaceInWorld(int i, int j, Item item)
        {
            WorldGen.KillWall(i, j, false);
        }
        */
    }
}