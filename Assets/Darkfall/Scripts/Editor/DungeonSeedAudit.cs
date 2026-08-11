#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Darkfall.Core;
using Darkfall.Gameplay;
using Darkfall.World;
using UnityEditor;
using UnityEngine;

namespace Darkfall.Editor
{
    /// <summary>
    /// Slow, exhaustive topology audit. Keep it separate from Validate Project so everyday
    /// iteration remains quick while CI and release candidates can exercise thousands of seeds.
    /// </summary>
    public static class DungeonSeedAudit
    {
        private const int DefaultSeedCount = 5000;
        private const int MaximumRecordedFailures = 250;
        private const string ReportPath = "work/validation/dungeon-seed-audit.json";
        private static readonly Vector2Int[] Directions =
            { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };

        [MenuItem("Darkfall/Validation/Audit 5000 Dungeon Seeds")]
        public static void AuditDungeonSeeds()
        {
            var report = Run(DefaultSeedCount, true);
            if (report.totalFailures > 0)
                throw new InvalidOperationException(
                    $"Dungeon seed audit failed: {report.totalFailures} error(s) across {report.failedSeeds} seed(s). " +
                    $"Report: {Path.GetFullPath(ReportPath)}");
        }

        /// <summary>Command-line entry point for -executeMethod.</summary>
        public static void AuditDungeonSeedsBatch()
        {
            var report = Run(DefaultSeedCount, false);
            if (report.totalFailures > 0)
                throw new InvalidOperationException(
                    $"Dungeon seed audit failed: {report.totalFailures} error(s) across {report.failedSeeds} seed(s)");
        }

        public static DungeonSeedAuditReport Run(int seedCount, bool showProgress)
        {
            if (seedCount <= 0) throw new ArgumentOutOfRangeException(nameof(seedCount));
            var stopwatch = Stopwatch.StartNew();
            var report = new DungeonSeedAuditReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                requestedSeeds = seedCount
            };
            var summaries = CreateSummaries();
            var balance = GameBalance.RuntimeDefault();

