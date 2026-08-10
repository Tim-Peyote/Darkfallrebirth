using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.World
{
    public sealed class DungeonView : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private DungeonVisualProfile profile;
        private Transform architectureDecor;
        private Transform structuralDecor;
        private Transform lightDecor;
        private Transform clutterDecor;

        public void Build(DungeonData data, int depth = 1)
        {
            Clear();
            profile = DungeonVisualProfile.ForDepth(depth);
            gameObject.name = "Dungeon · " + profile.Id;
            var contour = DungeonContour.Build(data);
            architectureDecor = CreateGroup(transform, "Architecture · " + profile.Id);
            BuildContourFloor(contour, data);
            BuildContourWalls(contour, data);
            BuildContourShadows(contour);
            var decorRoot = new GameObject("Decor · " + profile.Id).transform;
            decorRoot.SetParent(transform, false);
            structuralDecor = CreateGroup(decorRoot, "Structural");
            lightDecor = CreateGroup(decorRoot, "Light Sources");
            clutterDecor = CreateGroup(decorRoot, "Clutter");
            BuildDecor(data);
        }

        private void BuildContourFloor(DungeonContour contour, DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            const float uvScale = .08f;
            foreach (var polygon in contour.FloorPolygons)
            {
                var index = vertices.Count;
                var center = Vector2.zero;
                for (var i = 0; i < polygon.Length; i++) center += polygon[i];
                center /= polygon.Length;
                var elevation = data.SurfaceHeight(center);
                var tint = profile.FloorTint * RandomTint(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
                for (var i = 0; i < polygon.Length; i++)
                {
                    var point = IsoWorld.Project(polygon[i]);
                    point.y += elevation;
                    vertices.Add(point);
                    colors.Add(tint);
                    uvs.Add(polygon[i] * uvScale);
                }
                for (var i = 1; i < polygon.Length - 1; i++)
                {
                    triangles.Add(index);
                    triangles.Add(index + i + 1);
                    triangles.Add(index + i);
                }
            }
            var mesh = MakeMesh("Continuous Isometric Floor", vertices, triangles, colors, uvs);
            var material = CreateTexturedMaterial(profile.FloorTexture);
            CreateLayer(mesh.name, mesh, material, -20);
            BuildElevationRisers(data);
        }

        private void BuildElevationRisers(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var color = Color.Lerp(profile.WallTint, Color.white, profile.WallReadability) * .72f;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var level = data.ElevationLevel(x, y);
                if (level <= 0) continue;
                var height = data.SurfaceHeight(new Vector2(x + .5f, y + .5f));
                if (data.ElevationLevel(x, y - 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y), false))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x, y), new Vector2(x + 1, y), height, color);
                if (data.ElevationLevel(x + 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + 1, y + .5f), true))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y), new Vector2(x + 1, y + 1), height, color * .84f);
                if (data.ElevationLevel(x, y + 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y + 1), false))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y + 1), new Vector2(x, y + 1), height, color);
                if (data.ElevationLevel(x - 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x, y + .5f), true))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x, y + 1), new Vector2(x, y), height, color * .84f);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Raised Platform Fascias", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.WallTexture), 970);
        }

        private static bool IsStairRiserOpening(DungeonData data, Vector2 midpoint, bool vertical)
        {
            foreach (var feature in data.Architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs || feature.Vertical != vertical) continue;
                var normalDistance = vertical
                    ? Mathf.Abs(midpoint.x - feature.Position.x)
                    : Mathf.Abs(midpoint.y - feature.Position.y);
                var tangentDistance = vertical
                    ? Mathf.Abs(midpoint.y - feature.Position.y)
                    : Mathf.Abs(midpoint.x - feature.Position.x);
                if (normalDistance < .05f && tangentDistance <= .51f) return true;
            }
            return false;
        }

        private static void AddRiser(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            List<Vector2> uvs, Vector2 from, Vector2 to, float height, Color color)
        {
            var lowerFrom = IsoWorld.Project(from);
            var lowerTo = IsoWorld.Project(to);
            var upperFrom = lowerFrom + Vector2.up * height;
            var upperTo = lowerTo + Vector2.up * height;
            AddScreenQuad(vertices, triangles, colors, uvs, lowerFrom, lowerTo, upperTo, upperFrom,
                color, from, to);
        }

        private void BuildContourWalls(DungeonContour contour, DungeonData data)
        {
            var maximumDepth = data.Width + data.Height;
            var hasArchitecture = ArchitectureSpriteLibrary.HasBiome(profile.Id);
            var wallTexture = Resources.Load<Texture2D>(profile.WallTexture);
            if (wallTexture != null)
            {
                wallTexture.wrapMode = TextureWrapMode.Repeat;
                wallTexture.filterMode = FilterMode.Bilinear;
            }

            var shadowVertices = new List<Vector3>();
            var shadowTriangles = new List<int>();
            var shadowColors = new List<Color>();
            var shadowUvs = new List<Vector2>();

            foreach (var segment in contour.Segments)
            {
                AddContactShadow(shadowVertices, shadowTriangles, shadowColors, shadowUvs, segment.From, segment.To);
            }
            var shadowMesh = MakeMesh("Continuous Wall Contact Shadow", shadowVertices, shadowTriangles,
                shadowColors, shadowUvs);
            var shadowMaterial = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            materials.Add(shadowMaterial);
            CreateLayer(shadowMesh.name, shadowMesh, shadowMaterial, -5);

            // The continuous mesh is only a legacy fallback. Drawing it below a complete authored
            // kit reintroduces the bright polygonal rails the modular architecture replaces.
            if (hasArchitecture)
            {
                BuildArchitectureModules(contour, data);
                BuildThresholdArchitecture(data);
                return;
            }

            for (var depth = -2; depth <= maximumDepth + 2; depth++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var colors = new List<Color>();
                var uvs = new List<Vector2>();
                foreach (var segment in contour.Segments)
                {
                    var midpoint = (segment.From + segment.To) * .5f;
                    if (Mathf.RoundToInt(midpoint.x + midpoint.y) != depth) continue;
                    var projected = IsoWorld.Project(segment.To) - IsoWorld.Project(segment.From);
                    var shade = projected.x >= 0 ? .88f : .72f;
                    if (Mathf.Abs(projected.x) < .08f) shade = .8f;
                    var readableWall = Color.Lerp(profile.WallTint, Color.white, profile.WallReadability);
                    AddWallFace(vertices, triangles, colors, uvs, segment.From, segment.To,
                        readableWall * shade);
                }
                if (vertices.Count == 0) continue;
                var mesh = MakeMesh("Contour Wall Facades · " + depth, vertices, triangles, colors, uvs);
                var material = new Material(DarkfallRenderMaterials.SpriteLit)
                    { color = Color.white, mainTexture = wallTexture };
                materials.Add(material);
                CreateLayer(mesh.name, mesh, material, 1040 + depth * IsoWorld.DepthPrecision);
                var fillMaterial = new Material(DarkfallRenderMaterials.SpriteUnlit)
                {
                    color = new Color(1f, 1f, 1f, profile.WallFill),
                    mainTexture = wallTexture
                };
                materials.Add(fillMaterial);
                CreateLayer("Wall Texture Fill · " + depth, mesh, fillMaterial,
                    1041 + depth * IsoWorld.DepthPrecision);
            }

        }

        private void BuildArchitectureModules(DungeonContour contour, DungeonData data)
        {
            var moduleIndex = 0;
            var cornerPoints = new HashSet<Vector2Int>();
            foreach (var segment in contour.Segments)
            {
                var from = Quantize(segment.From);
                var to = Quantize(segment.To);
                var fromBits = CountMaskBits(FloorQuadrantMask(data, segment.From));
                var toBits = CountMaskBits(FloorQuadrantMask(data, segment.To));
                if (fromBits == 1 || fromBits == 3) cornerPoints.Add(from);
                if (toBits == 1 || toBits == 3) cornerPoints.Add(to);
            }
            foreach (var span in BuildBoundarySpans(contour.Segments))
            {
                var length = Mathf.RoundToInt(span.End - span.Start);
                var from = span.Vertical
                    ? new Vector2(span.Fixed, span.Start)
                    : new Vector2(span.Start, span.Fixed);
                var to = span.Vertical
                    ? new Vector2(span.Fixed, span.End)
                    : new Vector2(span.End, span.Fixed);
                var edgeHash = EdgeHash(from, to, length);
                for (var section = 0; section < length; section++)
                {
                    var coordinate = span.Start + section + .5f;
                    var anchor = span.Vertical
                        ? new Vector2(span.Fixed, coordinate)
                        : new Vector2(coordinate, span.Fixed);
                    if (FeatureReplacesWallModule(data, anchor)) continue;
                    var role = span.Vertical ? "wall-right" : "wall-left";
                    if (length >= 7 && section >= 2 && section <= length - 3 &&
                        (section + edgeHash) % 7 == 3)
                    {
                        var accent = (edgeHash + section) % 3;
                        // The small lancet/arch asset reads as a doorway at gameplay scale even
                        // though it is authored as a wall window. Keep passage semantics out of
                        // decorative wall variation: only solid niches and damaged masonry may
                        // be selected here. Real openings are emitted by the threshold grammar.
                        role = accent == 1 ? "wall-broken" : "wall-niche";
                    }
                    CreateArchitectureModule(role, anchor, span.Vertical, .92f, moduleIndex++,
                        data.BoundaryHeight(anchor));
                }
            }

            foreach (var pointKey in cornerPoints)
            {
                var point = new Vector2(pointKey.x * .5f, pointKey.y * .5f);
                var mask = FloorQuadrantMask(data, point);
                var bits = CountMaskBits(mask);
                if (bits != 1 && bits != 3) continue;
                if (FeatureReplacesWallModule(data, point)) continue;
                // The kit has horizontal mirrors, not a second near/far projection. A back-facing
                // concave corner (mask 1), or its inverse (mask 14), cannot be represented by the
                // available sprite without producing the long downward V/pillar seen in play.
                // Let the two straight modules form that hidden/back junction instead.
                if (mask == 1 || mask == 14) continue;
                // Role is named from the walkable side: a room corner (one occupied quadrant) is
                // concave to the player, while a void notch (three occupied quadrants) is a convex
                // masonry pier. The previous mapping was geometrically reversed.
                CreateArchitectureModule(bits == 1 ? "corner-inner" : "corner-outer",
                    point, FlipCorner(mask), .82f, moduleIndex++, data.BoundaryHeight(point));
            }
        }

        private static void AddWallWindowObstacle(DungeonData data, Vector2 anchor, bool vertical)
        {
            // arch-open is the authored lancet/window module. Its dark aperture is visual depth,
            // not a doorway: preserve a solid wall plane even if two carved floor regions happen
            // to approach the same contour closely.
            const float span = 1.02f;
            const float depth = .34f;
            data.AddArchitectureObstacle(vertical
                ? new Rect(anchor.x - depth * .5f, anchor.y - span * .5f, depth, span)
                : new Rect(anchor.x - span * .5f, anchor.y - depth * .5f, span, depth));
        }

        private static List<BoundarySpan> BuildBoundarySpans(IReadOnlyList<DungeonContourSegment> segments)
        {
            var units = new List<BoundarySpan>(segments.Count);
            foreach (var segment in segments)
            {
                var vertical = Mathf.Abs(segment.From.x - segment.To.x) < .01f;
                var fixedCoordinate = vertical ? segment.From.x : segment.From.y;
                var first = vertical ? segment.From.y : segment.From.x;
                var second = vertical ? segment.To.y : segment.To.x;
                units.Add(new BoundarySpan(vertical, fixedCoordinate, Mathf.Min(first, second), Mathf.Max(first, second)));
            }
            units.Sort((a, b) =>
            {
                var axis = a.Vertical.CompareTo(b.Vertical);
                if (axis != 0) return axis;
                var fixedResult = a.Fixed.CompareTo(b.Fixed);
                return fixedResult != 0 ? fixedResult : a.Start.CompareTo(b.Start);
            });
            var spans = new List<BoundarySpan>();
            foreach (var unit in units)
            {
                if (spans.Count > 0)
                {
                    var previous = spans[spans.Count - 1];
                    if (previous.Vertical == unit.Vertical && Mathf.Abs(previous.Fixed - unit.Fixed) < .01f &&
                        Mathf.Abs(previous.End - unit.Start) < .01f)
                    {
                        spans[spans.Count - 1] = new BoundarySpan(previous.Vertical, previous.Fixed,
                            previous.Start, unit.End);
                        continue;
                    }
                }
                spans.Add(unit);
            }
            return spans;
        }

        private static int FloorQuadrantMask(DungeonData data, Vector2 point)
        {
            var x = Mathf.RoundToInt(point.x);
            var y = Mathf.RoundToInt(point.y);
            return (data.IsFloor(x - 1, y - 1) ? 1 : 0) |
                   (data.IsFloor(x, y - 1) ? 2 : 0) |
                   (data.IsFloor(x, y) ? 4 : 0) |
                   (data.IsFloor(x - 1, y) ? 8 : 0);
        }

        private static bool FeatureReplacesWallModule(DungeonData data, Vector2 point)
        {
            foreach (var feature in data.Architecture)
            {
                var radius = feature.Kind == DungeonArchitectureKind.ElevationStairs ? 1.05f : 1.35f;
                if (Vector2.Distance(point, feature.Position) < radius) return true;
            }
            return false;
        }

        private readonly struct BoundarySpan
        {
            public readonly bool Vertical;
            public readonly float Fixed;
            public readonly float Start;
            public readonly float End;

            public BoundarySpan(bool vertical, float fixedCoordinate, float start, float end)
            {
                Vertical = vertical;
                Fixed = fixedCoordinate;
                Start = start;
                End = end;
            }
        }

        private int EdgeHash(Vector2 from, Vector2 to, int sections) =>
            (Mathf.RoundToInt((from.x + to.x) * 47f) * 73856093 ^
             Mathf.RoundToInt((from.y + to.y) * 47f) * 19349663 ^
             profile.Chapter * 83492791 ^ sections * 31) & int.MaxValue;

        private static Vector2Int Quantize(Vector2 point) =>
            new Vector2Int(Mathf.RoundToInt(point.x * 2f), Mathf.RoundToInt(point.y * 2f));

        private void BuildThresholdArchitecture(DungeonData data)
        {
            var featureIndex = 100000;
            foreach (var feature in data.Architecture)
            {
                if (feature.Kind == DungeonArchitectureKind.ClosedDoor)
                {
                    DungeonDoor.Spawn(data, feature, profile.Id, architectureDecor);
                    continue;
                }
                var role = feature.Kind == DungeonArchitectureKind.OpenGate ? "arcade" : "stairs";
                var scale = feature.Kind == DungeonArchitectureKind.OpenGate ? .98f : .86f;
                CreateArchitectureModule(role, feature.Position, feature.Vertical, scale, featureIndex++, 0f);
            }
        }

        private void CreateArchitectureModule(string role, Vector2 anchor, bool flipX, float scale, int index,
            float elevation = 0f)
        {
            var sprite = ArchitectureSpriteLibrary.Module(profile.Id, role);
            if (sprite == null) return;

            var owner = new GameObject($"{role} · {index}");
            owner.transform.SetParent(architectureDecor, false);
            owner.transform.position = anchor;

            var visual = new GameObject("Projected Architecture");
            visual.transform.SetParent(owner.transform, false);
            visual.transform.localScale = Vector3.one * scale;

            // Authored sprites already contain their own material shading. A restrained unlit pass
            // keeps carved detail legible in the global darkness; the lit pass still receives local
            // torches and player light.
            var readability = visual.AddComponent<SpriteRenderer>();
            readability.sprite = sprite;
            readability.flipX = flipX;
            readability.color = new Color(.72f, .72f, .72f, .30f);
            readability.sortingOrder = 0;
            DarkfallRenderMaterials.MakeEmissive(readability);

            var litObject = new GameObject("Local Light Pass");
            litObject.transform.SetParent(visual.transform, false);
            var lit = litObject.AddComponent<SpriteRenderer>();
            lit.sprite = sprite;
            lit.flipX = flipX;
            lit.color = Color.white;
            lit.sortingOrder = 1;
            DarkfallRenderMaterials.MakeLit(lit);

            // Share the actor depth plane. The logical x+y position now decides whether the actor
            // is in front of or behind a wall; the old +40 bias swallowed actors on the near side.
            visual.AddComponent<IsoVisual>().Initialize(owner.transform, elevation, 1002, false);
        }

        private static int CountMaskBits(int mask)
        {
            var bits = 0;
            for (var value = mask; value != 0; value >>= 1) bits += value & 1;
            return bits;
        }

        private static bool FlipCorner(int mask) =>
            // Screen-space mirroring swaps the logical east/south quadrants (2 <-> 8). It does
            // not invert the near/far quadrants; doing that was what made most corners face out.
            mask == 2 || mask == 13;

        private Material CreateTexturedMaterial(string path)
        {
            var material = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            var texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                material.mainTexture = texture;
            }
            materials.Add(material);
            return material;
        }

        private void AddContactShadow(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to)
        {
            var a = IsoWorld.Project(from);
            var b = IsoWorld.Project(to);
            var normal = Vector2.Perpendicular((b - a).normalized) * .026f;
            AddScreenQuad(v, t, c, uv, a - normal, b - normal, b + normal, a + normal,
                new Color(.018f, .016f, .014f, .34f), from, to);
        }

        private static void AddScreenQuad(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 a, Vector2 b, Vector2 d, Vector2 e, Color color, Vector2 logicalFrom, Vector2 logicalTo)
        {
            var index = v.Count;
            v.Add(a); v.Add(b); v.Add(d); v.Add(e);
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            var length = Vector2.Distance(logicalFrom, logicalTo) * .16f;
            uv.Add(Vector2.zero); uv.Add(new Vector2(length, 0));
            uv.Add(new Vector2(length, .08f)); uv.Add(new Vector2(0, .08f));
        }

        public void Clear()
        {
            for (var i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in materials) Destroy(material);
            meshes.Clear();
            materials.Clear();
        }

        private void AddWallFace(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to, Color color)
        {
            var baseFrom = IsoWorld.Project(from);
            var baseTo = IsoWorld.Project(to);
            var height = profile.WallHeight;
            var plinth = height * .18f;
            var frieze = height * .78f;
            AddWallBand(v, t, c, uv, baseFrom, baseTo, 0f, plinth, color * .68f, from, to, 0f, .16f);
            AddWallBand(v, t, c, uv, baseFrom, baseTo, plinth, frieze, color, from, to, .16f, .72f);
            AddWallBand(v, t, c, uv, baseFrom, baseTo, frieze, height, color * 1.12f, from, to, .72f, .92f);

            var topFrom = baseFrom + Vector2.up * height;
            var topTo = baseTo + Vector2.up * height;
            var projected = topTo - topFrom;
            var crownOffset = Vector2.Perpendicular(projected.normalized) * .105f;
            if (crownOffset.y < 0f) crownOffset = -crownOffset;
            AddScreenQuad(v, t, c, uv, topFrom, topTo, topTo + crownOffset, topFrom + crownOffset,
                color * .92f, from, to);
        }

        private static void AddWallBand(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 baseFrom, Vector2 baseTo, float bottom, float top, Color color,
            Vector2 logicalFrom, Vector2 logicalTo, float uvBottom, float uvTop)
        {
            var index = v.Count;
            var lowerFrom = baseFrom + Vector2.up * bottom;
            var lowerTo = baseTo + Vector2.up * bottom;
            var upperFrom = baseFrom + Vector2.up * top;
            var upperTo = baseTo + Vector2.up * top;
            v.Add(lowerFrom);
            v.Add(lowerTo);
            v.Add(upperTo);
            v.Add(upperFrom);
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            var length = Vector2.Distance(logicalFrom, logicalTo) * .22f;
            uv.Add(new Vector2(0, uvBottom)); uv.Add(new Vector2(length, uvBottom));
            uv.Add(new Vector2(length, uvTop)); uv.Add(new Vector2(0, uvTop));
        }


        private void BuildDecor(DungeonData data)
        {
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                var hash = ((bounds.x * 73856093) ^ (bounds.y * 19349663) ^
                            (roomIndex * 83492791) ^ (profile.Chapter * 297121507)) & int.MaxValue;
                if (roomIndex % profile.LightEveryRooms == 1)
                {
                    var lightProp = profile.Id == "ashen-catacombs" ? 2 : (roomIndex % 2 == 0 ? 0 : 8);
                    CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f),
                        profile.Id == "ashen-catacombs" ? 1f : .72f, "Biome Light", false, lightDecor);
                }

                // A Diablo theme room is reserved and decorated as one subject. Pick one wall zone
                // for this room and build a deterministic cluster around it instead of scattering
                // unrelated props over every walkable tile.
                var theme = (hash / 17) % 4;
                var anchor = ThemeAnchor(bounds, theme);
                var primary = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
                if (roomIndex > 0 && roomIndex < data.Rooms.Count - 1 && bounds.width >= 10 && bounds.height >= 9)
                    CreateProp(data, primary, anchor, .82f + (hash % 3) * .07f,
                        $"Theme {theme} · Primary", true, structuralDecor);

                var clusterCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 48f *
                    profile.DecorDensity), 2, 6);
                for (var member = 0; member < clusterCount; member++)
                {
                    var angle = (member / (float)clusterCount) * Mathf.PI * 2f + theme * .43f;
                    var radius = 1.05f + .55f * Hash01(hash + member * 92821);
                    var position = anchor + new Vector2(Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius * .72f);
                    position.x = Mathf.Clamp(position.x, bounds.xMin + 1.1f, bounds.xMax - 1.1f);
                    position.y = Mathf.Clamp(position.y, bounds.yMin + 1.1f, bounds.yMax - 1.1f);
                    var propIndex = profile.ClutterProps[(hash / (member + 3) + member * 5) %
                                                         profile.ClutterProps.Length];
                    CreateProp(data, propIndex, position, .48f + member % 3 * .08f,
                        $"Theme {theme} · Detail", false, clutterDecor);
                }
            }
        }

        private static Vector2 ThemeAnchor(RectInt bounds, int theme)
        {
            switch (theme)
            {
                case 0: return new Vector2(bounds.center.x, bounds.yMax - 1.35f);
                case 1: return new Vector2(bounds.xMax - 1.35f, bounds.center.y);
                case 2: return new Vector2(bounds.center.x, bounds.yMin + 1.35f);
                default: return new Vector2(bounds.xMin + 1.35f, bounds.center.y);
            }
        }

        private void CreateProp(DungeonData data, int index, Vector2 position, float scale, string objectName, bool blocks,
            Transform group)
        {
            if (!data.IsFloor(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return;
            foreach (var feature in data.Architecture)
                if (Vector2.Distance(position, feature.Position) < 1.6f) return;
            if (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 1.25f ||
                Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 1.25f) return;
            if (blocks && (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 2f ||
                           Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 2f))
                blocks = false;
            if (blocks && !data.TryAddObstaclePreservingRoutes(position)) return;
            var prop = new GameObject(objectName + " " + index);
            prop.transform.SetParent(group, false);
            prop.transform.position = position;
            if (profile.Id != "ashen-catacombs" && (index == 0 || index == 8)) scale = .72f;
            var visual = new GameObject("Projected Prop");
            visual.transform.SetParent(prop.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = EnvironmentSpriteAtlas.Prop(profile.Id, index);
            renderer.color = Color.white;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.AddComponent<IsoVisual>().Initialize(prop.transform, 0f, 1000);
            if (blocks)
            {
                var caster = visual.AddComponent<ShadowCaster2D>();
                caster.castsShadows = true;
                caster.selfShadows = false;
                caster.alphaCutoff = .22f;
            }
            var customBiomeDecor = profile.Id != "ashen-catacombs";
            if ((!customBiomeDecor && index == 2) || (customBiomeDecor && index == 0))
            {
                AddFlame(visual.transform, new Vector2(0, .24f), .56f, 9);
                data.AddLightSource(position + new Vector2(0, .22f), profile.FireTint, 5.8f, .16f);
            }
            else if (customBiomeDecor && index == 3)
            {
                data.AddLightSource(position + new Vector2(0, .28f), profile.FireTint, 4.6f, .13f);
            }
            else if (customBiomeDecor && index == 8)
            {
                data.AddLightSource(position + new Vector2(0, .18f), profile.FireTint, 4.8f, .14f);
            }
            else if (index == 8)
            {
                AddFlame(visual.transform, new Vector2(-.37f, .31f), .105f, 9);
                AddFlame(visual.transform, new Vector2(.19f, .29f), .10f, 9);
                AddFlame(visual.transform, new Vector2(.37f, .13f), .09f, 9);
                data.AddLightSource(position + new Vector2(0, .2f), profile.FireTint * new Color(1, 1, 1, .62f), 3.6f, .1f);
            }
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void AddFlame(Transform parent, Vector2 localPosition, float scale, int sortingOrder)
        {
            var flame = new GameObject("Animated Flame");
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = localPosition;
            flame.transform.localScale = Vector3.one * scale;
            flame.AddComponent<DungeonFlameAnimator>().Initialize(sortingOrder);
        }

        private void BuildContourShadows(DungeonContour contour)
        {
            var root = new GameObject("Smoothed Isometric Shadows");
            root.transform.SetParent(transform, false);
            root.AddComponent<CompositeShadowCaster2D>();
            foreach (var segment in contour.Segments)
                CreateShadowEdge(root.transform, segment.From, segment.To, profile.WallHeight);
        }

        private static void CreateShadowEdge(Transform parent, Vector2 from, Vector2 to, float wallHeight)
        {
            var a = IsoWorld.Project(from) + Vector2.up * (wallHeight * .55f);
            var b = IsoWorld.Project(to) + Vector2.up * (wallHeight * .55f);
            var delta = b - a;
            var shadow = new GameObject("Wall Shadow Edge");
            shadow.transform.SetParent(parent, false);
            shadow.transform.position = (a + b) * .5f;
            shadow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            shadow.transform.localScale = new Vector3(delta.magnitude, .12f, 1);
            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeAssets.Square;
            renderer.color = Color.clear;
            renderer.sortingOrder = -100;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            var caster = shadow.AddComponent<ShadowCaster2D>();
            caster.castsShadows = true;
            caster.selfShadows = false;
            caster.alphaCutoff = .01f;
        }

        private Mesh MakeMesh(string name, List<Vector3> vertices, List<int> triangles, List<Color> colors, List<Vector2> uvs)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        private void CreateLayer(string name, Mesh mesh, Material material, int sortingOrder)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
        }

        private static float RandomTint(int x, int y)
        {
            var hash = (x * 73856093) ^ (y * 19349663);
            return .90f + Mathf.Abs(hash % 13) / 100f;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
            }
            return (value & 0x7fffffff) / (float)int.MaxValue;
        }

    }
}
