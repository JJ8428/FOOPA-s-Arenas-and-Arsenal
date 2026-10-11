using FoopaArenaArsenal.Content.Items;
using System.IO;
using Terraria.ModLoader;

namespace FoopaArenaArsenal
{
	public class FoopaArenaArsenal : Mod
	{
		internal enum PacketType : byte
		{
			LargePickSound,
			LargeHammerSound,
			SlopeToolSound,
			LargeSpongeSound,
			LargeBottomlessBucketSound,
			LargeWallSound,
			LargeWallPlaceWallsRequest,
			LargeWallPlaceWallsGrant,
			LargeWallPlaceWallsDeny,
			LargeTileSound,
			LargeTilePlaceTilesRequest,
			LargeTilePlaceTilesGrant,
			LargeTilePlaceTilesDeny,
			ArenaBuilderSound,
            ArenaBuilderPlaceTilesRequest,
            ArenaBuilderPlaceTilesGrant,
            ArenaBuilderPlaceTilesDeny,
			LargePaintBrushPaintTilesRequest,
			LargePaintBrushPaintTilesGrant,
			LargePaintBrushPaintTilesDeny,
			LargePaintRollerPaintWallsRequest,
			LargePaintRollerPaintWallsGrant,
			LargePaintRollerPaintWallsDeny
        }

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			PacketType packetType = (PacketType)reader.ReadByte();

			switch (packetType)
			{
				case PacketType.LargePickSound:
					LargePickBase.HandleLargePickSound(reader, whoAmI);
					break;
				case PacketType.LargeHammerSound:
					LargeHammerBase.HandleLargeHammerSound(reader, whoAmI);
					break;
				case PacketType.SlopeToolSound:
					SlopeToolBase.HandleSlopeToolSound(reader, whoAmI);
					break;
				case PacketType.LargeSpongeSound:
					LargeSpongeBase.HandleLargeSpongeSound(reader, whoAmI);
					break;
				case PacketType.LargeBottomlessBucketSound:
                    LargeBottomlessBucketBase.HandleLargeBottomlessBucketSound(reader, whoAmI);
					break;
				case PacketType.LargeWallSound:
					LargeWallBase.HandleLargeWallSound(reader, whoAmI);
					break;
				case PacketType.LargeWallPlaceWallsRequest:
                    LargeWallBase.HandleLargeWallPlaceWallsRequest(reader, whoAmI);
                    break;
                case PacketType.LargeWallPlaceWallsGrant:
                    LargeWallBase.HandleLargeWallPlaceWallsGrant(reader, whoAmI);
                    break;
                case PacketType.LargeWallPlaceWallsDeny:
                    LargeWallBase.HandleLargeWallPlaceWallsDeny(reader, whoAmI);
                    break;
				case PacketType.LargeTileSound:
					LargeTileBase.HandleLargeTileSound(reader, whoAmI);
					break;
				case PacketType.LargeTilePlaceTilesRequest:
                    LargeTileBase.HandleLargeTilePlaceTilesRequest(reader, whoAmI);
                    break;
                case PacketType.LargeTilePlaceTilesGrant:
                    LargeTileBase.HandleLargeTilePlaceTilesGrant(reader, whoAmI);
                    break;
                case PacketType.LargeTilePlaceTilesDeny:
                    LargeTileBase.HandleLargeTilePlaceTilesDeny(reader, whoAmI);
                    break;
                case PacketType.ArenaBuilderSound:
                    ArenaBuilderBase.HandleArenaBuilderSound(reader, whoAmI);
                    break;
                case PacketType.ArenaBuilderPlaceTilesRequest:
                    ArenaBuilderBase.HandleArenaBuilderPlaceTilesRequest(reader, whoAmI);
                    break;
                case PacketType.ArenaBuilderPlaceTilesGrant:
                    ArenaBuilderBase.HandleArenaBuilderPlaceTilesGrant(reader, whoAmI);
                    break;
                case PacketType.ArenaBuilderPlaceTilesDeny:
                    ArenaBuilderBase.HandleArenaBuilderPlaceTilesDeny(reader, whoAmI);
                    break;
				case PacketType.LargePaintBrushPaintTilesRequest:
                    LargePaintBrushBase.HandleLargePaintBrushPaintTilesRequest(reader, whoAmI);
                    break;
                case PacketType.LargePaintBrushPaintTilesGrant:
                    LargePaintBrushBase.HandleLargePaintBrushPaintTilesGrant(reader, whoAmI);
                    break;
                case PacketType.LargePaintBrushPaintTilesDeny:
                    LargePaintBrushBase.HandleLargePaintBrushPaintTilesDeny(reader, whoAmI);
                    break;
				case PacketType.LargePaintRollerPaintWallsRequest:
                    LargePaintRollerBase.HandleLargePaintRollerPaintWallsRequest(reader, whoAmI);
                    break;
                case PacketType.LargePaintRollerPaintWallsGrant:
                    LargePaintRollerBase.HandleLargePaintRollerPaintWallsGrant(reader, whoAmI);
                    break;
                case PacketType.LargePaintRollerPaintWallsDeny:
                    LargePaintRollerBase.HandleLargePaintRollerPaintWallsDeny(reader, whoAmI);
                    break;
            }
		}
	}
}
