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

    public sealed class DungeonData
    {
        private readonly bool[,] floor;
        private readonly bool[,] explored;
        private readonly bool[,] visible;
        private readonly bool[,] obstacles;
        private readonly List<DungeonLightSource> lightSources = new List<DungeonLightSource>();
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<DungeonRoom> Rooms { get; }
        public IReadOnlyList<DungeonLightSource> LightSources => lightSources;
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

        public void AddObstacle(Vector2 position)
        {
            var x = Mathf.FloorToInt(position.x);
            var y = Mathf.FloorToInt(position.y);
            if (x >= 0 && y >= 0 && x < Width && y < Height) obstacles[x, y] = true;
        }

        public void AddLightSource(Vector2 position, Color color, float radius, float flicker = .1f)
        {
            lightSources.Add(new DungeonLightSource(position, color, radius, flicker));
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
