using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Conservative 3x3 corrections for corridor contours.</summary>
    public static class DungeonLayoutRepairPass
    {
        private static readonly Vector2Int[] Cardinals =
            { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };

        public static int Apply(bool[,] floor, IReadOnlyList<DungeonRoom> rooms)
        {
            var repairs = 0;
            const int maximumPasses = 3;
            for (var pass = 0; pass < maximumPasses; pass++)
            {
                var additions = new HashSet<Vector2Int>();
                for (var x = 1; x < floor.GetLength(0) - 1; x++)
                for (var y = 1; y < floor.GetLength(1) - 1; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsRoomApron(cell, rooms)) continue;
                    if (!floor[x, y])
                    {
                        // Close a one-cell bite/hole that would stack several inner corners and
                        // leave a black wedge between otherwise continuous floor modules.
                        if (CardinalCount(floor, cell) >= 3 || NeighbourCount(floor, cell) >= 7)
                            additions.Add(cell);
                        continue;
                    }

                }
                if (additions.Count == 0) break;
                foreach (var addition in additions)
                {
                    if (floor[addition.x, addition.y]) continue;
                    floor[addition.x, addition.y] = true;
                    repairs++;
                }
            }
            // Widening is deliberately bounded. Closing a newly exposed notch, however, is a
            // monotonic operation and can safely run to a fixed point without growing along
            // otherwise valid L-shaped corridors.
            for (var pass = 0; pass < 64; pass++)
            {
                var additions = new List<Vector2Int>();
                for (var x = 1; x < floor.GetLength(0) - 1; x++)
                for (var y = 1; y < floor.GetLength(1) - 1; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (floor[x, y] || IsRoomApron(cell, rooms)) continue;
                    if (CardinalCount(floor, cell) >= 3 || NeighbourCount(floor, cell) >= 7)
                        additions.Add(cell);
                }
                if (additions.Count == 0) break;
                foreach (var addition in additions)
                {
                    if (floor[addition.x, addition.y]) continue;
                    floor[addition.x, addition.y] = true;
                    repairs++;
                }
            }
            return repairs;
        }

        public static int CountUnresolvedNotches(bool[,] floor, IReadOnlyList<DungeonRoom> rooms)
        {
            var unresolved = 0;
            for (var x = 1; x < floor.GetLength(0) - 1; x++)
            for (var y = 1; y < floor.GetLength(1) - 1; y++)
            {
                var cell = new Vector2Int(x, y);
                if (floor[x, y] || IsRoomApron(cell, rooms)) continue;
                if (CardinalCount(floor, cell) >= 3 || NeighbourCount(floor, cell) >= 7) unresolved++;
            }
            return unresolved;
        }

        private static int CardinalCount(bool[,] floor, Vector2Int cell)
        {
            var count = 0;
            foreach (var direction in Cardinals) if (At(floor, cell + direction)) count++;
            return count;
        }

        private static int NeighbourCount(bool[,] floor, Vector2Int cell)
        {
            var count = 0;
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && At(floor, cell + new Vector2Int(dx, dy))) count++;
            return count;
        }

        private static bool IsRoomApron(Vector2Int cell, IReadOnlyList<DungeonRoom> rooms)
        {
            foreach (var room in rooms)
            {
                var protectedBounds = new RectInt(room.bounds.xMin - 2, room.bounds.yMin - 2,
                    room.bounds.width + 4, room.bounds.height + 4);
                if (protectedBounds.Contains(cell)) return true;
            }
            return false;
        }

        private static bool At(bool[,] floor, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < floor.GetLength(0) &&
            cell.y < floor.GetLength(1) && floor[cell.x, cell.y];
    }
}
