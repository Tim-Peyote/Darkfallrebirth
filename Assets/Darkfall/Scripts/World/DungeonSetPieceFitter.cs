using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    public static class DungeonSetPieceFitter
    {
        public static void FitAshenCatacombs(DungeonData dungeon, int seed)
        {
            if (dungeon == null || dungeon.Rooms.Count < 2) return;
            Reserve(dungeon, DungeonSetPieceKind.Entrance, 0, 3, true);
            Reserve(dungeon, DungeonSetPieceKind.Portal, dungeon.Rooms.Count - 1, 3, true);

            var usedRooms = new HashSet<int> { 0, dungeon.Rooms.Count - 1 };
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.Shrine, 5,
                room => room.theme == DungeonRoomTheme.Shrine ? 10000 : int.MinValue, seed ^ 0x13579);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.TreasureVault, 3,
                room => room.theme == DungeonRoomTheme.Reliquary ? 9000 : int.MinValue, seed ^ 0x24680);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.EliteArena, 3,
                room => room.theme == DungeonRoomTheme.Ritual ? 7000 : int.MinValue,
                seed ^ 0x51A7E);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.EventRoom, 3,
                room => room.theme == DungeonRoomTheme.Ossuary ? 5000 : int.MinValue, seed ^ 0xE7E17);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.MimicLair, 3,
                room => room.theme == DungeonRoomTheme.Reliquary || room.theme == DungeonRoomTheme.Ossuary ? 4000 : 0,
                seed ^ 0xA11CE);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.BiomeLandmark, 5,
                room => room.bounds.width * room.bounds.height, seed ^ 0xB10BE);
            FitRewardDeadEnds(dungeon, usedRooms, seed ^ 0x7EAD);
        }

        private static void FitRewardDeadEnds(DungeonData dungeon, HashSet<int> usedRooms, int seed)
        {
            // Reliquary is the semantic result of a one-entrance catacomb chamber. Long branches
            // must pay the player back, so additional valid reliquaries receive real treasure
            // reservations instead of decorative bones only. Keep the count bounded to preserve
            // run economy on large deep maps.
            var budget = Mathf.Clamp(1 + dungeon.Rooms.Count / 9, 1, 3);
            var placed = 0;
            var candidateCount = Mathf.Max(1, dungeon.Rooms.Count - 2);
            var start = StableScore(0, seed) % candidateCount;
            for (var offset = 0; offset < candidateCount && placed < budget; offset++)
            {
                var index = 1 + (start + offset) % candidateCount;
                if (usedRooms.Contains(index) || dungeon.Rooms[index].theme != DungeonRoomTheme.Reliquary)
                    continue;
                if (!Reserve(dungeon, DungeonSetPieceKind.TreasureVault, index, 3, false)) continue;
                usedRooms.Add(index);
                placed++;
            }
        }

        private static void FitBest(DungeonData dungeon, HashSet<int> usedRooms, DungeonSetPieceKind kind,
            int maskSize, System.Func<DungeonRoom, int> semanticScore, int seed)
        {
            var bestRoom = -1;
            var bestScore = int.MinValue;
            for (var i = 1; i < dungeon.Rooms.Count - 1; i++)
            {
                if (usedRooms.Contains(i)) continue;
                var room = dungeon.Rooms[i];
                if (room.bounds.width < maskSize + 2 || room.bounds.height < maskSize + 2) continue;
                if (!CanReserveAtRoomCenter(dungeon, i, maskSize)) continue;
                var semantic = semanticScore(room);
                if (semantic == int.MinValue) continue;
                var score = semantic + room.bounds.width * room.bounds.height + StableScore(i, seed) % 97;
                if (score <= bestScore) continue;
                bestRoom = i;
                bestScore = score;
            }
            if (bestRoom < 0 || !Reserve(dungeon, kind, bestRoom, maskSize, false)) return;
            usedRooms.Add(bestRoom);
        }

        private static bool Reserve(DungeonData dungeon, DungeonSetPieceKind kind, int roomIndex,
            int maskSize, bool allowProtected)
        {
            if (!CanReserveAtRoomCenter(dungeon, roomIndex, maskSize)) return false;
            var center = dungeon.Rooms[roomIndex].Center;
            var half = maskSize / 2;
            return dungeon.TryReserveSetPiece(kind, roomIndex,
                new RectInt(center.x - half, center.y - half, maskSize, maskSize),
                dungeon.CellCenter(center), allowProtected);
        }

        private static bool CanReserveAtRoomCenter(DungeonData dungeon, int roomIndex, int maskSize)
        {
            var center = dungeon.Rooms[roomIndex].Center;
            var half = maskSize / 2;
            var mask = new RectInt(center.x - half, center.y - half, maskSize, maskSize);
            foreach (var hazard in dungeon.Hazards)
                if (mask.Contains(hazard.Cell)) return false;
            return true;
        }

        private static int StableScore(int index, int seed)
        {
            unchecked
            {
                var value = seed ^ index * 83492791;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return value & int.MaxValue;
            }
        }
    }
}
