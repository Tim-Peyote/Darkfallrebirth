using UnityEngine;

namespace Darkfall.World
{
    public static class DungeonMiniSetMatcher
    {
        public static void MatchAshenCatacombs(DungeonData dungeon, int seed)
        {
            if (dungeon == null) return;
            var placed = 0;
            for (var roomIndex = 1; roomIndex < dungeon.Rooms.Count - 1 && placed < 6; roomIndex++)
            {
                var room = dungeon.Rooms[roomIndex];
                var bounds = room.bounds;
                if (bounds.width < 5 || bounds.height < 5) continue;
                var selector = StableScore(roomIndex, seed) % 8;
                var kind = selector == 0 ? DungeonMiniSetKind.StatueNiche :
                    selector == 1 ? DungeonMiniSetKind.RuinedCorner :
                    selector == 2 ? DungeonMiniSetKind.Colonnade :
                    selector == 3 ? DungeonMiniSetKind.RubbleBlock :
                    selector == 4 ? DungeonMiniSetKind.Campfire :
                    selector == 5 ? DungeonMiniSetKind.Altar :
                    selector == 6 ? DungeonMiniSetKind.SideChapel : DungeonMiniSetKind.CollapsedWall;
                var size = kind == DungeonMiniSetKind.Colonnade || kind == DungeonMiniSetKind.SideChapel ? 5 : 3;
                if (bounds.width < size + 1 || bounds.height < size + 1) continue;
                var anchor = AnchorFor(kind, bounds, selector);
                var cell = Vector2Int.FloorToInt(anchor);
                var half = size / 2;
                var mask = new RectInt(cell.x - half, cell.y - half, size, size);
                if (dungeon.TryReserveMiniSet(kind, roomIndex, mask, dungeon.CellCenter(cell))) placed++;
            }

            foreach (var hazard in dungeon.Hazards)
            {
                if (!hazard.SafeCrossing) continue;
                var cell = hazard.Cell;
                var roomIndex = FindRoom(dungeon, cell);
                if (roomIndex < 0) continue;
                dungeon.TryReserveMiniSet(DungeonMiniSetKind.HazardBridge, roomIndex,
                    new RectInt(cell.x - 1, cell.y - 1, 3, 3), dungeon.CellCenter(cell), true);
            }
        }

        private static Vector2 AnchorFor(DungeonMiniSetKind kind, RectInt bounds, int selector)
        {
            if (kind == DungeonMiniSetKind.StatueNiche)
                return new Vector2(bounds.center.x, bounds.yMax - 1.5f);
            if (kind == DungeonMiniSetKind.RuinedCorner)
                return new Vector2(bounds.xMin + 1.5f, bounds.yMin + 1.5f);
            if (kind == DungeonMiniSetKind.CollapsedWall)
                return new Vector2(bounds.xMax - 1.5f, bounds.center.y);
            if (kind == DungeonMiniSetKind.SideChapel)
                return new Vector2(bounds.center.x, bounds.yMin + 2.5f);
            if (kind == DungeonMiniSetKind.Colonnade)
                return new Vector2(bounds.center.x, bounds.center.y);
            return new Vector2(bounds.center.x + (selector % 2 == 0 ? -.5f : .5f), bounds.center.y);
        }

        private static int FindRoom(DungeonData dungeon, Vector2Int cell)
        {
            for (var i = 0; i < dungeon.Rooms.Count; i++)
                if (dungeon.Rooms[i].bounds.Contains(cell)) return i;
            return -1;
        }

        private static int StableScore(int index, int seed)
        {
            unchecked
            {
                var value = seed ^ index * 73856093;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }
    }
}
