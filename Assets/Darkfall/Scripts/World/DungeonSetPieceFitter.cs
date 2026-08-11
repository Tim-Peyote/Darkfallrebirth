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
                room => room.theme == DungeonRoomTheme.Shrine ? 10000 : 0, seed ^ 0x13579);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.TreasureVault, 3,
                room => room.theme == DungeonRoomTheme.Reliquary ? 9000 : 0, seed ^ 0x24680);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.EliteArena, 5,
                room => room.theme == DungeonRoomTheme.Ritual ? 7000 : room.bounds.width * room.bounds.height,
                seed ^ 0x51A7E);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.EventRoom, 5,
                room => room.theme == DungeonRoomTheme.Ossuary ? 5000 : 0, seed ^ 0xE7E17);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.MimicLair, 3,
                room => room.theme == DungeonRoomTheme.Reliquary || room.theme == DungeonRoomTheme.Ossuary ? 4000 : 0,
                seed ^ 0xA11CE);
            FitBest(dungeon, usedRooms, DungeonSetPieceKind.BiomeLandmark, 5,
                room => room.bounds.width * room.bounds.height, seed ^ 0xB10BE);
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
                var score = semanticScore(room) + room.bounds.width * room.bounds.height + StableScore(i, seed) % 97;
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
            var center = dungeon.Rooms[roomIndex].Center;
            var half = maskSize / 2;
            return dungeon.TryReserveSetPiece(kind, roomIndex,
                new RectInt(center.x - half, center.y - half, maskSize, maskSize),
                dungeon.CellCenter(center), allowProtected);
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
