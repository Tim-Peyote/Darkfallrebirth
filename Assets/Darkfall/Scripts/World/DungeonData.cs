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
        ElevationStairs
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

        public DungeonArchitectureFeature(DungeonArchitectureKind kind, Vector2 position, bool vertical, bool flipX)
        {
            Kind = kind;
            Position = position;
            Vertical = vertical;
            FlipX = flipX;
        }
    }

    public sealed class DungeonData
    {
        private readonly bool[,] floor;
        private readonly bool[,] explored;
        private readonly bool[,] visible;
        private readonly bool[,] obstacles;
        private readonly byte[,] elevation;
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

        internal void SetElevation(RectInt area, byte level)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            for (var y = area.yMin; y < area.yMax; y++)
                if (IsFloor(x, y)) elevation[x, y] = level;
        }

        public int ElevationLevel(int x, int y) => IsFloor(x, y) ? elevation[x, y] : 0;

        public float SurfaceHeight(Vector2 point)
        {
            const float stepHeight = .34f;
            var cellX = Mathf.FloorToInt(point.x);
            var cellY = Mathf.FloorToInt(point.y);
            var fallback = ElevationLevel(cellX, cellY);
            var gridX = point.x - .5f;
            var gridY = point.y - .5f;
            var x0 = Mathf.FloorToInt(gridX);
            var y0 = Mathf.FloorToInt(gridY);
            var tx = gridX - x0;
            var ty = gridY - y0;
            float Sample(int x, int y) => IsFloor(x, y) ? elevation[x, y] : fallback;
            var bottom = Mathf.Lerp(Sample(x0, y0), Sample(x0 + 1, y0), tx);
            var top = Mathf.Lerp(Sample(x0, y0 + 1), Sample(x0 + 1, y0 + 1), tx);
            return Mathf.Lerp(bottom, top, ty) * stepHeight;
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
            return IsWalkable(Mathf.FloorToInt(point.x - radius), Mathf.FloorToInt(point.y - radius))
                && IsWalkable(Mathf.FloorToInt(point.x + radius), Mathf.FloorToInt(point.y - radius))
                && IsWalkable(Mathf.FloorToInt(point.x - radius), Mathf.FloorToInt(point.y + radius))
                && IsWalkable(Mathf.FloorToInt(point.x + radius), Mathf.FloorToInt(point.y + radius));
        }

        private bool IsWalkable(int x, int y) => IsFloor(x, y) && !obstacles[x, y];

        public Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }
}
