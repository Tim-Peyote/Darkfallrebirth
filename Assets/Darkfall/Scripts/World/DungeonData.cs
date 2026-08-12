using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    public enum DungeonGenerationStage : byte
    {
        Layout,
        Repair,
        SetPieces,
        TileResolution,
        Population,
        Validation
    }

    public enum DungeonSetPieceKind : byte
    {
        Entrance,
        Portal,
        Shrine,
        TreasureVault,
        EliteArena,
        EventRoom,
        MimicLair,
        BiomeLandmark
    }

    public readonly struct DungeonSetPiece
    {
        public readonly DungeonSetPieceKind Kind;
        public readonly int RoomIndex;
        public readonly RectInt Mask;
        public readonly Vector2 Anchor;

        public DungeonSetPiece(DungeonSetPieceKind kind, int roomIndex, RectInt mask, Vector2 anchor)
        {
            Kind = kind;
            RoomIndex = roomIndex;
            Mask = mask;
            Anchor = anchor;
        }
    }

    public enum DungeonMiniSetKind : byte
    {
        StatueNiche,
        RuinedCorner,
        Colonnade,
        RubbleBlock,
        Campfire,
        Altar,
        SideChapel,
        CollapsedWall,
        HazardBridge
    }

    public enum DungeonFloorTileKind : byte
    {
        Center,
        Edge,
        OuterCorner,
        InnerCorner,
        Straight,
        End,
        Isolated
    }

    [Flags]
    public enum DungeonFloorNeighbours : byte
    {
        None = 0,
        West = 1,
        East = 2,
        South = 4,
        North = 8,
        SouthWest = 16,
        SouthEast = 32,
        NorthWest = 64,
        NorthEast = 128
    }

    public readonly struct DungeonResolvedFloorTile
    {
        public readonly Vector2Int Cell;
        public readonly DungeonFloorTileKind Kind;
        public readonly DungeonFloorNeighbours Neighbours;
        public readonly byte Variant;
        public readonly bool Damaged;

        public DungeonResolvedFloorTile(Vector2Int cell, DungeonFloorTileKind kind,
            DungeonFloorNeighbours neighbours, byte variant, bool damaged)
        {
            Cell = cell;
            Kind = kind;
            Neighbours = neighbours;
            Variant = variant;
            Damaged = damaged;
        }
    }

    public enum DungeonWallModuleKind : byte
    {
        Face,
        Broken,
        Niche,
        Arcade
    }

    public enum DungeonWallCornerKind : byte
    {
        Inner,
        Outer
    }

    public readonly struct DungeonResolvedWallModule
    {
        public readonly Vector2 Anchor;
        public readonly bool Vertical;
        public readonly DungeonWallModuleKind Kind;
        public readonly byte Variant;

        public DungeonResolvedWallModule(Vector2 anchor, bool vertical, DungeonWallModuleKind kind, byte variant)
        {
            Anchor = anchor;
            Vertical = vertical;
            Kind = kind;
            Variant = variant;
        }
    }

    public readonly struct DungeonResolvedWallCorner
    {
        public readonly Vector2 Anchor;
        public readonly DungeonWallCornerKind Kind;
        public readonly byte FloorQuadrants;

        public DungeonResolvedWallCorner(Vector2 anchor, DungeonWallCornerKind kind, byte floorQuadrants)
        {
            Anchor = anchor;
            Kind = kind;
            FloorQuadrants = floorQuadrants;
        }
    }

    public readonly struct DungeonMiniSet
    {
        public readonly DungeonMiniSetKind Kind;
        public readonly int RoomIndex;
        public readonly RectInt Mask;
        public readonly Vector2 Anchor;

        public DungeonMiniSet(DungeonMiniSetKind kind, int roomIndex, RectInt mask, Vector2 anchor)
        {
            Kind = kind;
            RoomIndex = roomIndex;
            Mask = mask;
            Anchor = anchor;
        }
    }

    [Serializable]
    public sealed class DungeonGenerationInfo
    {
        public int depth;
        public int seed;
        public string strategy;
        public int loopConnections;
        public int repairOperations;
        public int contextRepairOperations;
    }

    [Flags]
    public enum DungeonCellSemantic : ushort
    {
        None = 0,
        Floor = 1 << 0,
        Room = 1 << 1,
        Corridor = 1 << 2,
        Arrival = 1 << 3,
        Exit = 1 << 4,
        Door = 1 << 5,
        Stair = 1 << 6,
        Portal = 1 << 7,
        Hazard = 1 << 8,
        Light = 1 << 9,
        NoDecor = 1 << 10,
        EventReserved = 1 << 11
    }

    public enum DungeonRoomTheme
    {
        None,
        Arrival,
        Exit,
        Shrine,
        Reliquary,
        Ossuary,
        Armory,
        Ritual,
        Cistern,
        Forge,
        Garden,
        Observatory
    }

    [Serializable]
    public struct DungeonRoom
    {
        public RectInt bounds;
        public DungeonRoomTheme theme;
        public Vector2Int Center => new Vector2Int(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
    }

    public enum DungeonHazardKind
    {
        EmberSeep,
        Lava,
        Brine,
        Bile,
        VoidRift
    }

    public enum DungeonHazardTerminal : byte
    {
        None,
        Source,
        Sink
    }

    [Flags]
    public enum DungeonHazardConnections : byte
    {
        None = 0,
        West = 1,
        East = 2,
        South = 4,
        North = 8
    }

    /// <summary>
    /// One logical hazard tile. Connections select the authored centre/edge/corner/end sprite;
    /// gameplay damage is deliberately independent from that visual selection.
    /// </summary>
    public readonly struct DungeonHazardCell
    {
        public readonly Vector2Int Cell;
        public readonly DungeonHazardKind Kind;
        public readonly DungeonHazardConnections Connections;
        public readonly float DamagePerSecond;
        public readonly bool SafeCrossing;
        public readonly DungeonHazardTerminal Terminal;
        public readonly int FlowIndex;
        public readonly int FlowLength;

        public DungeonHazardCell(Vector2Int cell, DungeonHazardKind kind,
            DungeonHazardConnections connections, float damagePerSecond, bool safeCrossing = false,
            DungeonHazardTerminal terminal = DungeonHazardTerminal.None, int flowIndex = 0, int flowLength = 1)
        {
            Cell = cell;
            Kind = kind;
            Connections = connections;
            DamagePerSecond = damagePerSecond;
            SafeCrossing = safeCrossing;
            Terminal = terminal;
            FlowIndex = flowIndex;
            FlowLength = Mathf.Max(1, flowLength);
        }
    }

    public readonly struct DungeonLightSource
    {
        public readonly Vector2 Position;
        public readonly Color Color;
        public readonly float Radius;
        public readonly float Flicker;

        public DungeonLightSource(Vector2 position, Color color, float radius, float flicker)
        {
            Position = position;
            Color = color;
            Radius = radius;
            Flicker = flicker;
        }
    }

    public enum DungeonArchitectureKind
    {
        OpenGate,
        ClosedDoor,
        ElevationStairs
    }

    public enum DungeonDoorLockKind
    {
        None,
        Key,
        EnemySeal
    }

    /// <summary>
    /// A semantic architecture placement emitted by the generator. The view is deliberately not
    /// allowed to invent doors, stairs or arches from random wall accents: these features belong
    /// to the dungeon grammar and therefore have a valid topological location.
    /// </summary>
    public readonly struct DungeonArchitectureFeature
    {
        public readonly DungeonArchitectureKind Kind;
        public readonly Vector2 Position;
        public readonly bool Vertical;
        public readonly bool FlipX;
        public readonly int Width;
        public readonly DungeonDoorLockKind DoorLock;

        public DungeonArchitectureFeature(DungeonArchitectureKind kind, Vector2 position, bool vertical, bool flipX,
            int width = 2, DungeonDoorLockKind doorLock = DungeonDoorLockKind.None)
        {
            Kind = kind;
            Position = position;
            Vertical = vertical;
            FlipX = flipX;
            Width = width;
            DoorLock = doorLock;
        }
    }

    public sealed class DungeonData
    {
        // Matches the authored stair's upper landing (with the runtime 1.03 module scale). The
        // former .56 value aligned the raised floor with the middle steps instead of the landing.
        public const float ElevationStepHeight = .9f;
        private readonly bool[,] floor;
        private readonly bool[,] explored;
        private readonly bool[,] visible;
        private readonly bool[,] obstacles;
        private readonly sbyte[,] elevation;
        private readonly DungeonCellSemantic[,] semantics;
        private readonly Dictionary<int, Rect> dynamicObstacles = new Dictionary<int, Rect>();
        private readonly List<Rect> architectureObstacles = new List<Rect>();
        private int nextDynamicObstacleId = 1;
        private readonly List<DungeonLightSource> lightSources = new List<DungeonLightSource>();
        private readonly List<DungeonArchitectureFeature> architecture = new List<DungeonArchitectureFeature>();
        private readonly List<DungeonHazardCell> hazards = new List<DungeonHazardCell>();
        private readonly List<DungeonSetPiece> setPieces = new List<DungeonSetPiece>();
        private readonly List<DungeonMiniSet> miniSets = new List<DungeonMiniSet>();
        private readonly List<DungeonResolvedFloorTile> resolvedFloorTiles = new List<DungeonResolvedFloorTile>();
        private readonly Dictionary<int, DungeonResolvedFloorTile> resolvedFloorTileLookup =
            new Dictionary<int, DungeonResolvedFloorTile>();
        private readonly List<DungeonResolvedWallModule> resolvedWallModules = new List<DungeonResolvedWallModule>();
        private readonly List<DungeonResolvedWallCorner> resolvedWallCorners = new List<DungeonResolvedWallCorner>();
        private readonly bool[] completedStages = new bool[Enum.GetValues(typeof(DungeonGenerationStage)).Length];
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<DungeonRoom> Rooms { get; }
        public IReadOnlyList<DungeonLightSource> LightSources => lightSources;
        public IReadOnlyList<DungeonArchitectureFeature> Architecture => architecture;
        public IReadOnlyList<DungeonHazardCell> Hazards => hazards;
        public IReadOnlyList<DungeonSetPiece> SetPieces => setPieces;
        public IReadOnlyList<DungeonMiniSet> MiniSets => miniSets;
        public IReadOnlyList<DungeonResolvedFloorTile> ResolvedFloorTiles => resolvedFloorTiles;
        public IReadOnlyList<DungeonResolvedWallModule> ResolvedWallModules => resolvedWallModules;
        public IReadOnlyList<DungeonResolvedWallCorner> ResolvedWallCorners => resolvedWallCorners;
        public Vector2Int StartCell => Rooms[0].Center;
        public Vector2Int ExitCell => Rooms[Rooms.Count - 1].Center;
        public DungeonGenerationInfo GenerationInfo { get; private set; }
        public DungeonGenerationStage NextGenerationStage
        {
            get
            {
                for (var i = 0; i < completedStages.Length; i++)
                    if (!completedStages[i]) return (DungeonGenerationStage)i;
                return DungeonGenerationStage.Validation;
            }
        }

        public DungeonData(bool[,] floor, List<DungeonRoom> rooms)
        {
            this.floor = floor;
            Width = floor.GetLength(0);
            Height = floor.GetLength(1);
            explored = new bool[Width, Height];
            visible = new bool[Width, Height];
            obstacles = new bool[Width, Height];
            elevation = new sbyte[Width, Height];
            semantics = new DungeonCellSemantic[Width, Height];
            Rooms = rooms;
            InitializeSemantics(rooms);
        }

        private void InitializeSemantics(IReadOnlyList<DungeonRoom> rooms)
        {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                if (floor[x, y]) semantics[x, y] = DungeonCellSemantic.Floor | DungeonCellSemantic.Corridor;

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                var bounds = rooms[roomIndex].bounds;
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                for (var y = bounds.yMin; y < bounds.yMax; y++)
                {
                    if (!IsFloor(x, y)) continue;
                    semantics[x, y] &= ~DungeonCellSemantic.Corridor;
                    semantics[x, y] |= DungeonCellSemantic.Room;
                    if (roomIndex == 0)
                        semantics[x, y] |= DungeonCellSemantic.Arrival | DungeonCellSemantic.NoDecor;
                    else if (roomIndex == rooms.Count - 1)
                        semantics[x, y] |= DungeonCellSemantic.Exit | DungeonCellSemantic.NoDecor;
                }
            }
            MarkSemantic(StartCell, DungeonCellSemantic.Arrival | DungeonCellSemantic.NoDecor);
            MarkSemantic(ExitCell, DungeonCellSemantic.Exit | DungeonCellSemantic.Portal | DungeonCellSemantic.NoDecor);
        }

        public bool IsFloor(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && floor[x, y];
        }

        public DungeonCellSemantic SemanticsAt(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height ? semantics[x, y] : DungeonCellSemantic.None;

        public DungeonCellSemantic SemanticsAt(Vector2Int cell) => SemanticsAt(cell.x, cell.y);

        public bool HasSemantic(int x, int y, DungeonCellSemantic value) =>
            (SemanticsAt(x, y) & value) == value;

        public bool HasSemantic(Vector2Int cell, DungeonCellSemantic value) =>
            HasSemantic(cell.x, cell.y, value);

        public bool HasCompletedStage(DungeonGenerationStage stage) => completedStages[(int)stage];

        internal void CompleteGenerationStage(DungeonGenerationStage stage)
        {
            var index = (int)stage;
            if (completedStages[index]) return;
            for (var previous = 0; previous < index; previous++)
                if (!completedStages[previous])
                    throw new InvalidOperationException(
                        $"Dungeon generation stage {stage} cannot run before {(DungeonGenerationStage)previous}.");
            completedStages[index] = true;
        }

        internal void SetGenerationInfo(DungeonGenerationInfo info) => GenerationInfo = info;

        internal void SetResolvedFloorTiles(IEnumerable<DungeonResolvedFloorTile> tiles)
        {
            resolvedFloorTiles.Clear();
            resolvedFloorTileLookup.Clear();
            foreach (var tile in tiles)
            {
                resolvedFloorTiles.Add(tile);
                resolvedFloorTileLookup[tile.Cell.x + tile.Cell.y * Width] = tile;
            }
        }

        public bool TryGetResolvedFloorTile(int x, int y, out DungeonResolvedFloorTile tile) =>
            resolvedFloorTileLookup.TryGetValue(x + y * Width, out tile);

        internal void SetResolvedWalls(IEnumerable<DungeonResolvedWallModule> modules,
            IEnumerable<DungeonResolvedWallCorner> corners)
        {
            resolvedWallModules.Clear();
            resolvedWallModules.AddRange(modules);
            resolvedWallCorners.Clear();
            resolvedWallCorners.AddRange(corners);
        }

        internal void MarkSemantic(Vector2Int cell, DungeonCellSemantic value)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height) return;
            semantics[cell.x, cell.y] |= value;
        }

        internal void ReserveArea(RectInt area, DungeonCellSemantic reservation)
        {
            for (var x = Mathf.Max(0, area.xMin); x < Mathf.Min(Width, area.xMax); x++)
            for (var y = Mathf.Max(0, area.yMin); y < Mathf.Min(Height, area.yMax); y++)
                if (IsFloor(x, y)) semantics[x, y] |= reservation;
        }

        internal bool TryReserveSetPiece(DungeonSetPieceKind kind, int roomIndex, RectInt mask,
            Vector2 anchor, bool allowProtected = false)
        {
            if (roomIndex < 0 || roomIndex >= Rooms.Count || mask.width <= 0 || mask.height <= 0) return false;
            var room = Rooms[roomIndex].bounds;
            if (mask.xMin < room.xMin || mask.yMin < room.yMin || mask.xMax > room.xMax || mask.yMax > room.yMax)
                return false;
            foreach (var existing in setPieces)
                if (existing.Mask.Overlaps(mask)) return false;
            for (var x = mask.xMin; x < mask.xMax; x++)
            for (var y = mask.yMin; y < mask.yMax; y++)
            {
                if (!IsFloor(x, y)) return false;
                if (!allowProtected && (SemanticsAt(x, y) &
                    (DungeonCellSemantic.NoDecor | DungeonCellSemantic.Hazard |
                     DungeonCellSemantic.Door | DungeonCellSemantic.Stair | DungeonCellSemantic.Portal)) != 0)
                    return false;
            }
            setPieces.Add(new DungeonSetPiece(kind, roomIndex, mask, anchor));
            ReserveArea(mask, DungeonCellSemantic.EventReserved | DungeonCellSemantic.NoDecor);
            return true;
        }

        public bool TryGetSetPiece(DungeonSetPieceKind kind, out DungeonSetPiece result)
        {
            foreach (var setPiece in setPieces)
                if (setPiece.Kind == kind) { result = setPiece; return true; }
            result = default;
            return false;
        }

        internal bool TryReserveMiniSet(DungeonMiniSetKind kind, int roomIndex, RectInt mask,
            Vector2 anchor, bool allowHazard = false)
        {
            if (roomIndex < 0 || roomIndex >= Rooms.Count || mask.width <= 0 || mask.height <= 0) return false;
            var room = Rooms[roomIndex].bounds;
            if (mask.xMin < room.xMin || mask.yMin < room.yMin || mask.xMax > room.xMax || mask.yMax > room.yMax)
                return false;
            for (var x = mask.xMin; x < mask.xMax; x++)
            for (var y = mask.yMin; y < mask.yMax; y++)
            {
                if (!IsFloor(x, y)) return false;
                var blocked = DungeonCellSemantic.EventReserved | DungeonCellSemantic.Door |
                              DungeonCellSemantic.Stair | DungeonCellSemantic.Portal;
                if (!allowHazard) blocked |= DungeonCellSemantic.Hazard;
                if ((SemanticsAt(x, y) & blocked) != 0) return false;
            }
            foreach (var setPiece in setPieces)
                if (setPiece.Mask.Overlaps(mask)) return false;
            foreach (var miniSet in miniSets)
                if (miniSet.Mask.Overlaps(mask)) return false;
            miniSets.Add(new DungeonMiniSet(kind, roomIndex, mask, anchor));
            ReserveArea(mask, DungeonCellSemantic.EventReserved | DungeonCellSemantic.NoDecor);
            return true;
        }

        public bool IsExplored(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && explored[x, y];

        public bool IsVisible(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && visible[x, y];

        public bool BlocksVision(int x, int y) => BlocksVision(new Vector2(x + .5f, y + .5f));

        public bool BlocksVision(Vector2 point)
        {
            var x = Mathf.FloorToInt(point.x);
            var y = Mathf.FloorToInt(point.y);
            if (!IsFloor(x, y)) return true;
            // Gameplay props use the cell obstacle grid for collision, but a sarcophagus, statue
            // or altar must not turn the whole Nox visibility polygon into a black wall. Only
            // authored architecture and currently closed dynamic doors occlude sight.
            foreach (var obstacle in dynamicObstacles.Values)
                if (obstacle.Contains(point)) return true;
            return false;
        }

        public bool TryAddObstaclePreservingRoutes(Vector2 position)
        {
            var x = Mathf.FloorToInt(position.x);
            var y = Mathf.FloorToInt(position.y);
            if (!IsFloor(x, y) || obstacles[x, y]) return false;

            // A blocking prop belongs in open room volume, never in a one/two-cell throat.
            var openNeighbours = 0;
            if (IsWalkable(x - 1, y)) openNeighbours++;
            if (IsWalkable(x + 1, y)) openNeighbours++;
            if (IsWalkable(x, y - 1)) openNeighbours++;
            if (IsWalkable(x, y + 1)) openNeighbours++;
            if (openNeighbours < 3) return false;

            obstacles[x, y] = true;
            if (AllRoomCentersReachable()) return true;
            obstacles[x, y] = false;
            return false;
        }

        private bool AllRoomCentersReachable()
        {
            var start = StartCell;
            if (!IsWalkable(start.x, start.y)) return false;
            var reached = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            reached[start.x, start.y] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= Width || next.y >= Height ||
                        reached[next.x, next.y] || !IsWalkable(next.x, next.y)) continue;
                    reached[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
            foreach (var room in Rooms)
                if (!reached[room.Center.x, room.Center.y]) return false;
            return true;
        }

        public void AddLightSource(Vector2 position, Color color, float radius, float flicker = .1f)
        {
            lightSources.Add(new DungeonLightSource(position, color, radius, flicker));
            MarkSemantic(Vector2Int.FloorToInt(position), DungeonCellSemantic.Light);
        }

        internal void AddArchitecture(DungeonArchitectureFeature feature)
        {
            architecture.Add(feature);
            var semantic = feature.Kind == DungeonArchitectureKind.ElevationStairs
                ? DungeonCellSemantic.Stair | DungeonCellSemantic.NoDecor
                : DungeonCellSemantic.Door | DungeonCellSemantic.NoDecor;
            MarkSemantic(Vector2Int.FloorToInt(feature.Position), semantic);
        }

        internal void AddHazard(DungeonHazardCell hazard)
        {
            hazards.Add(hazard);
            MarkSemantic(hazard.Cell, DungeonCellSemantic.Hazard | DungeonCellSemantic.NoDecor);
        }

        public float HazardDamageAt(Vector2 point)
        {
            return TryGetHazardAt(point, out var hazard) ? hazard.DamagePerSecond : 0f;
        }

        public bool TryGetHazardAt(Vector2 point, out DungeonHazardCell hazard)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            for (var i = 0; i < hazards.Count; i++)
                if (hazards[i].Cell == cell)
                {
                    hazard = hazards[i];
                    return true;
                }
            hazard = default;
            return false;
        }

        public bool IsHazardCell(int x, int y)
        {
            for (var i = 0; i < hazards.Count; i++)
                if (hazards[i].Cell.x == x && hazards[i].Cell.y == y) return true;
            return false;
        }

        internal void AddArchitectureObstacle(Rect area) => architectureObstacles.Add(area);

        internal void AddStairTraversal(DungeonArchitectureFeature feature)
        {
            // Only the central ramp is walkable; the authored stone cheeks remain solid.
            const float laneWidth = .82f;
            // The cheeks run along the complete 1.44-cell flight, not just across the lip. The
            // old .52 obstacle ended halfway down the artwork and let actors walk through the
            // stone sides, especially when entering a below-ground stair from its upper landing.
            const float crossingDepth = 1.44f;
            var totalWidth = Mathf.Max(1.5f, feature.Width);
            var sideWidth = Mathf.Max(.18f, (totalWidth - laneWidth) * .5f);
            if (feature.Vertical)
            {
                AddArchitectureObstacle(new Rect(feature.Position.x - crossingDepth * .5f,
                    feature.Position.y - totalWidth * .5f, crossingDepth, sideWidth));
                AddArchitectureObstacle(new Rect(feature.Position.x - crossingDepth * .5f,
                    feature.Position.y + totalWidth * .5f - sideWidth, crossingDepth, sideWidth));
            }
            else
            {
                AddArchitectureObstacle(new Rect(feature.Position.x - totalWidth * .5f,
                    feature.Position.y - crossingDepth * .5f, sideWidth, crossingDepth));
                AddArchitectureObstacle(new Rect(feature.Position.x + totalWidth * .5f - sideWidth,
                    feature.Position.y - crossingDepth * .5f, sideWidth, crossingDepth));
            }
        }

        public int AddDynamicObstacle(Rect area)
        {
            var id = nextDynamicObstacleId++;
            dynamicObstacles[id] = area;
            return id;
        }

        public void RemoveDynamicObstacle(int id) => dynamicObstacles.Remove(id);

        internal void SetElevation(RectInt area, sbyte level)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            for (var y = area.yMin; y < area.yMax; y++)
                if (IsFloor(x, y)) elevation[x, y] = level;
        }

        public int ElevationLevel(int x, int y) => IsFloor(x, y) ? elevation[x, y] : 0;

        public float SurfaceHeight(Vector2 point)
        {
            foreach (var feature in architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs) continue;
                var normalCoordinate = feature.Vertical ? point.x : point.y;
                var thresholdCoordinate = feature.Vertical ? feature.Position.x : feature.Position.y;
                var tangentDistance = feature.Vertical
                    ? Mathf.Abs(point.y - feature.Position.y)
                    : Mathf.Abs(point.x - feature.Position.x);
                const float rampHalfDepth = .72f;
                if (tangentDistance > .43f || Mathf.Abs(normalCoordinate - thresholdCoordinate) > rampHalfDepth)
                    continue;

                // Sample both landings on the stair centreline. Sampling the actor's tangent cell
                // made the detected pair of levels change near a stair edge, so the sprite and
                // camera snapped vertically while the actor was still on the same flight.
                var tangentCell = Mathf.FloorToInt(feature.Vertical ? feature.Position.y : feature.Position.x);
                var negative = feature.Vertical
                    ? ElevationLevel(Mathf.FloorToInt(feature.Position.x - .25f), tangentCell)
                    : ElevationLevel(tangentCell, Mathf.FloorToInt(feature.Position.y - .25f));
                var positive = feature.Vertical
                    ? ElevationLevel(Mathf.FloorToInt(feature.Position.x + .25f), tangentCell)
                    : ElevationLevel(tangentCell, Mathf.FloorToInt(feature.Position.y + .25f));
                // The feature position is the raised platform lip, not the middle of the ramp.
                // Interpolating symmetrically around it made an actor remain half-submerged after
                // their feet had already reached the upper floor. Keep the slope wholly on the
                // lower side and arrive at full platform height exactly at the threshold.
                if (negative < positive)
                {
                    var t = Mathf.InverseLerp(thresholdCoordinate - rampHalfDepth,
                        thresholdCoordinate, normalCoordinate);
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(negative, positive, t) * ElevationStepHeight;
                }
                if (positive < negative)
                {
                    var t = Mathf.InverseLerp(thresholdCoordinate,
                        thresholdCoordinate + rampHalfDepth, normalCoordinate);
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(negative, positive, t) * ElevationStepHeight;
                }
                return negative * ElevationStepHeight;
            }

            return ElevationLevel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y)) * ElevationStepHeight;
        }

        public bool IsOnElevationStair(Vector2 point)
        {
            foreach (var feature in architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs) continue;
                var normalDistance = feature.Vertical
                    ? Mathf.Abs(point.x - feature.Position.x)
                    : Mathf.Abs(point.y - feature.Position.y);
                var tangentDistance = feature.Vertical
                    ? Mathf.Abs(point.y - feature.Position.y)
                    : Mathf.Abs(point.x - feature.Position.x);
                if (normalDistance <= .76f && tangentDistance <= .43f) return true;
            }
            return false;
        }

        public float BoundaryHeight(Vector2 point)
        {
            const float sample = .08f;
            var offsets = new[]
            {
                new Vector2(sample, sample), new Vector2(-sample, sample),
                new Vector2(sample, -sample), new Vector2(-sample, -sample)
            };
            var foundFloor = false;
            var height = float.NegativeInfinity;
            foreach (var offset in offsets)
            {
                var probe = point + offset;
                if (!IsFloor(Mathf.FloorToInt(probe.x), Mathf.FloorToInt(probe.y))) continue;
                foundFloor = true;
                height = Mathf.Max(height, SurfaceHeight(probe));
            }
            // Void is not an elevation-zero platform. Including it in the maximum kept contour
            // walls around sunken rooms at the main floor while their actual floor moved down.
            return foundFloor ? height : 0f;
        }

        public void BeginVisibilityUpdate()
        {
            Array.Clear(visible, 0, visible.Length);
        }

        public void Reveal(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            explored[x, y] = true;
            visible[x, y] = true;
        }

        public bool CanOccupy(Vector2 point, float radius = 0.3f)
        {
            if (TouchesObstacle(point, radius, architectureObstacles)) return false;
            foreach (var obstacle in dynamicObstacles.Values)
                if (TouchesObstacle(point, radius, obstacle)) return false;
            return IsWalkable(Mathf.FloorToInt(point.x - radius), Mathf.FloorToInt(point.y - radius))
                && IsWalkable(Mathf.FloorToInt(point.x + radius), Mathf.FloorToInt(point.y - radius))
                && IsWalkable(Mathf.FloorToInt(point.x - radius), Mathf.FloorToInt(point.y + radius))
                && IsWalkable(Mathf.FloorToInt(point.x + radius), Mathf.FloorToInt(point.y + radius));
        }

        public bool CanTraverse(Vector2 from, Vector2 to, float radius = .3f)
        {
            if (!CanOccupy(to, radius)) return false;
            var fromLevel = ElevationLevel(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
            var toLevel = ElevationLevel(Mathf.FloorToInt(to.x), Mathf.FloorToInt(to.y));
            if (fromLevel == toLevel) return true;
            foreach (var feature in architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs) continue;
                var tangentDistance = feature.Vertical
                    ? Mathf.Abs(to.y - feature.Position.y)
                    : Mathf.Abs(to.x - feature.Position.x);
                var normalDistance = feature.Vertical
                    ? Mathf.Abs(to.x - feature.Position.x)
                    : Mathf.Abs(to.y - feature.Position.y);
                if (tangentDistance <= .43f - radius * .25f && normalDistance <= .78f) return true;
            }
            return false;
        }

        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            // Navigation is two-dimensional, combat is not. Floors that overlap in the isometric
            // projection must not see, aggro or shoot through one another. On a staircase the
            // sampled surface height changes continuously, so actors can engage only once their
            // feet are physically near the same part of the flight.
            if (!SharesCombatElevation(from, to)) return false;
            var distance = Vector2.Distance(from, to);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / .16f));
            var previous = from;
            for (var i = 1; i < steps; i++)
            {
                var sample = Vector2.Lerp(from, to, i / (float)steps);
                if (!CanOccupy(sample, .05f) || !CanTraverse(previous, sample, .05f)) return false;
                previous = sample;
            }
            return true;
        }

        public bool SharesCombatElevation(Vector2 first, Vector2 second, float tolerance = .30f) =>
            Mathf.Abs(SurfaceHeight(first) - SurfaceHeight(second)) <= tolerance;

        private static bool TouchesObstacle(Vector2 point, float radius, IReadOnlyList<Rect> obstacles)
        {
            for (var i = 0; i < obstacles.Count; i++)
                if (TouchesObstacle(point, radius, obstacles[i])) return true;
            return false;
        }

        private static bool TouchesObstacle(Vector2 point, float radius, Rect obstacle) =>
            point.x + radius > obstacle.xMin && point.x - radius < obstacle.xMax &&
            point.y + radius > obstacle.yMin && point.y - radius < obstacle.yMax;

        private bool IsWalkable(int x, int y) => IsFloor(x, y) && !obstacles[x, y];

        public Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }
}
