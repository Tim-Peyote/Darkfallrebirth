using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    [Serializable]
    public struct DungeonRoom
    {
        public RectInt bounds;
        public Vector2Int Center => new Vector2Int(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
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
        private readonly byte[,] elevation;
        private readonly Dictionary<int, Rect> dynamicObstacles = new Dictionary<int, Rect>();
        private readonly List<Rect> architectureObstacles = new List<Rect>();
        private int nextDynamicObstacleId = 1;
        private readonly List<DungeonLightSource> lightSources = new List<DungeonLightSource>();
        private readonly List<DungeonArchitectureFeature> architecture = new List<DungeonArchitectureFeature>();
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<DungeonRoom> Rooms { get; }
        public IReadOnlyList<DungeonLightSource> LightSources => lightSources;
        public IReadOnlyList<DungeonArchitectureFeature> Architecture => architecture;
        public Vector2Int StartCell => Rooms[0].Center;
        public Vector2Int ExitCell => Rooms[Rooms.Count - 1].Center;

        public DungeonData(bool[,] floor, List<DungeonRoom> rooms)
        {
            this.floor = floor;
            Width = floor.GetLength(0);
            Height = floor.GetLength(1);
            explored = new bool[Width, Height];
            visible = new bool[Width, Height];
            obstacles = new bool[Width, Height];
            elevation = new byte[Width, Height];
            Rooms = rooms;
        }

        public bool IsFloor(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && floor[x, y];
        }

        public bool IsExplored(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && explored[x, y];

        public bool IsVisible(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && visible[x, y];

        public bool BlocksVision(int x, int y) => !IsFloor(x, y) || obstacles[x, y];

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
        }

        internal void AddArchitecture(DungeonArchitectureFeature feature) => architecture.Add(feature);

        internal void AddArchitectureObstacle(Rect area) => architectureObstacles.Add(area);

        internal void AddStairTraversal(DungeonArchitectureFeature feature)
        {
            // Only the central ramp is walkable; the authored stone cheeks remain solid.
            const float laneWidth = .82f;
            const float crossingDepth = .52f;
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

        internal void SetElevation(RectInt area, byte level)
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

                var negative = feature.Vertical
                    ? ElevationLevel(Mathf.FloorToInt(feature.Position.x - .25f), Mathf.FloorToInt(point.y))
                    : ElevationLevel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(feature.Position.y - .25f));
                var positive = feature.Vertical
                    ? ElevationLevel(Mathf.FloorToInt(feature.Position.x + .25f), Mathf.FloorToInt(point.y))
                    : ElevationLevel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(feature.Position.y + .25f));
                // The feature position is the raised platform lip, not the middle of the ramp.
                // Interpolating symmetrically around it made an actor remain half-submerged after
                // their feet had already reached the upper floor. Keep the slope wholly on the
                // lower side and arrive at full platform height exactly at the threshold.
                if (negative < positive)
                {
                    var t = Mathf.InverseLerp(thresholdCoordinate - rampHalfDepth,
                        thresholdCoordinate, normalCoordinate);
                    return Mathf.Lerp(negative, positive, t) * ElevationStepHeight;
                }
                if (positive < negative)
                {
                    var t = Mathf.InverseLerp(thresholdCoordinate,
                        thresholdCoordinate + rampHalfDepth, normalCoordinate);
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
            return Mathf.Max(
                SurfaceHeight(point + new Vector2(sample, sample)),
                SurfaceHeight(point + new Vector2(-sample, sample)),
                SurfaceHeight(point + new Vector2(sample, -sample)),
                SurfaceHeight(point + new Vector2(-sample, -sample)));
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
