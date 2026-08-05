using System;
using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    public static class DungeonGenerator
    {
        public static DungeonData Generate(GameBalance balance, int depth, int seed)
        {
            if (depth % 10 == 0) return GenerateBossArena();
            var random = new System.Random(seed);
            var size = Mathf.Clamp(balance.mapSize + Mathf.FloorToInt(depth * 1.5f), 30, 120);
            var floor = new bool[size, size];
            var rooms = new List<DungeonRoom>();
            var minimumRooms = 10 + Mathf.FloorToInt(depth * .4f);
            var maximumRooms = 15 + Mathf.FloorToInt(depth * .6f);
            var targetRooms = random.Next(minimumRooms, maximumRooms + 1);
            var attempts = targetRooms * 18;

            for (var attempt = 0; attempt < attempts && rooms.Count < targetRooms; attempt++)
            {
                var width = random.Next(balance.minimumRoomSize, balance.maximumRoomSize + 1);
                var height = random.Next(balance.minimumRoomSize, balance.maximumRoomSize + 1);
                var candidate = new RectInt(random.Next(2, size - width - 2), random.Next(2, size - height - 2), width, height);
                if (OverlapsAny(candidate, rooms)) continue;

                var room = new DungeonRoom { bounds = candidate };
                CarveRoom(floor, candidate);
                if (rooms.Count > 0) CarveConnection(floor, rooms[rooms.Count - 1].Center, room.Center, random.Next(0, 2) == 0);
                rooms.Add(room);
            }

            if (rooms.Count < 2)
            {
                rooms.Clear();
                Array.Clear(floor, 0, floor.Length);
                var first = new DungeonRoom { bounds = new RectInt(3, 3, 8, 8) };
                var second = new DungeonRoom { bounds = new RectInt(size - 12, size - 12, 8, 8) };
                rooms.Add(first);
                rooms.Add(second);
                CarveRoom(floor, first.bounds);
                CarveRoom(floor, second.bounds);
                CarveConnection(floor, first.Center, second.Center, true);
            }

            MakeFarthestRoomLast(rooms);
            var extraConnections = Mathf.Min(5, Mathf.Max(1, rooms.Count / 4));
            for (var i = 0; i < extraConnections; i++)
            {
                var first = random.Next(0, rooms.Count);
                var second = random.Next(0, rooms.Count);
                if (first == second) continue;
                CarveConnection(floor, rooms[first].Center, rooms[second].Center, random.Next(0, 2) == 0);
            }

            return new DungeonData(floor, rooms);
        }

        private static DungeonData GenerateBossArena()
        {
            const int size = 30;
            var floor = new bool[size, size];
            for (var x = 4; x < 26; x++)
            for (var y = 4; y < 26; y++)
                floor[x, y] = true;
            var pillars = new[] { new Vector2Int(10, 10), new Vector2Int(19, 10), new Vector2Int(10, 19), new Vector2Int(19, 19) };
            foreach (var pillar in pillars)
                for (var x = pillar.x; x < pillar.x + 2; x++)
                for (var y = pillar.y; y < pillar.y + 2; y++)
                    floor[x, y] = false;
            return new DungeonData(floor, new List<DungeonRoom>
            {
                new DungeonRoom { bounds = new RectInt(5, 14, 2, 2) },
                new DungeonRoom { bounds = new RectInt(23, 14, 2, 2) }
            });
        }

        private static void MakeFarthestRoomLast(List<DungeonRoom> rooms)
        {
            if (rooms.Count < 2) return;
            var origin = rooms[0].Center;
            var farthest = 1;
            var best = 0;
            for (var i = 1; i < rooms.Count; i++)
            {
                var distance = (rooms[i].Center - origin).sqrMagnitude;
                if (distance <= best) continue;
                best = distance;
                farthest = i;
            }
            (rooms[farthest], rooms[rooms.Count - 1]) = (rooms[rooms.Count - 1], rooms[farthest]);
        }

        private static bool OverlapsAny(RectInt room, List<DungeonRoom> rooms)
        {
            var padded = new RectInt(room.x - 1, room.y - 1, room.width + 2, room.height + 2);
            for (var i = 0; i < rooms.Count; i++)
                if (padded.Overlaps(rooms[i].bounds)) return true;
            return false;
        }

        private static void CarveRoom(bool[,] floor, RectInt room)
        {
            for (var x = room.xMin; x < room.xMax; x++)
            for (var y = room.yMin; y < room.yMax; y++)
                floor[x, y] = true;
        }

        private static void CarveConnection(bool[,] floor, Vector2Int from, Vector2Int to, bool horizontalFirst)
        {
            if (horizontalFirst)
            {
                CarveHorizontal(floor, from.x, to.x, from.y);
                CarveVertical(floor, from.y, to.y, to.x);
            }
            else
            {
                CarveVertical(floor, from.y, to.y, from.x);
                CarveHorizontal(floor, from.x, to.x, to.y);
            }
        }

        private static void CarveHorizontal(bool[,] floor, int from, int to, int y)
        {
            // Two-cell corridors keep the rendered opening and the actor's collision envelope
            // visually aligned. One-cell corridors were technically passable at their centre,
            // but read as wall trim and snagged the player at every L-shaped connection.
            for (var x = Mathf.Min(from, to); x <= Mathf.Max(from, to); x++)
            {
                floor[x, y] = true;
                if (y + 1 < floor.GetLength(1)) floor[x, y + 1] = true;
            }
        }

        private static void CarveVertical(bool[,] floor, int from, int to, int x)
        {
            for (var y = Mathf.Min(from, to); y <= Mathf.Max(from, to); y++)
            {
                floor[x, y] = true;
                if (x + 1 < floor.GetLength(0)) floor[x + 1, y] = true;
            }
        }
    }
}
