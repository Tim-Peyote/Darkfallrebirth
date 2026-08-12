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
            if (depth % 10 == 0)
            {
                var bossArena = GenerateBossArena();
                bossArena.SetGenerationInfo(new DungeonGenerationInfo
                {
                    depth = depth, seed = seed, strategy = "boss-arena"
                });
                CompleteLogicalPipeline(bossArena);
                return bossArena;
            }

            var strategy = DungeonLayoutStrategies.ForDepth(depth);
            var draft = strategy.Generate(balance, depth, seed);
            var arrivalThreshold = RepairLayout(draft);
            var dungeon = new DungeonData(draft.Floor, draft.Rooms);
            dungeon.CompleteGenerationStage(DungeonGenerationStage.Layout);
            dungeon.CompleteGenerationStage(DungeonGenerationStage.Repair);
            ApplySetPieces(dungeon, draft, arrivalThreshold);
            dungeon.SetGenerationInfo(new DungeonGenerationInfo
            {
                depth = depth,
                seed = seed,
                strategy = draft.StrategyId,
                loopConnections = draft.LoopConnections,
                repairOperations = draft.RepairOperations,
                contextRepairOperations = draft.ContextRepairOperations
            });
            dungeon.CompleteGenerationStage(DungeonGenerationStage.SetPieces);
            return dungeon;
        }

        internal static DungeonLayoutPlan BuildRoomCorridorLayout(GameBalance balance, int depth, int seed,
            int biomeStyle)
        {
            var random = new System.Random(seed);
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
                var safeRoom = rooms.Count == 0;
                var width = safeRoom ? random.Next(5, 7) : random.Next(minimumRoom, maximumRoom + 1);
                var height = safeRoom ? random.Next(5, 7) : random.Next(minimumRoom, maximumRoom + 1);
                // Deeper chapters increasingly introduce halls and transepts instead of merely
                // placing more rooms on a larger map.
                if (!safeRoom && depth >= 6 && attempt % Mathf.Max(3, 8 - depth / 10) == 0)
                {
                    if (random.Next(0, 2) == 0) width = Mathf.Min(size - 6, Mathf.RoundToInt(width * 1.28f));
                    else height = Mathf.Min(size - 6, Mathf.RoundToInt(height * 1.28f));
                }
                if (!safeRoom && biomeStyle == 1)
                {
                    var monumental = Mathf.Max(width, height);
                    width = Mathf.Min(size - 6, monumental);
                    height = Mathf.Min(size - 6, Mathf.Max(height, Mathf.RoundToInt(monumental * .82f)));
                }
                else if (!safeRoom && biomeStyle == 2)
                {
                    if (random.Next(0, 2) == 0) width = Mathf.Min(size - 6, Mathf.RoundToInt(width * 1.32f));
                    else height = Mathf.Min(size - 6, Mathf.RoundToInt(height * 1.32f));
                }
                else if (!safeRoom && biomeStyle == 4)
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

            return new DungeonLayoutPlan(depth, seed, biomeStyle, random, floor, rooms);
        }

        internal static DungeonLayoutPlan BuildAshenCatacombsLayout(GameBalance balance, int depth, int seed)
        {
            var random = new System.Random(seed);
            var size = Mathf.Clamp(balance.mapSize + Mathf.FloorToInt(depth * 1.5f), 30, 72);
            var floor = new bool[size, size];
            var rooms = new List<DungeonRoom>();
            var targetRooms = random.Next(12 + depth / 3, 17 + depth / 2);

            // Catacombs grow from one burial chamber into short neighbouring branches. This is
            // intentionally different from scattering rectangles across the whole map and then
            // joining them with long L corridors.
            var arrivalSize = random.Next(5, 7);
            var arrival = new RectInt(random.Next(4, Mathf.Max(5, size / 3)),
                random.Next(4, Mathf.Max(5, size / 3)), arrivalSize, arrivalSize);
            rooms.Add(new DungeonRoom { bounds = arrival });
            CarveRoom(floor, arrival, random, 0);

            var attempts = targetRooms * 45;
            for (var attempt = 0; attempt < attempts && rooms.Count < targetRooms; attempt++)
            {
                // Most new crypts continue a recent branch; occasional older anchors create a
                // readable fork. The result has reward dead ends without becoming a linear snake.
                var recentWindow = Mathf.Min(5, rooms.Count);
                var anchorIndex = random.Next(100) < 72
                    ? rooms.Count - 1 - random.Next(recentWindow)
                    : random.Next(rooms.Count);
                var anchor = rooms[anchorIndex].bounds;
                var chapel = rooms.Count % 6 == 4;
                var width = chapel ? random.Next(8, 11) : random.Next(5, 8);
                var height = chapel ? random.Next(7, 10) : random.Next(5, 8);
                var direction = random.Next(4);
                var gap = random.Next(2, 5);
                var jitter = random.Next(-2, 3);
                int x;
                int y;
                if (direction == 0)
                {
                    x = anchor.xMax + gap;
                    y = anchor.y + anchor.height / 2 - height / 2 + jitter;
                }
                else if (direction == 1)
                {
                    x = anchor.xMin - width - gap;
                    y = anchor.y + anchor.height / 2 - height / 2 + jitter;
                }
                else if (direction == 2)
                {
                    x = anchor.x + anchor.width / 2 - width / 2 + jitter;
                    y = anchor.yMax + gap;
                }
                else
                {
                    x = anchor.x + anchor.width / 2 - width / 2 + jitter;
                    y = anchor.yMin - height - gap;
                }
                var candidate = new RectInt(x, y, width, height);
                if (candidate.xMin < 2 || candidate.yMin < 2 || candidate.xMax >= size - 2 ||
                    candidate.yMax >= size - 2 || OverlapsAny(candidate, rooms)) continue;

                var room = new DungeonRoom { bounds = candidate };
                CarveRoom(floor, candidate, random, 0);
                CarveConnection(floor, rooms[anchorIndex].Center, room.Center, random);
                rooms.Add(room);
            }

            if (rooms.Count < 8)
                return BuildRoomCorridorLayout(balance, depth, seed, 0);

            var result = new DungeonLayoutPlan(depth, seed, 0, random, floor, rooms)
            {
                // Catacombs favour forks and reward dead ends. Repair may add one safety loop,
                // while the generic grammar adds up to five and erases their identity.
                ExtraConnectionBudget = 1
            };
            return result;
        }

        private static ArchitectureThreshold RepairLayout(DungeonLayoutPlan draft)
        {
            MakeFarthestRoomLast(draft.Rooms);
            var extraConnections = draft.ExtraConnectionBudget >= 0
                ? draft.ExtraConnectionBudget
                : Mathf.Min(5, Mathf.Max(1, draft.Rooms.Count / 4));
            for (var i = 0; i < extraConnections; i++)
            {
                // Room zero is a protected arrival miniset. It keeps its original single route
                // into the dungeon instead of receiving late shortcut corridors.
                var first = draft.Random.Next(1, draft.Rooms.Count);
                var second = draft.Random.Next(1, draft.Rooms.Count);
                if (first == second) continue;
                CarveConnection(draft.Floor, draft.Rooms[first].Center, draft.Rooms[second].Center, draft.Random);
                draft.LoopConnections++;
            }

            draft.RepairOperations += EnsureRoomConnectivity(draft.Floor, draft.Rooms, draft.Random);
            var arrivalThreshold = BuildSafeArrival(
                draft.Floor, draft.Rooms[0].bounds, draft.Rooms[1].Center);
            draft.RepairOperations++;
            draft.RepairOperations += EnsureNonArrivalConnectivity(
                draft.Floor, draft.Rooms, draft.Rooms[0].bounds);
            draft.ContextRepairOperations = DungeonLayoutRepairPass.Apply(draft.Floor, draft.Rooms);
            draft.RepairOperations += draft.ContextRepairOperations;
            return arrivalThreshold;
        }

        private static void ApplySetPieces(DungeonData dungeon, DungeonLayoutPlan draft,
            ArchitectureThreshold arrivalThreshold)
        {
            if (draft.StrategyId == "ashen-catacombs") AssignAshenRoomThemes(draft);
            else AssignRoomThemes(draft.Rooms, draft.BiomeStyle, draft.Seed);
            if (draft.StrategyId == "ashen-catacombs")
                DungeonSetPieceFitter.FitAshenCatacombs(dungeon, draft.Seed);
            ReserveThemedSetPieces(dungeon);
            BuildArchitectureGrammar(dungeon, draft.Depth, draft.Seed, arrivalThreshold);
            BuildHazardGrammar(dungeon, draft.BiomeStyle, draft.Depth, draft.Seed);
            if (draft.StrategyId == "ashen-catacombs")
                DungeonMiniSetMatcher.MatchAshenCatacombs(dungeon, draft.Seed);
        }

        private static void AssignAshenRoomThemes(DungeonLayoutPlan draft)
        {
            var shrineRoom = -1;
            var largestArea = -1;
            for (var i = 0; i < draft.Rooms.Count; i++)
            {
                var room = draft.Rooms[i];
                if (i == 0) room.theme = DungeonRoomTheme.Arrival;
                else if (i == draft.Rooms.Count - 1) room.theme = DungeonRoomTheme.Exit;
                else
                {
                    var entrances = CountRoomEntrances(draft.Floor, room.bounds);
                    var area = room.bounds.width * room.bounds.height;
                    if (area > largestArea) { largestArea = area; shrineRoom = i; }
                    // Start from spatial roles, then place scarce authored scenarios explicitly.
                    // Treating every ordinary chamber as Ossuary/Ritual made themes visually
                    // meaningless and allowed repeated encounters in adjacent rooms.
                    room.theme = entrances <= 1 && area >= 25
                        ? DungeonRoomTheme.Reliquary
                        : DungeonRoomTheme.None;
                }
                draft.Rooms[i] = room;
            }
            // Placement can occasionally reject every large authored chapel. The largest internal
            // chamber still becomes one, keeping the biome readable on every valid seed.
            if (shrineRoom > 0 && shrineRoom < draft.Rooms.Count - 1)
            {
                var hasShrine = false;
                for (var i = 1; i < draft.Rooms.Count - 1; i++)
                    hasShrine |= draft.Rooms[i].theme == DungeonRoomTheme.Shrine;
                if (!hasShrine)
                {
                    var room = draft.Rooms[shrineRoom];
                    room.theme = DungeonRoomTheme.Shrine;
                    draft.Rooms[shrineRoom] = room;
                }
            }
            EnsureAshenRewardTheme(draft.Rooms, draft.Seed ^ 0x7EAD);
            EnsureAshenScenarioTheme(draft.Rooms, DungeonRoomTheme.Ritual, draft.Seed ^ 0x51A7E, 42, 5);
            EnsureAshenScenarioTheme(draft.Rooms, DungeonRoomTheme.Ossuary, draft.Seed ^ 0xE7E17, 36, 5);
        }

        private static void EnsureAshenRewardTheme(List<DungeonRoom> rooms, int seed)
        {
            for (var i = 1; i < rooms.Count - 1; i++)
                if (rooms[i].theme == DungeonRoomTheme.Reliquary) return;

            // Some repaired layouts have no one-entrance room left. Preserve that topology, but
            // still assign the smallest suitable internal chamber as a readable reward branch;
            // never steal the authored chapel or either transition room.
            var best = -1;
            var bestScore = int.MaxValue;
            for (var i = 1; i < rooms.Count - 1; i++)
            {
                var room = rooms[i];
                if (room.theme == DungeonRoomTheme.Shrine) continue;
                var area = room.bounds.width * room.bounds.height;
                var score = area * 100 + ArchitectureRoomScore(i, seed) % 97;
                if (score >= bestScore) continue;
                best = i;
                bestScore = score;
            }
            if (best < 0) return;
            var selected = rooms[best];
            selected.theme = DungeonRoomTheme.Reliquary;
            rooms[best] = selected;
        }

        private static void EnsureAshenScenarioTheme(List<DungeonRoom> rooms, DungeonRoomTheme required,
            int seed, int preferredArea, int minimumSide)
        {
            for (var i = 1; i < rooms.Count - 1; i++)
                if (rooms[i].theme == required) return;

            var best = -1;
            var bestScore = int.MinValue;
            for (var i = 1; i < rooms.Count - 1; i++)
            {
                var room = rooms[i];
                // Preserve authored chapel and reward-dead-end roles. Scenario rooms need a
                // useful combat footprint, so prefer a large multi-entry chamber.
                if (room.theme == DungeonRoomTheme.Shrine || room.theme == DungeonRoomTheme.Reliquary ||
                    room.theme == DungeonRoomTheme.Ritual || room.theme == DungeonRoomTheme.Ossuary)
                    continue;
                var area = room.bounds.width * room.bounds.height;
                // The scenario reservation needs a 3x3 mask plus a one-cell combat apron.
                // Area alone is insufficient: a 4x11 room is large on paper but cannot host it.
                if (room.bounds.width < minimumSide || room.bounds.height < minimumSide) continue;
                var separation = MinimumDistanceToMajorTheme(rooms, i);
                // Scenario rooms need combat space and must not read as one repeated themed block.
                var preferredBonus = area >= preferredArea ? 100000 : 0;
                var score = preferredBonus + area * 100 + Mathf.Min(separation, 24) * 180 +
                            ArchitectureRoomScore(i, seed) % 97;
                if (score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            if (best < 0) return;
            var selected = rooms[best];
            selected.theme = required;
            rooms[best] = selected;
        }

        private static int MinimumDistanceToMajorTheme(List<DungeonRoom> rooms, int candidate)
        {
            var distance = int.MaxValue;
            var center = rooms[candidate].Center;
            for (var i = 0; i < rooms.Count; i++)
            {
                var theme = rooms[i].theme;
                if (theme != DungeonRoomTheme.Shrine && theme != DungeonRoomTheme.Ritual &&
                    theme != DungeonRoomTheme.Ossuary) continue;
                var other = rooms[i].Center;
                distance = Mathf.Min(distance, Mathf.Abs(center.x - other.x) + Mathf.Abs(center.y - other.y));
            }
            return distance == int.MaxValue ? 24 : distance;
        }

        private static int CountRoomEntrances(bool[,] floor, RectInt bounds)
        {
            var entrances = 0;
            entrances += CountThresholdRuns(bounds.xMin, bounds.xMax,
                x => floor[x, bounds.yMin] && floor[x, bounds.yMin - 1]);
            entrances += CountThresholdRuns(bounds.xMin, bounds.xMax,
                x => floor[x, bounds.yMax - 1] && floor[x, bounds.yMax]);
            entrances += CountThresholdRuns(bounds.yMin, bounds.yMax,
                y => floor[bounds.xMin, y] && floor[bounds.xMin - 1, y]);
            entrances += CountThresholdRuns(bounds.yMin, bounds.yMax,
                y => floor[bounds.xMax - 1, y] && floor[bounds.xMax, y]);
            return entrances;
        }

        private static int CountThresholdRuns(int minimum, int maximum, Func<int, bool> crossing)
        {
            var runs = 0;
            var inside = false;
            for (var value = minimum; value < maximum; value++)
            {
                var next = crossing(value);
                if (next && !inside) runs++;
                inside = next;
            }
            return runs;
        }

        private static void ReserveThemedSetPieces(DungeonData dungeon)
        {
            for (var i = 1; i < dungeon.Rooms.Count - 1; i++)
            {
                var room = dungeon.Rooms[i];
                if (room.theme != DungeonRoomTheme.Shrine && room.theme != DungeonRoomTheme.Reliquary &&
                    room.theme != DungeonRoomTheme.Ritual) continue;
                var center = room.Center;
                dungeon.ReserveArea(new RectInt(center.x - 2, center.y - 2, 5, 5),
                    DungeonCellSemantic.EventReserved);
            }
        }

        private static void CompleteLogicalPipeline(DungeonData dungeon)
        {
            // These checkpoints deliberately live on the data product rather than in the view.
            // Later biome strategies can replace layout/repair/set-piece implementations while
            // tile resolution and population keep consuming the same ordered contract.
            dungeon.CompleteGenerationStage(DungeonGenerationStage.Layout);
            dungeon.CompleteGenerationStage(DungeonGenerationStage.Repair);
            dungeon.CompleteGenerationStage(DungeonGenerationStage.SetPieces);
        }

        private static void AssignRoomThemes(List<DungeonRoom> rooms, int biomeStyle, int seed)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (i == 0) room.theme = DungeonRoomTheme.Arrival;
                else if (i == rooms.Count - 1) room.theme = DungeonRoomTheme.Exit;
                else
                {
                    var score = ArchitectureRoomScore(i, seed ^ biomeStyle * 486187739);
                    var common = new[]
                    {
                        DungeonRoomTheme.Shrine, DungeonRoomTheme.Reliquary,
                        DungeonRoomTheme.Ossuary, DungeonRoomTheme.Armory,
                        DungeonRoomTheme.Ritual
                    };
                    room.theme = score % 5 == 0
                        ? biomeStyle == 1 ? DungeonRoomTheme.Forge
                        : biomeStyle == 2 ? DungeonRoomTheme.Cistern
                        : biomeStyle == 3 ? DungeonRoomTheme.Garden
                        : biomeStyle == 4 ? DungeonRoomTheme.Observatory
                        : DungeonRoomTheme.Ossuary
                        : common[(score / 7) % common.Length];
                }
                rooms[i] = room;
            }
        }

        private static void BuildHazardGrammar(DungeonData data, int biomeStyle, int depth, int seed)
        {
            // Hazards are coherent tile fields. A mask derived from four neighbours selects
            // straight, bend, bank, end and island modules, so a river can never become a pile of
            // unrelated decals. Shallow floors keep them rare and the protected arrival/exit
            // rooms are excluded.
            var candidates = new List<int>();
            for (var i = 1; i < data.Rooms.Count - 1; i++)
            {
                var bounds = data.Rooms[i].bounds;
                if (bounds.width >= 8 && bounds.height >= 8 && HazardFitsTheme(data.Rooms[i].theme, biomeStyle))
                    candidates.Add(i);
            }
            // A chapter can legitimately roll no signature room; topology still gets a field,
            // but never by borrowing the protected arrival or exit.
            if (candidates.Count == 0)
                for (var i = 1; i < data.Rooms.Count - 1; i++)
                    if (data.Rooms[i].bounds.width >= 8 && data.Rooms[i].bounds.height >= 8) candidates.Add(i);
            if (candidates.Count == 0) return;
            var budget = Mathf.Clamp(depth / 12 + 1, 1, 3);
            var random = new System.Random(seed ^ 0x6A09E667);
            for (var field = 0; field < budget && candidates.Count > 0; field++)
            {
                var pick = random.Next(candidates.Count);
                var roomIndex = candidates[pick];
                candidates.RemoveAt(pick);
                var bounds = data.Rooms[roomIndex].bounds;
                // A liquid/seep/rift is a directed feature, not a random paint brush. It enters
                // from one room boundary, meanders without teleporting or doubling back, and
                // leaves through the opposite boundary. This guarantees exactly two meaningful
                // terminals and keeps every visual module four-neighbour connected.
                var path = BuildDirectedHazardPath(bounds, random);
                var cells = new HashSet<Vector2Int>(path);
                if (path.Count < 3) continue;
                var overlapsSetPiece = false;
                foreach (var cell in cells)
                    if (data.HasSemantic(cell, DungeonCellSemantic.EventReserved))
                    {
                        overlapsSetPiece = true;
                        break;
                    }
                if (overlapsSetPiece) continue;

                var kind = (DungeonHazardKind)Mathf.Clamp(biomeStyle, 0, 4);
                var damage = kind == DungeonHazardKind.Lava ? 16f :
                    kind == DungeonHazardKind.VoidRift ? 13f : 9f;
                var crossing = path.Count >= 7 ? path[path.Count / 2] : new Vector2Int(-1, -1);
                var flowIndices = new Dictionary<Vector2Int, int>();
                for (var i = 0; i < path.Count; i++)
                    if (!flowIndices.ContainsKey(path[i])) flowIndices[path[i]] = i;
                foreach (var cell in cells)
                {
                    var connections = DungeonHazardConnections.None;
                    if (cells.Contains(cell + Vector2Int.left)) connections |= DungeonHazardConnections.West;
                    if (cells.Contains(cell + Vector2Int.right)) connections |= DungeonHazardConnections.East;
                    if (cells.Contains(cell + Vector2Int.down)) connections |= DungeonHazardConnections.South;
                    if (cells.Contains(cell + Vector2Int.up)) connections |= DungeonHazardConnections.North;
                    var safeCrossing = cell == crossing;
                    var terminal = cell == path[0] ? DungeonHazardTerminal.Source :
                        cell == path[path.Count - 1] ? DungeonHazardTerminal.Sink : DungeonHazardTerminal.None;
                    data.AddHazard(new DungeonHazardCell(cell, kind, connections,
                        safeCrossing ? 0f : damage, safeCrossing, terminal, flowIndices[cell], path.Count));
                }
            }
        }

        private static List<Vector2Int> BuildDirectedHazardPath(RectInt bounds, System.Random random)
        {
            var path = new List<Vector2Int>();
            var horizontal = random.Next(2) == 0;
            var minX = bounds.xMin + 1;
            var maxX = bounds.xMax - 2;
            var minY = bounds.yMin + 1;
            var maxY = bounds.yMax - 2;
            var current = horizontal
                ? new Vector2Int(minX, random.Next(minY + 1, maxY))
                : new Vector2Int(random.Next(minX + 1, maxX), minY);
            path.Add(current);

            var primaryEnd = horizontal ? maxX : maxY;
            while ((horizontal ? current.x : current.y) < primaryEnd)
            {
                // Lateral movement is inserted as its own cardinal step. The old generator moved
                // diagonally in intent and then relied on chance to fill the missing joint.
                if (random.Next(100) < 42)
                {
                    var lateral = random.Next(2) == 0 ? -1 : 1;
                    var candidate = horizontal
                        ? new Vector2Int(current.x, Mathf.Clamp(current.y + lateral, minY, maxY))
                        : new Vector2Int(Mathf.Clamp(current.x + lateral, minX, maxX), current.y);
                    if (candidate != current && !path.Contains(candidate))
                    {
                        current = candidate;
                        path.Add(current);
                    }
                }
                current += horizontal ? Vector2Int.right : Vector2Int.up;
                path.Add(current);
            }
            return path;
        }

        private static bool HazardFitsTheme(DungeonRoomTheme theme, int biomeStyle)
        {
            switch (biomeStyle)
            {
                case 1: return theme == DungeonRoomTheme.Forge || theme == DungeonRoomTheme.Ritual;
                case 2: return theme == DungeonRoomTheme.Cistern || theme == DungeonRoomTheme.Ritual;
                case 3: return theme == DungeonRoomTheme.Garden || theme == DungeonRoomTheme.Ossuary;
                case 4: return theme == DungeonRoomTheme.Observatory || theme == DungeonRoomTheme.Ritual;
                default: return theme == DungeonRoomTheme.Ossuary || theme == DungeonRoomTheme.Ritual;
            }
        }

        private static int EnsureRoomConnectivity(bool[,] floor, List<DungeonRoom> rooms, System.Random random)
        {
            var repairs = 0;
            for (var room = 1; room < rooms.Count; room++)
            {
                var previous = rooms[room - 1].Center;
                var current = rooms[room].Center;
                if (HasFloorRoute(floor, previous, current)) continue;
                CarveConnection(floor, previous, current, random);
                repairs++;
            }
            return repairs;
        }

        private static int EnsureNonArrivalConnectivity(bool[,] floor, List<DungeonRoom> rooms,
            RectInt arrivalRoom)
        {
            var repairs = 0;
            for (var room = 2; room < rooms.Count; room++)
            {
                var previous = rooms[room - 1].Center;
                var current = rooms[room].Center;
                if (!HasFloorRoute(floor, previous, current))
                {
                    CarveConnectionAvoidingArrival(floor, previous, current, arrivalRoom);
                    repairs++;
                }
            }
            return repairs;
        }

        private static void CarveConnectionAvoidingArrival(bool[,] floor, Vector2Int from, Vector2Int to,
            RectInt arrivalRoom)
        {
            var width = floor.GetLength(0);
            var height = floor.GetLength(1);
            var protectedArea = new RectInt(arrivalRoom.xMin - 2, arrivalRoom.yMin - 2,
                arrivalRoom.width + 4, arrivalRoom.height + 4);
            var visited = new bool[width, height];
            var previous = new Vector2Int[width, height];
            var queue = new Queue<Vector2Int>();
            var directions = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            queue.Enqueue(from);
            visited[from.x, from.y] = true;
            while (queue.Count > 0 && !visited[to.x, to.y])
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 2 || next.y < 2 || next.x >= width - 2 || next.y >= height - 2 ||
                        visited[next.x, next.y] || protectedArea.Contains(next)) continue;
                    visited[next.x, next.y] = true;
                    previous[next.x, next.y] = cell;
                    queue.Enqueue(next);
                }
            }
            if (!visited[to.x, to.y]) return;
            var path = to;
            while (path != from)
            {
                CarveDisc(floor, path.x, path.y, 2);
                path = previous[path.x, path.y];
            }
            CarveDisc(floor, from.x, from.y, 2);
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
                // Boss floors are a single open encounter composition. This first logical region
                // places the hero at the opposite end of the arena without building a safety
                // chamber or threshold door around them.
                new DungeonRoom { bounds = new RectInt(5, 13, 4, 4) },
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

        private static ArchitectureThreshold BuildSafeArrival(bool[,] floor, RectInt room, Vector2Int target)
        {
            // Remove every accidental corridor touching the arrival room. A later narrow route is
            // the sole connection, so the architecture pass can always fit one real door.
            for (var y = room.yMin - 1; y <= room.yMax; y++)
            {
                floor[room.xMin - 1, y] = false;
                floor[room.xMax, y] = false;
            }
            for (var x = room.xMin - 1; x <= room.xMax; x++)
            {
                floor[x, room.yMin - 1] = false;
                floor[x, room.yMax] = false;
            }

            var center = new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);
            var delta = target - center;
            Vector2Int outside;
            Vector2Int second;
            Vector2Int outward;
            Vector2 thresholdPosition;
            bool vertical;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                var x = delta.x >= 0 ? room.xMax : room.xMin - 1;
                outside = new Vector2Int(x, center.y);
                second = outside + Vector2Int.up;
                outward = delta.x >= 0 ? Vector2Int.right : Vector2Int.left;
                thresholdPosition = new Vector2(delta.x >= 0 ? room.xMax : room.xMin, center.y + 1f);
                vertical = true;
            }
            else
            {
                var y = delta.y >= 0 ? room.yMax : room.yMin - 1;
                outside = new Vector2Int(center.x, y);
                second = outside + Vector2Int.right;
                outward = delta.y >= 0 ? Vector2Int.up : Vector2Int.down;
                thresholdPosition = new Vector2(center.x + 1f, delta.y >= 0 ? room.yMax : room.yMin);
                vertical = false;
            }
            floor[outside.x, outside.y] = true;
            floor[second.x, second.y] = true;

            // Leave perpendicular to the room before turning toward the dungeon. Turning on the
            // perimeter itself opens every cell between the doorway and the bend.
            var departure = outside + outward * 2;
            CarveTwoWideAxis(floor, outside, departure);
            var bend = new Vector2Int(target.x, departure.y);
            CarveTwoWideAxis(floor, departure, bend);
            CarveTwoWideAxis(floor, bend, target);
            return new ArchitectureThreshold(0, thresholdPosition, vertical, false, 2, false);
        }

        private static void CarveTwoWideAxis(bool[,] floor, Vector2Int from, Vector2Int to)
        {
            var step = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
            var tangent = step.x != 0 ? Vector2Int.up : Vector2Int.right;
            var current = from;
            while (true)
            {
                floor[current.x, current.y] = true;
                var side = current + tangent;
                floor[side.x, side.y] = true;
                if (current == to) break;
                current += step;
            }
        }

        private static void BuildArchitectureGrammar(DungeonData data, int depth, int seed,
            ArchitectureThreshold arrivalThreshold)
        {
            var candidates = new List<ArchitectureThreshold>();
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                FindHorizontalThresholds(data, roomIndex, bounds.xMin, bounds.xMax,
                    bounds.yMin, bounds.yMin - 1, bounds.yMin, false, false, candidates);
                FindHorizontalThresholds(data, roomIndex, bounds.xMin, bounds.xMax,
                    bounds.yMax - 1, bounds.yMax, bounds.yMax, false, true, candidates);
                FindVerticalThresholds(data, roomIndex, bounds.yMin, bounds.yMax,
                    bounds.xMin, bounds.xMin - 1, bounds.xMin, true, false, candidates);
                FindVerticalThresholds(data, roomIndex, bounds.yMin, bounds.yMax,
                    bounds.xMax - 1, bounds.xMax, bounds.xMax, true, true, candidates);
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
            var hasArrival = false;
            foreach (var threshold in thresholds)
                if (threshold.RoomIndex == 0 && Vector2.Distance(threshold.Position, arrivalThreshold.Position) < .2f)
                {
                    hasArrival = true;
                    break;
                }
            if (!hasArrival) thresholds.Add(arrivalThreshold);

            // Elevation is an authored platform inside a room, never an elevation flag applied to
            // the complete room. Keeping a lower floor apron around the platform prevents room
            // doors and corridors from becoming accidental, unmodelled height transitions.
            var platformBudget = Mathf.Clamp(1 + depth / 15 + data.Rooms.Count / 22, 1, 3);
            var platformTransitions = new List<ArchitectureThreshold>();
            var roomCandidates = new List<int>();
            for (var roomIndex = 1; roomIndex < data.Rooms.Count - 1; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                if (bounds.width < 8 || bounds.height < 8) continue;
                roomCandidates.Add(roomIndex);
            }
            roomCandidates.Sort((a, b) => ArchitectureRoomScore(a, seed).CompareTo(ArchitectureRoomScore(b, seed)));
            foreach (var roomIndex in roomCandidates)
            {
                if (platformTransitions.Count >= platformBudget) break;
                var separated = true;
                foreach (var previous in platformTransitions)
                    if (Vector2.Distance(data.Rooms[previous.RoomIndex].Center, data.Rooms[roomIndex].Center) < 12f)
                    {
                        separated = false;
                        break;
                    }
                if (!separated) continue;
                var room = data.Rooms[roomIndex].bounds;
                // Platforms are authored spaces, not one repeated rectangle stamped into every
                // room. Large chambers can afford a wider lower-floor apron; compact rooms retain
                // enough upper/lower floor for a readable landing on both sides of the flight.
                var platformInset = room.width >= 11 && room.height >= 11 &&
                                    ArchitectureRoomScore(roomIndex, seed ^ 0x1A71F0) % 2 == 0 ? 3 : 2;
                var platform = new RectInt(room.xMin + platformInset, room.yMin + platformInset,
                    room.width - platformInset * 2, room.height - platformInset * 2);
                var level = ArchitectureRoomScore(roomIndex, seed ^ 0x5EED71) % 4 == 0
                    ? (sbyte)-1 : (sbyte)1;
                data.SetElevation(platform, level);
                var vertical = ArchitectureRoomScore(roomIndex, seed ^ 0x71A17) % 2 == 0;
                var stairWidth = (vertical ? platform.height : platform.width) >= 5 &&
                                 ArchitectureRoomScore(roomIndex, seed ^ 0x57A175) % 3 == 0 ? 3 : 2;
                var transition = vertical
                    ? new ArchitectureThreshold(roomIndex,
                        new Vector2(platform.xMax, platform.yMin + platform.height / 2f), true, true,
                        stairWidth, true)
                    : new ArchitectureThreshold(roomIndex,
                        new Vector2(platform.xMin + platform.width / 2f, platform.yMax), false, true,
                        stairWidth, true);
                platformTransitions.Add(transition);
                thresholds.Add(transition);
            }

            thresholds.Sort((a, b) => ArchitectureScore(a, seed).CompareTo(ArchitectureScore(b, seed)));
            var doorPlaced = false;
            var allowDoor = depth > 1 && ArchitectureFloorScore(seed, depth) % 100 < 16;
            foreach (var threshold in thresholds)
            {
                if (threshold.RoomIndex == 0 &&
                    Vector2.Distance(threshold.Position, arrivalThreshold.Position) > .2f) continue;
                var kind = IsPlatformTransition(platformTransitions, threshold)
                    ? DungeonArchitectureKind.ElevationStairs : DungeonArchitectureKind.OpenGate;
                var doorLock = DungeonDoorLockKind.None;
                // Arrival room thresholds are always real unlocked doors. Their blockers also
                // occlude enemy perception/projectiles until the player chooses to leave safety.
                if (threshold.RoomIndex == 0) kind = DungeonArchitectureKind.ClosedDoor;
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

        private static bool IsPlatformTransition(IReadOnlyList<ArchitectureThreshold> transitions,
            ArchitectureThreshold candidate)
        {
            foreach (var transition in transitions)
                if (transition.Vertical == candidate.Vertical &&
                    Vector2.Distance(transition.Position, candidate.Position) < .05f) return true;
            return false;
        }

        private static int CountRoomEntrances(DungeonData data, RectInt bounds)
        {
            var entrances = 0;
            entrances += CountThresholdRuns(bounds.xMin, bounds.xMax,
                x => data.IsFloor(x, bounds.yMin) && data.IsFloor(x, bounds.yMin - 1));
            entrances += CountThresholdRuns(bounds.xMin, bounds.xMax,
                x => data.IsFloor(x, bounds.yMax - 1) && data.IsFloor(x, bounds.yMax));
            entrances += CountThresholdRuns(bounds.yMin, bounds.yMax,
                y => data.IsFloor(bounds.xMin, y) && data.IsFloor(bounds.xMin - 1, y));
            entrances += CountThresholdRuns(bounds.yMin, bounds.yMax,
                y => data.IsFloor(bounds.xMax - 1, y) && data.IsFloor(bounds.xMax, y));
            return entrances;
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
            int insideY, int outsideY, float edgeY, bool flipX, bool supportsRaisedPlatform,
            List<ArchitectureThreshold> result)
        {
            var start = -1;
            for (var x = minimum; x <= maximum; x++)
            {
                var crossing = x < maximum && data.IsFloor(x, insideY) && data.IsFloor(x, outsideY);
                if (crossing && start < 0) start = x;
                if (crossing || start < 0) continue;
                result.Add(new ArchitectureThreshold(roomIndex, new Vector2((start + x) * .5f, edgeY),
                    false, flipX, x - start, supportsRaisedPlatform));
                start = -1;
            }
        }

        private static void FindVerticalThresholds(DungeonData data, int roomIndex, int minimum, int maximum,
            int insideX, int outsideX, float edgeX, bool flipX, bool supportsRaisedPlatform,
            List<ArchitectureThreshold> result)
        {
            var start = -1;
            for (var y = minimum; y <= maximum; y++)
            {
                var crossing = y < maximum && data.IsFloor(insideX, y) && data.IsFloor(outsideX, y);
                if (crossing && start < 0) start = y;
                if (crossing || start < 0) continue;
                result.Add(new ArchitectureThreshold(roomIndex, new Vector2(edgeX, (start + y) * .5f),
                    true, flipX, y - start, supportsRaisedPlatform));
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
            public readonly bool SupportsRaisedPlatform;

            public ArchitectureThreshold(int roomIndex, Vector2 position, bool vertical, bool flipX, int width,
                bool supportsRaisedPlatform)
            {
                RoomIndex = roomIndex;
                Position = position;
                Vertical = vertical;
                FlipX = flipX;
                Width = width;
                SupportsRaisedPlatform = supportsRaisedPlatform;
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
