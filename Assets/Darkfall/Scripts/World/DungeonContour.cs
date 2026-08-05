using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    internal readonly struct DungeonContourSegment
    {
        public readonly Vector2 From;
        public readonly Vector2 To;
        public DungeonContourSegment(Vector2 from, Vector2 to) { From = from; To = to; }
    }

    internal sealed class DungeonContour
    {
        public readonly List<Vector2[]> FloorPolygons = new List<Vector2[]>();
        public readonly List<DungeonContourSegment> Segments = new List<DungeonContourSegment>();

        public static DungeonContour Build(DungeonData data)
        {
            var result = new DungeonContour();
            for (var x = -1; x < data.Width; x++)
            for (var y = -1; y < data.Height; y++)
            {
                var mask = (data.IsFloor(x, y) ? 1 : 0) |
                           (data.IsFloor(x + 1, y) ? 2 : 0) |
                           (data.IsFloor(x + 1, y + 1) ? 4 : 0) |
                           (data.IsFloor(x, y + 1) ? 8 : 0);
                if (mask == 0) continue;
                AddCase(result, mask, x, y);
            }
            return result;
        }

        private static void AddCase(DungeonContour result, int mask, float x, float y)
        {
            var p0 = new Vector2(x, y);
            var p1 = new Vector2(x + 1, y);
            var p2 = new Vector2(x + 1, y + 1);
            var p3 = new Vector2(x, y + 1);
            var b = (p0 + p1) * .5f;
            var r = (p1 + p2) * .5f;
            var t = (p2 + p3) * .5f;
            var l = (p3 + p0) * .5f;

            switch (mask)
            {
                case 1: AddPolygon(result, p0, b, l); AddSegment(result, l, b); break;
                case 2: AddPolygon(result, p1, r, b); AddSegment(result, b, r); break;
                case 3: AddPolygon(result, p0, p1, r, l); AddSegment(result, l, r); break;
                case 4: AddPolygon(result, p2, t, r); AddSegment(result, r, t); break;
                case 5:
                    AddPolygon(result, p0, b, l); AddPolygon(result, p2, t, r);
                    AddSegment(result, l, b); AddSegment(result, r, t); break;
                case 6: AddPolygon(result, b, p1, p2, t); AddSegment(result, b, t); break;
                case 7: AddPolygon(result, p0, p1, p2, t, l); AddSegment(result, l, t); break;
                case 8: AddPolygon(result, p3, l, t); AddSegment(result, t, l); break;
                case 9: AddPolygon(result, p0, b, t, p3); AddSegment(result, t, b); break;
                case 10:
                    AddPolygon(result, p1, r, b); AddPolygon(result, p3, l, t);
                    AddSegment(result, b, r); AddSegment(result, t, l); break;
                case 11: AddPolygon(result, p0, p1, r, t, p3); AddSegment(result, t, r); break;
                case 12: AddPolygon(result, l, r, p2, p3); AddSegment(result, r, l); break;
                case 13: AddPolygon(result, p0, b, r, p2, p3); AddSegment(result, r, b); break;
                case 14: AddPolygon(result, b, p1, p2, p3, l); AddSegment(result, b, l); break;
                case 15: AddPolygon(result, p0, p1, p2, p3); break;
            }
        }

        private static void AddPolygon(DungeonContour result, params Vector2[] points) =>
            result.FloorPolygons.Add(points);

        private static void AddSegment(DungeonContour result, Vector2 from, Vector2 to) =>
            result.Segments.Add(new DungeonContourSegment(from, to));
    }
}
