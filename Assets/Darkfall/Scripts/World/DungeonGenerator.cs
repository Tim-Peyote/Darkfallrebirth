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

            EnsureRoomConnectivity(floor, rooms, random);

            var dungeon = new DungeonData(floor, rooms);
            BuildArchitectureGrammar(dungeon, depth, seed);
            return dungeon;
        }

        private static void EnsureRoomConnectivity(bool[,] floor, List<DungeonRoom> rooms, System.Random random)
        {
            for (var room = 1; room < rooms.Count; room++)
            {
                var previous = rooms[room - 1].Center;
                var current = rooms[room].Center;
                if (HasFloorRoute(floor, previous, current)) continue;
                CarveConnection(floor, previous, current, random);
            }
        }

        private static bool HasFloorRoute(bool[,] floor, Vector2Int start, Vector2Int goal)
        {
            if (!floor[start.x, start.y] || !floor[goal.x, goal.y]) return false;
            var visited = new bool[floor.GetLength(0), floor.GetLength(1)];
            var queue = new Queue<Vector2Int>();
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == goal) return true;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= floor.GetLength(0) || next.y >= floor.GetLength(1) ||
                        visited[next.x, next.y] || !floor[next.x, next.y]) continue;
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
            return false;
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
            var dungeon = new DungeonData(floor, new List<DungeonRoom>
            {
                new DungeonRoom { bounds = new RectInt(5, 14, 2, 2) },
                new DungeonRoom { bounds = new RectInt(23, 14, 2, 2) }
            });
            return dungeon;
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
            // Diablo's macro layout is orthogonal. Its richness comes from connected rooms,
            // chambers, halls and later context substitutions, not from shaving every room into
            // a marching-squares polygon. Keeping the base room rectangular gives wall pieces a
            // stable grammar and makes every corner an actual corner.
            for (var x = room.xMin; x < room.xMax; x++)
            for (var y = room.yMin; y < room.yMax; y++)
                floor[x, y] = true;
        }

        private static void CarveConnection(bool[,] floor, Vector2Int from, Vector2Int to, System.Random random)
        {
            // The logical corridor is orthogonal, as in Diablo's DRLG. The isometric projection
            // supplies the diagonal screen direction; a Bresenham corridor only creates a noisy
            // staircase of fake architectural corners.
            var width = random.Next(0, 4) == 0 ? 3 : 2;
            if (random.Next(0, 2) == 0)
            {
                CarveAxisCorridor(floor, from, new Vector2Int(to.x, from.y), width);
                CarveAxisCorridor(floor, new Vector2Int(to.x, from.y), to, width);
            }
            else
            {
                CarveAxisCorridor(floor, from, new Vector2Int(from.x, to.y), width);
                CarveAxisCorridor(floor, new Vector2Int(from.x, to.y), to, width);
            }
            CarveDisc(floor, from.x, from.y, width + 1);
            CarveDisc(floor, to.x, to.y, width + 1);
        }

        private static void CarveAxisCorridor(bool[,] floor, Vector2Int from, Vector2Int to, int width)
        {
            var step = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
            var current = from;
            while (true)
            {
                CarveDisc(floor, current.x, current.y, width);
                if (current == to) break;
                current += step;
            }
        }

        private static void BuildArchitectureGrammar(DungeonData data, int depth, int seed)
        {
            var candidates = new List<ArchitectureThreshold>();
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                FindHorizontalThresholds(data, roomIndex, bounds.xMin, bounds.xMax,
                    bounds.yMin, bounds.yMin - 1, bounds.yMin, false, candidates);
                FindHorizontalThresholds(data, roomIndex, bounds.xMin, bounds.xMax,
                    bounds.yMax - 1, bounds.yMax, bounds.yMax, false, candidates);
                FindVerticalThresholds(data, roomIndex, bounds.yMin, bounds.yMax,
                    bounds.xMin, bounds.xMin - 1, bounds.xMin, true, candidates);
                FindVerticalThresholds(data, roomIndex, bounds.yMin, bounds.yMax,
                    bounds.xMax - 1, bounds.xMax, bounds.xMax, true, candidates);
            }

            var thresholds = new List<ArchitectureThreshold>();
            foreach (var candidate in candidates)
            {
                // The authored threshold kits span a two/three-cell passage. A broad overlap is a
                // merged room, while a one-cell throat is deliberately left visually open.
                if (candidate.Width < 2 || candidate.Width > 3) continue;
                var duplicate = false;
                foreach (var accepted in thresholds)
                    if (Vector2.Distance(accepted.Position, candidate.Position) < 2.25f)
                    {
                        duplicate = true;
                        break;
                    }
                if (!duplicate) thresholds.Add(candidate);
            }

            // Elevation belongs to a room/platform, not to a freestanding stair sprite. Select a
            // few complete rooms, raise their floor, then turn every valid entrance of those rooms
            // into a stair transition. The level exit remains the independent ExitPortal entity.
            var platformBudget = Mathf.Clamp(1 + depth / 15 + data.Rooms.Count / 22, 1, 3);
            var platformRooms = new List<int>();
            var roomCandidates = new List<int>();
            for (var roomIndex = 1; roomIndex < data.Rooms.Count - 1; roomIndex++)
            {
                var validEntrances = 0;
                foreach (var threshold in thresholds)
                    if (threshold.RoomIndex == roomIndex) validEntrances++;
                if (validEntrances > 0) roomCandidates.Add(roomIndex);
            }
            roomCandidates.Sort((a, b) => ArchitectureRoomScore(a, seed).CompareTo(ArchitectureRoomScore(b, seed)));
            foreach (var roomIndex in roomCandidates)
            {
                if (platformRooms.Count >= platformBudget) break;
                var separated = true;
                foreach (var previous in platformRooms)
                    if (Vector2.Distance(data.Rooms[previous].Center, data.Rooms[roomIndex].Center) < 12f)
                    {
                        separated = false;
                        break;
                    }
                if (!separated) continue;
                platformRooms.Add(roomIndex);
                data.SetElevation(data.Rooms[roomIndex].bounds, 1);
            }

            thresholds.Sort((a, b) => ArchitectureScore(a, seed).CompareTo(ArchitectureScore(b, seed)));
            var doorPlaced = false;
            var allowDoor = depth > 1 && ArchitectureFloorScore(seed, depth) % 100 < 16;
            foreach (var threshold in thresholds)
            {
                var kind = platformRooms.Contains(threshold.RoomIndex)
                    ? DungeonArchitectureKind.ElevationStairs
                    : DungeonArchitectureKind.OpenGate;
                var doorLock = DungeonDoorLockKind.None;
                // Doors are punctuation, not wallpaper. At most one ordinary floor threshold is
                // promoted to a stateful door, and shallow first floors stay immediately readable.
                if (allowDoor && !doorPlaced && kind == DungeonArchitectureKind.OpenGate && threshold.Width == 2 &&
                    Vector2.Distance(threshold.Position, data.CellCenter(data.StartCell)) > 5f &&
                    Vector2.Distance(threshold.Position, data.CellCenter(data.ExitCell)) > 5f)
                {
                    kind = DungeonArchitectureKind.ClosedDoor;
                    doorLock = depth >= 4 && ArchitectureScore(threshold, seed ^ 0xA11CE) % 100 < 45
                        ? DungeonDoorLockKind.EnemySeal
                        : depth >= 3 && ArchitectureScore(threshold, seed ^ 0xBADC0DE) % 100 < 35
                            ? DungeonDoorLockKind.Key
                            : DungeonDoorLockKind.None;
                    doorPlaced = true;
                }
                var feature = new DungeonArchitectureFeature(kind, threshold.Position,
                    threshold.Vertical, threshold.FlipX, threshold.Width, doorLock);
                data.AddArchitecture(feature);
                if (kind == DungeonArchitectureKind.ElevationStairs) data.AddStairTraversal(feature);
            }
        }

        private static int ArchitectureRoomScore(int roomIndex, int seed)
        {
            unchecked
            {
                var value = seed ^ roomIndex * 83492791;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }

        private static int ArchitectureFloorScore(int seed, int depth)
        {
            unchecked
            {
                var value = seed ^ depth * 19349663 ^ 0x51F15E;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }

        private static int ArchitectureScore(ArchitectureThreshold threshold, int seed)
        {
            unchecked
            {
                var value = seed ^ Mathf.RoundToInt(threshold.Position.x * 97f) * 73856093 ^
                            Mathf.RoundToInt(threshold.Position.y * 97f) * 19349663 ^ threshold.RoomIndex * 83492791;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }

        private static void FindHorizontalThresholds(DungeonData data, int roomIndex, int minimum, int maximum,
            int insideY, int outsideY, float edgeY, bool flipX, List<ArchitectureThreshold> result)
        {
            var start = -1;
            for (var x = minimum; x <= maximum; x++)
            {
                var crossing = x < maximum && data.IsFloor(x, insideY) && data.IsFloor(x, outsideY);
                if (crossing && start < 0) start = x;
                if (crossing || start < 0) continue;
                result.Add(new ArchitectureThreshold(roomIndex, new Vector2((start + x) * .5f, edgeY),
                    false, flipX, x - start));
                start = -1;
            }
        }

        private static void FindVerticalThresholds(DungeonData data, int roomIndex, int minimum, int maximum,
            int insideX, int outsideX, float edgeX, bool flipX, List<ArchitectureThreshold> result)
        {
            var start = -1;
            for (var y = minimum; y <= maximum; y++)
            {
                var crossing = y < maximum && data.IsFloor(insideX, y) && data.IsFloor(outsideX, y);
                if (crossing && start < 0) start = y;
                if (crossing || start < 0) continue;
                result.Add(new ArchitectureThreshold(roomIndex, new Vector2(edgeX, (start + y) * .5f),
                    true, flipX, y - start));
                start = -1;
            }
        }

        private readonly struct ArchitectureThreshold
        {
            public readonly int RoomIndex;
            public readonly Vector2 Position;
            public readonly bool Vertical;
            public readonly bool FlipX;
            public readonly int Width;

            public ArchitectureThreshold(int roomIndex, Vector2 position, bool vertical, bool flipX, int width)
            {
                RoomIndex = roomIndex;
                Position = position;
                Vertical = vertical;
                FlipX = flipX;
                Width = width;
            }
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
