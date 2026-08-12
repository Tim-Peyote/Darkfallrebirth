using UnityEngine;
using System.Collections.Generic;

namespace Darkfall.World
{
    public static class DungeonMiniSetMatcher
    {
        public static void MatchAshenCatacombs(DungeonData dungeon, int seed)
        {
            if (dungeon == null) return;
            var placed = 0;
            var usedKinds = new HashSet<DungeonMiniSetKind>();
            var roomCount = Mathf.Max(1, dungeon.Rooms.Count - 2);
            var start = StableScore(0, seed ^ 0x4D1A1) % roomCount;
            for (var offset = 0; offset < roomCount && placed < 8; offset++)
            {
                var roomIndex = 1 + (start + offset) % roomCount;
                var room = dungeon.Rooms[roomIndex];
                var bounds = room.bounds;
                if (bounds.width < 5 || bounds.height < 5) continue;
                var selector = StableScore(roomIndex, seed) % 8;
                if (!TrySelectKind(room, selector, usedKinds, out var kind)) continue;
                var size = kind == DungeonMiniSetKind.Colonnade || kind == DungeonMiniSetKind.SideChapel ? 5 : 3;
                if (bounds.width < size + 1 || bounds.height < size + 1) continue;
                var anchor = AnchorFor(kind, bounds, selector);
                var cell = Vector2Int.FloorToInt(anchor);
                var half = size / 2;
                var mask = new RectInt(cell.x - half, cell.y - half, size, size);
                if (!dungeon.TryReserveMiniSet(kind, roomIndex, mask, dungeon.CellCenter(cell))) continue;
                usedKinds.Add(kind);
                placed++;
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

        private static bool TrySelectKind(DungeonRoom room, int selector,
            HashSet<DungeonMiniSetKind> used, out DungeonMiniSetKind result)
        {
            // Room role narrows the visual grammar before deterministic variation is applied.
            // This turns mini-sets into contextual compositions instead of scattered props.
            DungeonMiniSetKind[] pool;
            if (room.theme == DungeonRoomTheme.Shrine)
                pool = new[] { DungeonMiniSetKind.SideChapel, DungeonMiniSetKind.StatueNiche,
                    DungeonMiniSetKind.Altar };
            else if (room.theme == DungeonRoomTheme.Reliquary)
                pool = new[] { DungeonMiniSetKind.RuinedCorner, DungeonMiniSetKind.RubbleBlock,
                    DungeonMiniSetKind.CollapsedWall };
            else if (room.theme == DungeonRoomTheme.Ossuary)
                pool = new[] { DungeonMiniSetKind.Colonnade, DungeonMiniSetKind.StatueNiche,
                    DungeonMiniSetKind.RuinedCorner };
            else if (room.theme == DungeonRoomTheme.Ritual)
                pool = new[] { DungeonMiniSetKind.Campfire, DungeonMiniSetKind.Colonnade,
                    DungeonMiniSetKind.CollapsedWall };
            else
                pool = new[] { DungeonMiniSetKind.RuinedCorner, DungeonMiniSetKind.RubbleBlock,
                    DungeonMiniSetKind.Campfire, DungeonMiniSetKind.CollapsedWall,
                    DungeonMiniSetKind.StatueNiche, DungeonMiniSetKind.Altar,
                    DungeonMiniSetKind.Colonnade, DungeonMiniSetKind.SideChapel };

            for (var step = 0; step < pool.Length; step++)
            {
                var candidate = pool[(selector + step) % pool.Length];
                if (used.Contains(candidate)) continue;
                result = candidate;
                return true;
            }
            result = default;
            return false;
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
