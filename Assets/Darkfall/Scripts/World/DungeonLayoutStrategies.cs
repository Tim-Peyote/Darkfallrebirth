using System;
using System.Collections.Generic;
using Darkfall.Core;

namespace Darkfall.World
{
    internal interface IDungeonLayoutStrategy
    {
        string Id { get; }
        DungeonLayoutPlan Generate(GameBalance balance, int depth, int seed);
    }

    internal sealed class DungeonLayoutPlan
    {
        public readonly int Depth;
        public readonly int Seed;
        public readonly int BiomeStyle;
        public readonly Random Random;
        public readonly bool[,] Floor;
        public readonly List<DungeonRoom> Rooms;
        public string StrategyId;
        public int LoopConnections;
        public int RepairOperations;
        public int ExtraConnectionBudget = -1;

        public DungeonLayoutPlan(int depth, int seed, int biomeStyle, Random random,
            bool[,] floor, List<DungeonRoom> rooms)
        {
            Depth = depth;
            Seed = seed;
            BiomeStyle = biomeStyle;
            Random = random;
            Floor = floor;
            Rooms = rooms;
        }
    }

    internal static class DungeonLayoutStrategies
    {
        private static readonly IDungeonLayoutStrategy[] Chapters =
        {
            new AshenCatacombsStrategy(),
            new RoomCorridorStrategy("ember-vaults", 1),
            new RoomCorridorStrategy("drowned-crypt", 2),
            new RoomCorridorStrategy("charnel-gardens", 3),
            new RoomCorridorStrategy("obsidian-sanctum", 4)
        };

        public static IDungeonLayoutStrategy ForDepth(int depth) =>
            Chapters[Math.Max(0, (depth - 1) / 10) % Chapters.Length];

        private sealed class RoomCorridorStrategy : IDungeonLayoutStrategy
        {
            public string Id { get; }
            private readonly int biomeStyle;

            public RoomCorridorStrategy(string id, int biomeStyle)
            {
                Id = id;
                this.biomeStyle = biomeStyle;
            }

            public DungeonLayoutPlan Generate(GameBalance balance, int depth, int seed)
            {
                var plan = DungeonGenerator.BuildRoomCorridorLayout(balance, depth, seed, biomeStyle);
                plan.StrategyId = Id;
                return plan;
            }
        }

        private sealed class AshenCatacombsStrategy : IDungeonLayoutStrategy
        {
            public string Id => "ashen-catacombs";

            public DungeonLayoutPlan Generate(GameBalance balance, int depth, int seed)
            {
                var plan = DungeonGenerator.BuildAshenCatacombsLayout(balance, depth, seed);
                plan.StrategyId = Id;
                return plan;
            }
        }
    }
}
