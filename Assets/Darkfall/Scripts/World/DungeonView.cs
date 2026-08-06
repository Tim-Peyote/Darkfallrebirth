using System.Collections.Generic;
using Darkfall.Core;
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
            BuildContourFloor(contour);
            BuildContourWalls(contour, data);
            BuildContourShadows(contour);
            var decorRoot = new GameObject("Decor · " + profile.Id).transform;
            decorRoot.SetParent(transform, false);
            structuralDecor = CreateGroup(decorRoot, "Structural");
            lightDecor = CreateGroup(decorRoot, "Light Sources");
            clutterDecor = CreateGroup(decorRoot, "Clutter");
            BuildDecor(data);
        }

        private void BuildContourFloor(DungeonContour contour)
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
                var tint = profile.FloorTint * RandomTint(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
                for (var i = 0; i < polygon.Length; i++)
                {
                    var point = IsoWorld.Project(polygon[i]);
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
                BuildArchitectureModules(contour);
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

        private void BuildArchitectureModules(DungeonContour contour)
        {
            var moduleIndex = 0;
            foreach (var rawPath in BuildContourPaths(contour.Segments))
            {
                var path = SimplifyOrthogonalPath(rawPath);
                if (path.Count < 3) continue;
                for (var edge = 0; edge < path.Count; edge++)
                {
                    var from = path[edge];
                    var to = path[(edge + 1) % path.Count];
                    var delta = to - from;
                    var horizontal = Mathf.Abs(delta.y) < .12f;
                    var vertical = Mathf.Abs(delta.x) < .12f;
                    if (!horizontal && !vertical) continue;

                    var length = horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
                    var sections = Mathf.Max(1, Mathf.RoundToInt(length));
                    var edgeHash = EdgeHash(from, to, sections);
                    for (var section = 0; section < sections; section++)
                    {
                        var anchor = Vector2.Lerp(from, to, (section + .5f) / sections);
                        var role = vertical ? "wall-right" : "wall-left";
                        if (sections >= 5 && section >= 2 && section <= sections - 3 &&
                            (section + edgeHash) % 7 == 3)
                        {
                            var accent = (edgeHash + section) % 4;
                            role = accent == 1 ? "arcade" : accent == 2 ? "wall-broken" : "wall-niche";
                        }
                        CreateArchitectureModule(role, anchor, vertical, .92f, moduleIndex++);
                    }
                }

                // A sharp orthogonal turn has no bevel edge of its own, so it receives exactly
                // one corner module at the shared contour vertex.
                for (var vertex = 0; vertex < path.Count; vertex++)
                {
                    var previous = path[(vertex - 1 + path.Count) % path.Count];
                    var current = path[vertex];
                    var next = path[(vertex + 1) % path.Count];
                    var incoming = current - previous;
                    var outgoing = next - current;
                    var incomingAxis = Mathf.Abs(incoming.x) < .12f || Mathf.Abs(incoming.y) < .12f;
                    var outgoingAxis = Mathf.Abs(outgoing.x) < .12f || Mathf.Abs(outgoing.y) < .12f;
                    if (!incomingAxis || !outgoingAxis || Mathf.Abs(Vector2.Dot(incoming.normalized, outgoing.normalized)) > .15f)
                        continue;
                    var incomingLength = Mathf.Max(Mathf.Abs(incoming.x), Mathf.Abs(incoming.y));
                    var outgoingLength = Mathf.Max(Mathf.Abs(outgoing.x), Mathf.Abs(outgoing.y));
                    if (incomingLength < 1.5f || outgoingLength < 1.5f) continue;
                    var mask = ClosestContourMask(contour.Segments, current);
                    CreateArchitectureModule(CountMaskBits(mask) >= 3 ? "corner-inner" : "corner-outer",
                        current, FlipCorner(mask), 1f, moduleIndex++);
                }
            }
        }

        private static List<Vector2> SimplifyOrthogonalPath(List<Vector2> path)
        {
            var result = new List<Vector2>(path);
            var changed = true;
            while (changed && result.Count > 3)
            {
                changed = false;
                for (var i = 0; i < result.Count; i++)
                {
                    var previous = result[(i - 1 + result.Count) % result.Count];
                    var current = result[i];
                    var next = result[(i + 1) % result.Count];
                    var incoming = current - previous;
                    var outgoing = next - current;
                    var cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
                    if (Mathf.Abs(cross) > .001f || Vector2.Dot(incoming, outgoing) <= 0f) continue;
                    result.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
            return result;
        }

        private static List<List<Vector2>> BuildContourPaths(IReadOnlyList<DungeonContourSegment> segments)
        {
            var atPoint = new Dictionary<Vector2Int, List<int>>();
            for (var i = 0; i < segments.Count; i++)
            {
                AddSegmentAtPoint(atPoint, Quantize(segments[i].From), i);
                AddSegmentAtPoint(atPoint, Quantize(segments[i].To), i);
            }
            var visited = new bool[segments.Count];
            var result = new List<List<Vector2>>();
            for (var start = 0; start < segments.Count; start++)
            {
                if (visited[start]) continue;
                var path = new List<Vector2> { segments[start].From };
                var startPoint = Quantize(segments[start].From);
                var currentPoint = startPoint;
                var currentSegment = start;
                while (currentSegment >= 0 && !visited[currentSegment])
                {
                    visited[currentSegment] = true;
                    var segment = segments[currentSegment];
                    var fromKey = Quantize(segment.From);
                    var nextPoint = fromKey == currentPoint ? segment.To : segment.From;
                    var nextKey = Quantize(nextPoint);
                    path.Add(nextPoint);
                    if (nextKey == startPoint) break;
                    currentPoint = nextKey;
                    currentSegment = -1;
                    if (!atPoint.TryGetValue(nextKey, out var neighbours)) break;
                    foreach (var neighbour in neighbours)
                    {
                        if (visited[neighbour]) continue;
                        currentSegment = neighbour;
                        break;
                    }
                }
                if (path.Count > 1 && Quantize(path[path.Count - 1]) == Quantize(path[0])) path.RemoveAt(path.Count - 1);
                if (path.Count >= 3) result.Add(path);
            }
            return result;
        }

        private static void AddSegmentAtPoint(Dictionary<Vector2Int, List<int>> lookup, Vector2Int point, int index)
        {
            if (!lookup.TryGetValue(point, out var indices)) lookup[point] = indices = new List<int>(2);
            indices.Add(index);
        }

        private static int ClosestContourMask(IReadOnlyList<DungeonContourSegment> segments, Vector2 point)
        {
            var best = float.MaxValue;
            var mask = 1;
            foreach (var segment in segments)
            {
                var distance = ((segment.From + segment.To) * .5f - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                mask = segment.Mask;
            }
            return mask;
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
                var role = feature.Kind == DungeonArchitectureKind.OpenArch ? "arch-open" : "stairs";
                var scale = feature.Kind == DungeonArchitectureKind.OpenArch ? .92f : .86f;
                CreateArchitectureModule(role, feature.Position, feature.FlipX, scale, featureIndex++);
            }
        }

        private void CreateArchitectureModule(string role, Vector2 anchor, bool flipX, float scale, int index)
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

            visual.AddComponent<IsoVisual>().Initialize(owner.transform, 0f, 1040);
        }

        private static int CountMaskBits(int mask)
        {
            var bits = 0;
            for (var value = mask; value != 0; value >>= 1) bits += value & 1;
            return bits;
        }

        private static bool FlipCorner(int mask) =>
            mask == 2 || mask == 4 || mask == 10 || mask == 11 || mask == 13;

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
