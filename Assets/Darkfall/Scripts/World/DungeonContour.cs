using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    internal readonly struct DungeonContourSegment
    {
        public readonly Vector2 From;
        public readonly Vector2 To;
        public readonly int Mask;
        public DungeonContourSegment(Vector2 from, Vector2 to, int mask) { From = from; To = to; Mask = mask; }
    }

    internal sealed class DungeonContour
    {
        public readonly List<Vector2[]> FloorPolygons = new List<Vector2[]>();
        public readonly List<DungeonContourSegment> Segments = new List<DungeonContourSegment>();

        public static DungeonContour Build(DungeonData data)
        {
            var result = new DungeonContour();
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                if (!data.IsFloor(x, y)) continue;
                var bottomLeft = new Vector2(x, y);
                var bottomRight = new Vector2(x + 1, y);
                var topRight = new Vector2(x + 1, y + 1);
                var topLeft = new Vector2(x, y + 1);
                AddPolygon(result, bottomLeft, bottomRight, topRight, topLeft);

                // Collision cells occupy [x,x+1] x [y,y+1]. Keeping the visual boundary on the
                // same integer grid removes the half-cell bevels and artificial diamonds created
                // by the old marching-squares pass.
                if (!data.IsFloor(x, y - 1)) AddSegment(result, bottomLeft, bottomRight, 1);
                if (!data.IsFloor(x + 1, y)) AddSegment(result, bottomRight, topRight, 2);
                if (!data.IsFloor(x, y + 1)) AddSegment(result, topRight, topLeft, 4);
                if (!data.IsFloor(x - 1, y)) AddSegment(result, topLeft, bottomLeft, 8);
            }
            return result;
        }

        private static void AddPolygon(DungeonContour result, params Vector2[] points) =>
            result.FloorPolygons.Add(points);

        private static void AddSegment(DungeonContour result, Vector2 from, Vector2 to, int mask) =>
            result.Segments.Add(new DungeonContourSegment(from, to, mask));
    }
}