            try
            {
                for (var sample = 0; sample < seedCount; sample++)
                {
                    if (showProgress && sample % 25 == 0)
                        EditorUtility.DisplayProgressBar("Darkfall dungeon seed audit",
                            $"Seed {sample + 1:N0} / {seedCount:N0}", (float)sample / seedCount);

                    var depth = sample % 50 + 1;
                    var seed = unchecked(sample * 73856093 ^ depth * 19349663 ^ 0x51F15E);
                    var dungeon = DungeonGenerator.Generate(balance, depth, seed);
                    var summary = summaries[Mathf.Clamp((depth - 1) / 10, 0, 4)];
                    summary.samples++;
                    summary.minimumRooms = Mathf.Min(summary.minimumRooms, dungeon.Rooms.Count);
                    summary.maximumRooms = Mathf.Max(summary.maximumRooms, dungeon.Rooms.Count);
                    summary.totalRooms += dungeon.Rooms.Count;
                    summary.minimumSize = Mathf.Min(summary.minimumSize, dungeon.Width);
                    summary.maximumSize = Mathf.Max(summary.maximumSize, dungeon.Width);
                    summary.totalFloorCells += CountFloorCells(dungeon);
                    summary.totalHazardCells += dungeon.Hazards.Count;

                    var failures = ValidateSeed(dungeon, depth, seed, out var mainPathLength,
                        out var deadEnds, out var reachableCells);
                    summary.totalMainPathLength += Mathf.Max(0, mainPathLength);
                    summary.totalDeadEnds += deadEnds;
                    summary.totalReachableCells += reachableCells;
                    report.seeds.Add(new DungeonSeedRecord
                    {
                        sample = sample,
                        seed = seed,
                        depth = depth,
                        biome = DungeonVisualProfile.ForDepth(depth).Id,
                        strategy = dungeon.GenerationInfo?.strategy ?? "unknown",
                        rooms = dungeon.Rooms.Count,
                        width = dungeon.Width,
                        height = dungeon.Height,
                        floorCells = CountFloorCells(dungeon),
                        hazardCells = dungeon.Hazards.Count,
                        mainPathLength = mainPathLength,
                        loopConnections = dungeon.GenerationInfo?.loopConnections ?? 0,
                        deadEnds = deadEnds,
                        repairOperations = dungeon.GenerationInfo?.repairOperations ?? 0
                    });

                    if (failures.Count == 0)
                    {
                        report.passedSeeds++;
                        continue;
                    }

                    report.failedSeeds++;
                    report.totalFailures += failures.Count;
                    foreach (var failure in failures)
                    {
                        if (report.failures.Count >= MaximumRecordedFailures) break;
                        report.failures.Add(new DungeonSeedAuditFailure
                        {
                            sample = sample,
                            seed = seed,
                            depth = depth,
                            biome = DungeonVisualProfile.ForDepth(depth).Id,
                            message = failure
                        });
                    }
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                UnityEngine.Object.DestroyImmediate(balance);
            }

            stopwatch.Stop();
            report.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
            report.biomes.AddRange(summaries);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "work/validation");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));

            var absoluteReport = Path.GetFullPath(ReportPath);
            if (report.totalFailures == 0)
                UnityEngine.Debug.Log(
                    $"Dungeon seed audit passed: {seedCount:N0} seeds in {report.durationSeconds:F1}s. {absoluteReport}");
            else
                UnityEngine.Debug.LogError(
                    $"Dungeon seed audit found {report.totalFailures} error(s) across {report.failedSeeds} seed(s). " +
                    $"First {report.failures.Count} recorded in {absoluteReport}");
            return report;
        }

        private static List<string> ValidateSeed(DungeonData dungeon, int depth, int seed,
            out int mainPathLength, out int deadEnds, out int reachableCells)
        {
            var failures = new List<string>();
            mainPathLength = -1;
            deadEnds = 0;
            reachableCells = 0;
            if (dungeon == null)
            {
                failures.Add("Generator returned null");
                return failures;
            }
            if (dungeon.Rooms == null || dungeon.Rooms.Count < 2)
            {
                failures.Add("Dungeon contains fewer than two rooms");
                return failures;
            }
            if (!dungeon.IsFloor(dungeon.StartCell.x, dungeon.StartCell.y)) failures.Add("Start is not on floor");
            if (!dungeon.IsFloor(dungeon.ExitCell.x, dungeon.ExitCell.y)) failures.Add("Exit is not on floor");

            var distances = BuildDistanceMap(dungeon, dungeon.StartCell, out reachableCells);
            if (IsInside(dungeon, dungeon.ExitCell)) mainPathLength = distances[dungeon.ExitCell.x, dungeon.ExitCell.y];
            if (mainPathLength < 0) failures.Add("Exit is unreachable from start");
            for (var room = 0; room < dungeon.Rooms.Count; room++)
            {
                var center = dungeon.Rooms[room].Center;
                if (!IsInside(dungeon, center) || distances[center.x, center.y] < 0)
                    failures.Add($"Room {room} center is unreachable");
            }

            var floorCells = 0;
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
            {
                if (!dungeon.IsFloor(x, y)) continue;
                floorCells++;
                var neighbours = 0;
                foreach (var direction in Directions)
                    if (dungeon.IsFloor(x + direction.x, y + direction.y)) neighbours++;
                if (neighbours == 1) deadEnds++;
            }
            if (reachableCells != floorCells)
                failures.Add($"Disconnected floor cells: reached {reachableCells} of {floorCells}");

            var start = dungeon.CellCenter(dungeon.StartCell);
            if (!dungeon.CanOccupy(start + Vector2.left * .3f, .22f)) failures.Add("Start blocks left movement");
            if (!dungeon.CanOccupy(start + Vector2.right * .3f, .22f)) failures.Add("Start blocks right movement");
            if (!dungeon.CanOccupy(start + Vector2.up * .3f, .22f)) failures.Add("Start blocks upward movement");
            if (!dungeon.CanOccupy(start + Vector2.down * .3f, .22f)) failures.Add("Start blocks downward movement");
            foreach (var screenDirection in Directions)
            {
                var logicalStep = IsoWorld.UnprojectDirection(screenDirection).normalized * .15f;
                var resolved = DungeonMovement.ResolveStep(dungeon, start, logicalStep, .22f, true);
                var logicalDelta = resolved - start;
                if (logicalDelta.sqrMagnitude <= .01f ||
                    Vector2.Dot(IsoWorld.ProjectDirection(logicalDelta).normalized, screenDirection) <= .98f)
                    failures.Add($"Screen movement {screenDirection} changed direction or remained blocked");
            }
            if (dungeon.ElevationLevel(dungeon.ExitCell.x, dungeon.ExitCell.y) != 0)
                failures.Add("Exit overlaps a raised platform");

            ValidateArchitecture(dungeon, depth, failures);
            ValidateHazards(dungeon, failures);

            // Exercise representative structural-decor placements. Accepted obstacles must not
            // disconnect any semantic room centre from the arrival room.
            foreach (var room in dungeon.Rooms)
            {
                var bounds = room.bounds;
                dungeon.TryAddObstaclePreservingRoutes(new Vector2(bounds.xMin + 1.2f, bounds.yMin + 1.1f));
            }
            var afterDecor = BuildDistanceMap(dungeon, dungeon.StartCell, out _);
            for (var room = 0; room < dungeon.Rooms.Count; room++)
            {
                var center = dungeon.Rooms[room].Center;
                if (IsInside(dungeon, center) && afterDecor[center.x, center.y] < 0)
                    failures.Add($"Decor placement disconnected room {room}");
            }

            // Determinism is sampled rather than doubled for all 5000 cases.
            if (Mathf.Abs(seed % 50) == 0)
            {
                var duplicateBalance = GameBalance.RuntimeDefault();
                try
                {
                    var duplicate = DungeonGenerator.Generate(duplicateBalance, depth, seed);
                    if (!SameLayout(dungeon, duplicate)) failures.Add("Same depth and seed produced a different layout");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(duplicateBalance);
                }
            }
            return failures;
        }

        private static void ValidateArchitecture(DungeonData dungeon, int depth, List<string> failures)
        {
            var startDoors = 0;
            foreach (var feature in dungeon.Architecture)
            {
                var x = Mathf.FloorToInt(feature.Position.x);
                var y = Mathf.FloorToInt(feature.Position.y);
                var validThreshold = feature.Vertical
                    ? dungeon.IsFloor(x - 1, y) && dungeon.IsFloor(x, y)
                    : dungeon.IsFloor(x, y - 1) && dungeon.IsFloor(x, y);
                if (!validThreshold) failures.Add($"{feature.Kind} is not attached to a valid passage");
                var firstElevation = feature.Vertical
                    ? dungeon.ElevationLevel(x - 1, y)
                    : dungeon.ElevationLevel(x, y - 1);
                var secondElevation = dungeon.ElevationLevel(x, y);
                if (feature.Kind == DungeonArchitectureKind.ElevationStairs && firstElevation == secondElevation)
                    failures.Add("Stairs do not connect two elevations");
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs && firstElevation != secondElevation)
                    failures.Add("Gate or door incorrectly bridges an elevation change");
                if (feature.Kind != DungeonArchitectureKind.ClosedDoor) continue;
                var bounds = dungeon.Rooms[0].bounds;
                if (feature.Position.x >= bounds.xMin - .1f && feature.Position.x <= bounds.xMax + .1f &&
                    feature.Position.y >= bounds.yMin - .1f && feature.Position.y <= bounds.yMax + .1f)
                    startDoors++;
            }
            var expectedStartDoors = depth % 10 == 0 ? 0 : 1;
            if (startDoors != expectedStartDoors)
                failures.Add($"Arrival room has {startDoors} safety doors; expected {expectedStartDoors}");
        }

        private static void ValidateHazards(DungeonData dungeon, List<string> failures)
        {
            var byCell = new Dictionary<Vector2Int, DungeonHazardCell>();
            foreach (var hazard in dungeon.Hazards)
            {
                if (byCell.ContainsKey(hazard.Cell)) failures.Add($"Duplicate hazard cell {hazard.Cell}");
                byCell[hazard.Cell] = hazard;
                if (!dungeon.IsFloor(hazard.Cell.x, hazard.Cell.y)) failures.Add("Hazard is outside carved floor");
                if (hazard.Cell == dungeon.StartCell || hazard.Cell == dungeon.ExitCell)
                    failures.Add("Hazard overlaps a protected transition");
            }
            foreach (var hazard in dungeon.Hazards)
            {
                var expected = DungeonHazardConnections.None;
                if (byCell.ContainsKey(hazard.Cell + Vector2Int.left)) expected |= DungeonHazardConnections.West;
                if (byCell.ContainsKey(hazard.Cell + Vector2Int.right)) expected |= DungeonHazardConnections.East;
                if (byCell.ContainsKey(hazard.Cell + Vector2Int.down)) expected |= DungeonHazardConnections.South;
                if (byCell.ContainsKey(hazard.Cell + Vector2Int.up)) expected |= DungeonHazardConnections.North;
                if (hazard.Connections != expected) failures.Add($"Hazard mask mismatch at {hazard.Cell}");
            }

            var remaining = new HashSet<Vector2Int>(byCell.Keys);
            while (remaining.Count > 0)
            {
                var origin = default(Vector2Int);
                foreach (var cell in remaining) { origin = cell; break; }
                var kind = byCell[origin].Kind;
                var sources = 0;
                var sinks = 0;
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(origin);
                remaining.Remove(origin);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    var hazard = byCell[cell];
                    if (hazard.Kind != kind) failures.Add("Connected hazard component mixes materials");
                    if (hazard.Terminal == DungeonHazardTerminal.Source) sources++;
                    if (hazard.Terminal == DungeonHazardTerminal.Sink) sinks++;
                    foreach (var direction in Directions)
                    {
                        var next = cell + direction;
                        if (!remaining.Remove(next)) continue;
                        queue.Enqueue(next);
                    }
                }
                if (sources != 1 || sinks != 1)
                    failures.Add($"Hazard component has {sources} source(s) and {sinks} sink(s)");
            }
        }

        private static int[,] BuildDistanceMap(DungeonData dungeon, Vector2Int start, out int reached)
        {
            var distances = new int[dungeon.Width, dungeon.Height];
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++) distances[x, y] = -1;
            reached = 0;
            if (!IsInside(dungeon, start) || !dungeon.IsFloor(start.x, start.y)) return distances;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            distances[start.x, start.y] = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                reached++;
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (!IsInside(dungeon, next) || distances[next.x, next.y] >= 0 ||
                        !dungeon.IsFloor(next.x, next.y)) continue;
                    distances[next.x, next.y] = distances[cell.x, cell.y] + 1;
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        private static bool SameLayout(DungeonData first, DungeonData second)
        {
            if (first.Width != second.Width || first.Height != second.Height ||
                first.Rooms.Count != second.Rooms.Count || first.Architecture.Count != second.Architecture.Count ||
                first.Hazards.Count != second.Hazards.Count) return false;
            for (var x = 0; x < first.Width; x++)
            for (var y = 0; y < first.Height; y++)
                if (first.IsFloor(x, y) != second.IsFloor(x, y) ||
                    first.ElevationLevel(x, y) != second.ElevationLevel(x, y)) return false;
            return true;
        }

        private static int CountFloorCells(DungeonData dungeon)
        {
            var count = 0;
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
                if (dungeon.IsFloor(x, y)) count++;
            return count;
        }

        private static bool IsInside(DungeonData dungeon, Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < dungeon.Width && cell.y < dungeon.Height;

        private static List<BiomeAuditSummary> CreateSummaries()
        {
            var result = new List<BiomeAuditSummary>();
            foreach (var depth in new[] { 1, 11, 21, 31, 41 })
                result.Add(new BiomeAuditSummary
                {
                    biome = DungeonVisualProfile.ForDepth(depth).Id,
                    depthFrom = depth,
                    depthTo = depth + 9,
                    minimumRooms = int.MaxValue,
                    minimumSize = int.MaxValue
                });
            return result;
        }
    }

    [Serializable]
    public sealed class DungeonSeedAuditReport
    {
        public string generatedAtUtc;
        public string unityVersion;
        public int requestedSeeds;
        public int passedSeeds;
        public int failedSeeds;
        public int totalFailures;
        public float durationSeconds;
        public List<BiomeAuditSummary> biomes = new List<BiomeAuditSummary>();
        public List<DungeonSeedRecord> seeds = new List<DungeonSeedRecord>();
        public List<DungeonSeedAuditFailure> failures = new List<DungeonSeedAuditFailure>();
    }

    [Serializable]
    public sealed class DungeonSeedRecord
    {
        public int sample;
        public int seed;
        public int depth;
        public string biome;
        public string strategy;
        public int rooms;
        public int width;
        public int height;
        public int floorCells;
        public int hazardCells;
        public int mainPathLength;
        public int loopConnections;
        public int deadEnds;
        public int repairOperations;
    }

    [Serializable]
    public sealed class BiomeAuditSummary
    {
        public string biome;
        public int depthFrom;
        public int depthTo;
        public int samples;
        public int minimumRooms;
        public int maximumRooms;
        public long totalRooms;
        public int minimumSize;
        public int maximumSize;
        public long totalFloorCells;
        public long totalHazardCells;
        public long totalMainPathLength;
        public long totalDeadEnds;
        public long totalReachableCells;
    }

    [Serializable]
    public sealed class DungeonSeedAuditFailure
    {
        public int sample;
        public int seed;
        public int depth;
        public string biome;
        public string message;
    }
}
#endif
