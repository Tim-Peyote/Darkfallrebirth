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
            var biomeStyle = Mathf.Max(0, (depth - 1) / 10) % 5;
            var size = Mathf.Clamp(balance.mapSize + Mathf.FloorToInt(depth * 1.5f), 30, 120);
            var floor = new bool[size, size];
            var rooms = new List<DungeonRoom>();
            var minimumRooms = 10 + Mathf.FloorToInt(depth * .4f);
            var maximumRooms = 15 + Mathf.FloorToInt(depth * .6f);
            var targetRooms = random.Next(minimumRooms, maximumRooms + 1);
            var attempts = targetRooms * 18;
            var roomGrowth = Mathf.Min(12, Mathf.Max(0, depth - 1) / 4);
            var minimumRoom = Mathf.Min(size - 8, balance.minimumRoomSize + roomGrowth / 3);
            var maximumRoom = Mathf.Min(size - 6, balance.maximumRoomSize + roomGrowth);

            for (var attempt = 0; attempt < attempts && rooms.Count < targetRooms; attempt++)
            {
                var width = random.Next(minimumRoom, maximumRoom + 1);
                var height = random.Next(minimumRoom, maximumRoom + 1);
                // Deeper chapters increasingly introduce halls and transepts instead of merely
                // placing more rooms on a larger map.
                if (depth >= 6 && attempt % Mathf.Max(3, 8 - depth / 10) == 0)
                {
                    if (random.Next(0, 2) == 0) width = Mathf.Min(size - 6, Mathf.RoundToInt(width * 1.28f));
                    else height = Mathf.Min(size - 6, Mathf.RoundToInt(height * 1.28f));
                }
                if (biomeStyle == 1)
                {
                    var monumental = Mathf.Max(width, height);
                    width = Mathf.Min(size - 6, monumental);
                    height = Mathf.Min(size - 6, Mathf.Max(height, Mathf.RoundToInt(monumental * .82f)));
                }
                else if (biomeStyle == 2)
                {
                    if (random.Next(0, 2) == 0) width = Mathf.Min(size - 6, Mathf.RoundToInt(width * 1.32f));
                    else height = Mathf.Min(size - 6, Mathf.RoundToInt(height * 1.32f));
                }
                else if (biomeStyle == 4)
                {
                    var sanctum = Mathf.RoundToInt((width + height) * .5f);
                    width = height = Mathf.Min(size - 6, sanctum);
                }
                var candidate = new RectInt(random.Next(2, size - width - 2), random.Next(2, size - height - 2), width, height);
                if (OverlapsAny(candidate, rooms)) continue;

                var room = new DungeonRoom { bounds = candidate };
                CarveRoom(floor, candidate, random, biomeStyle);
                if (rooms.Count > 0) CarveConnection(floor, rooms[rooms.Count - 1].Center, room.Center, random);
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
                CarveRoom(floor, first.bounds, random, biomeStyle);
                CarveRoom(floor, second.bounds, random, biomeStyle);
                CarveConnection(floor, first.Center, second.Center, random);
            }

            MakeFarthestRoomLast(rooms);
            var extraConnections = Mathf.Min(5, Mathf.Max(1, rooms.Count / 4));
            for (var i = 0; i < extraConnections; i++)
            {
                var first = random.Next(0, rooms.Count);
                var second = random.Next(0, rooms.Count);
                if (first == second) continue;
                CarveConnection(floor, rooms[first].Center, rooms[second].Center, random);
            }

            return new DungeonData(floor, rooms);
        }

        private static DungeonData GenerateBossArena()
        {
            const int size = 30;
            var floor = new bool[size, size];
            for (var x = 4; x < 26; x++)
            for (var y = 4; y < 26; y++)
            {
                var left = x - 4;
                var right = 25 - x;
                var bottom = y - 4;
                var top = 25 - y;
                // Boss rooms are monumental octagons rather than the largest rectangle in a run.
                if (left + bottom < 5 || right + bottom < 5 || left + top < 5 || right + top < 5) continue;
                floor[x, y] = true;
            }
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

        private static void CarveRoom(bool[,] floor, RectInt room, System.Random random, int biomeStyle)
        {
            // Cathedral-like chambers are still grid-authored for reliable gameplay, but their
            // silhouette is composed rather than exposed as a rectangle. Chamfers, shallow bays
            // and asymmetric recesses give the renderer useful architectural corners without
            // creating tiny collision traps.
            var chamfer = Mathf.Clamp(Mathf.Min(room.width, room.height) / 4, 1, 3);
            if (biomeStyle == 1) chamfer = Mathf.Min(chamfer, 2);
            var symmetricCut = biomeStyle == 4 ? random.Next(1, chamfer + 1) : -1;
            var cutTopLeft = symmetricCut > 0 ? symmetricCut : random.Next(1, chamfer + 1);
            var cutTopRight = symmetricCut > 0 ? symmetricCut : random.Next(1, chamfer + 1);
            var cutBottomLeft = symmetricCut > 0 ? symmetricCut : random.Next(1, chamfer + 1);
            var cutBottomRight = symmetricCut > 0 ? symmetricCut : random.Next(1, chamfer + 1);
            for (var x = room.xMin; x < room.xMax; x++)
            for (var y = room.yMin; y < room.yMax; y++)
            {
                var left = x - room.xMin;
                var right = room.xMax - 1 - x;
                var bottom = y - room.yMin;
                var top = room.yMax - 1 - y;
                if (left + top < cutTopLeft || right + top < cutTopRight ||
                    left + bottom < cutBottomLeft || right + bottom < cutBottomRight) continue;
                floor[x, y] = true;
            }

            // Shallow recesses break the four-wall rectangle into readable bays and niches.
            if (room.width >= 9 && room.height >= 8)
            {
                var recessWidth = Mathf.Clamp(room.width / 4, 2, 4);
                var recessDepth = random.Next(1, Mathf.Min(3, room.height / 3));
                var recessX = random.Next(room.xMin + 1, room.xMax - recessWidth - 1);
                var fromTop = random.Next(0, 2) == 0;
                var recessY = fromTop ? room.yMax - recessDepth : room.yMin;
                for (var x = recessX; x < recessX + recessWidth; x++)
                for (var y = recessY; y < recessY + recessDepth; y++) floor[x, y] = false;

                if (room.width >= 12 && random.Next(0, 3) != 0)
                {
                    var sideHeight = Mathf.Clamp(room.height / 3, 3, 5);
                    var sideY = random.Next(room.yMin + 1, room.yMax - sideHeight - 1);
                    var fromRight = random.Next(0, 2) == 0;
                    var sideX = fromRight ? room.xMax - 1 : room.xMin;
                    for (var y = sideY; y < sideY + sideHeight; y++) floor[sideX, y] = false;
                }
                if (biomeStyle == 3 && room.height >= 11)
                {
                    var organicWidth = Mathf.Clamp(room.width / 5, 2, 4);
                    var organicX = random.Next(room.xMin + 1, room.xMax - organicWidth - 1);
                    for (var x = organicX; x < organicX + organicWidth; x++) floor[x, room.yMin] = false;
                }
            }
        }

        private static void CarveConnection(bool[,] floor, Vector2Int from, Vector2Int to, System.Random random)
        {
            // A widened Bresenham passage avoids the repeated right-angle elbows of the previous
            // generator. Small landings at both ends make entrances read as authored thresholds.
            var width = random.Next(0, 4) == 0 ? 3 : 2;
            var x = from.x;
            var y = from.y;
            var dx = Mathf.Abs(to.x - from.x);
            var dy = Mathf.Abs(to.y - from.y);
            var sx = from.x < to.x ? 1 : -1;
            var sy = from.y < to.y ? 1 : -1;
            var error = dx - dy;
            while (true)
            {
                CarveDisc(floor, x, y, width);
                if (x == to.x && y == to.y) break;
                var twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
            }
            CarveDisc(floor, from.x, from.y, width + 1);
            CarveDisc(floor, to.x, to.y, width + 1);
        }

        private static void CarveDisc(bool[,] floor, int centerX, int centerY, int width)
        {
            var negative = (width - 1) / 2;
            var positive = width / 2;
            for (var x = centerX - negative; x <= centerX + positive; x++)
            for (var y = centerY - negative; y <= centerY + positive; y++)
            {
                if (x > 0 && y > 0 && x < floor.GetLength(0) - 1 && y < floor.GetLength(1) - 1)
                    floor[x, y] = true;
            }
        }
    }
}
