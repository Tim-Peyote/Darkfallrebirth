using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Turns logical floor cells into stable context tiles without depending on view assets.</summary>
    public static class DungeonFloorTileResolver
    {
        public const int VariantCount = 4;

        public static void Resolve(DungeonData dungeon, int seed)
        {
            var result = new List<DungeonResolvedFloorTile>();
            var variants = new byte[dungeon.Width, dungeon.Height];
            for (var y = 0; y < dungeon.Height; y++)
            for (var x = 0; x < dungeon.Width; x++)
            {
                if (!dungeon.IsFloor(x, y)) continue;
                var neighbours = Neighbours(dungeon, x, y);
                var kind = Classify(neighbours);
                var variant = (byte)(PositiveHash(seed, x, y, 17) % VariantCount);
                // A local correction rule prevents the most visible tiled-floor defect: two
                // equal modules touching in reading order. It remains deterministic per seed.
                var westVariant = x > 0 && dungeon.IsFloor(x - 1, y) ? variants[x - 1, y] : byte.MaxValue;
                var southVariant = y > 0 && dungeon.IsFloor(x, y - 1) ? variants[x, y - 1] : byte.MaxValue;
                for (var attempt = 0; attempt < VariantCount &&
                     (variant == westVariant || variant == southVariant); attempt++)
                    variant = (byte)((variant + 1) % VariantCount);
                variants[x, y] = variant;
                var protectedCell = (dungeon.SemanticsAt(x, y) &
                                     (DungeonCellSemantic.Arrival | DungeonCellSemantic.Exit)) != 0;
                var damaged = PositiveHash(seed, x, y, 53) % 11 == 0 && !protectedCell;
                result.Add(new DungeonResolvedFloorTile(new Vector2Int(x, y), kind, neighbours, variant, damaged));
            }
            dungeon.SetResolvedFloorTiles(result);
        }

        public static DungeonFloorNeighbours Neighbours(DungeonData dungeon, int x, int y)
        {
            var result = DungeonFloorNeighbours.None;
            if (dungeon.IsFloor(x - 1, y)) result |= DungeonFloorNeighbours.West;
            if (dungeon.IsFloor(x + 1, y)) result |= DungeonFloorNeighbours.East;
            if (dungeon.IsFloor(x, y - 1)) result |= DungeonFloorNeighbours.South;
            if (dungeon.IsFloor(x, y + 1)) result |= DungeonFloorNeighbours.North;
            if (dungeon.IsFloor(x - 1, y - 1)) result |= DungeonFloorNeighbours.SouthWest;
            if (dungeon.IsFloor(x + 1, y - 1)) result |= DungeonFloorNeighbours.SouthEast;
            if (dungeon.IsFloor(x - 1, y + 1)) result |= DungeonFloorNeighbours.NorthWest;
            if (dungeon.IsFloor(x + 1, y + 1)) result |= DungeonFloorNeighbours.NorthEast;
            return result;
        }

        public static DungeonFloorTileKind Classify(DungeonFloorNeighbours neighbours)
        {
            const DungeonFloorNeighbours cardinal = DungeonFloorNeighbours.West | DungeonFloorNeighbours.East |
                                                     DungeonFloorNeighbours.South | DungeonFloorNeighbours.North;
            var sides = neighbours & cardinal;
            var count = Count((byte)sides);
            if (count == 0) return DungeonFloorTileKind.Isolated;
            if (count == 1) return DungeonFloorTileKind.End;
            if (count == 2)
            {
                var opposite = sides == (DungeonFloorNeighbours.West | DungeonFloorNeighbours.East) ||
                               sides == (DungeonFloorNeighbours.South | DungeonFloorNeighbours.North);
                return opposite ? DungeonFloorTileKind.Straight : DungeonFloorTileKind.OuterCorner;
            }
            if (count == 3) return DungeonFloorTileKind.Edge;
            var allDiagonals = DungeonFloorNeighbours.SouthWest | DungeonFloorNeighbours.SouthEast |
                               DungeonFloorNeighbours.NorthWest | DungeonFloorNeighbours.NorthEast;
            return (neighbours & allDiagonals) == allDiagonals
                ? DungeonFloorTileKind.Center : DungeonFloorTileKind.InnerCorner;
        }

        private static int Count(byte value)
        {
            var count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private static int PositiveHash(int seed, int x, int y, int salt)
        {
            unchecked
            {
                var hash = seed ^ (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791);
                hash ^= hash >> 16;
                return hash & int.MaxValue;
            }
        }
    }
}
