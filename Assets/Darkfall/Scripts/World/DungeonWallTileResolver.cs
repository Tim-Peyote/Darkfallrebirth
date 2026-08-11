using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    public static class DungeonWallTileResolver
    {
        private readonly struct Unit
        {
            public readonly bool Vertical;
            public readonly int Fixed;
            public readonly int Start;
            public Unit(bool vertical, int fixedCoordinate, int start)
            { Vertical = vertical; Fixed = fixedCoordinate; Start = start; }
        }

        public static void Resolve(DungeonData dungeon, int seed)
        {
            var units = CollectBoundaryUnits(dungeon);
            var modules = new List<DungeonResolvedWallModule>(units.Count);
            units.Sort((a, b) => a.Vertical != b.Vertical ? a.Vertical.CompareTo(b.Vertical) :
                a.Fixed != b.Fixed ? a.Fixed.CompareTo(b.Fixed) : a.Start.CompareTo(b.Start));
            for (var runStart = 0; runStart < units.Count;)
            {
                var runEnd = runStart + 1;
                while (runEnd < units.Count && units[runEnd].Vertical == units[runStart].Vertical &&
                       units[runEnd].Fixed == units[runStart].Fixed &&
                       units[runEnd].Start == units[runEnd - 1].Start + 1) runEnd++;
                var length = runEnd - runStart;
                var previousVariant = byte.MaxValue;
                for (var index = runStart; index < runEnd; index++)
                {
                    var unit = units[index];
                    var section = index - runStart;
                    var variant = (byte)(Hash(seed, unit.Fixed, unit.Start, unit.Vertical ? 31 : 47) % 3);
                    if (variant == previousVariant) variant = (byte)((variant + 1) % 3);
                    previousVariant = variant;
                    var kind = DungeonWallModuleKind.Face;
                    // Accents need ordinary wall on both sides; this prevents broken art at a
                    // corner, door cheek or one-cell passage.
                    if (!unit.Vertical && length >= 7 && section >= 2 && section <= length - 3)
                    {
                        var accent = Hash(seed, unit.Fixed, unit.Start, 73) % 13;
                        if (accent == 0) kind = DungeonWallModuleKind.Broken;
                        else if (accent == 1) kind = DungeonWallModuleKind.Niche;
                        else if (accent == 2) kind = DungeonWallModuleKind.Arcade;
                    }
                    var anchor = unit.Vertical
                        ? new Vector2(unit.Fixed, unit.Start + .5f)
                        : new Vector2(unit.Start + .5f, unit.Fixed);
                    modules.Add(new DungeonResolvedWallModule(anchor, unit.Vertical, kind, variant));
                }
                runStart = runEnd;
            }

            var corners = new List<DungeonResolvedWallCorner>();
            for (var x = 0; x <= dungeon.Width; x++)
            for (var y = 0; y <= dungeon.Height; y++)
            {
                byte quadrants = 0;
                if (dungeon.IsFloor(x - 1, y - 1)) quadrants |= 1;
                if (dungeon.IsFloor(x, y - 1)) quadrants |= 2;
                if (dungeon.IsFloor(x - 1, y)) quadrants |= 4;
                if (dungeon.IsFloor(x, y)) quadrants |= 8;
                var count = Count(quadrants);
                if (count == 1) corners.Add(new DungeonResolvedWallCorner(new Vector2(x, y),
                    DungeonWallCornerKind.Outer, quadrants));
                else if (count == 3) corners.Add(new DungeonResolvedWallCorner(new Vector2(x, y),
                    DungeonWallCornerKind.Inner, quadrants));
            }
            dungeon.SetResolvedWalls(modules, corners);
        }

        private static List<Unit> CollectBoundaryUnits(DungeonData dungeon)
        {
            var result = new List<Unit>();
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
            {
                if (!dungeon.IsFloor(x, y)) continue;
                if (!dungeon.IsFloor(x, y - 1)) result.Add(new Unit(false, y, x));
                if (!dungeon.IsFloor(x, y + 1)) result.Add(new Unit(false, y + 1, x));
                if (!dungeon.IsFloor(x - 1, y)) result.Add(new Unit(true, x, y));
                if (!dungeon.IsFloor(x + 1, y)) result.Add(new Unit(true, x + 1, y));
            }
            return result;
        }

        private static int Count(byte value)
        {
            var count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private static int Hash(int seed, int x, int y, int salt)
        {
            unchecked
            {
                var hash = seed ^ x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                hash ^= hash >> 16;
                return hash & int.MaxValue;
            }
        }
    }
}
