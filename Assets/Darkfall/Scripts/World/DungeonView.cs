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
            BuildRaisedFloorCaps(data);
            BuildElevationRisers(data);
        }

        private void BuildRaisedFloorCaps(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var seamVertices = new List<Vector3>();
            var seamTriangles = new List<int>();
            var seamColors = new List<Color>();
            var seamUvs = new List<Vector2>();
            const float uvScale = .08f;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var level = data.ElevationLevel(x, y);
                if (level <= 0) continue;
                var height = level * DungeonData.ElevationStepHeight;
                var index = vertices.Count;
                var logical = new[]
                {
                    new Vector2(x, y), new Vector2(x + 1, y),
                    new Vector2(x + 1, y + 1), new Vector2(x, y + 1)
                };
                foreach (var point in logical)
                {
                    var projected = IsoWorld.Project(point);
                    projected.y += height;
                    vertices.Add(projected);
                    uvs.Add(point * uvScale);
                    colors.Add(profile.FloorTint * RandomTint(x, y));
                }
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
                triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);

                // Even an away-facing ledge needs a graphic seam. Without it, an inaccessible
                // upper floor reads as ordinary floor behind a wall because its vertical face is
                // correctly hidden by the cap in this projection.
                if (data.ElevationLevel(x - 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x, y + .5f), true))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x, y), new Vector2(x, y + 1), Vector2.right, height);
                if (data.ElevationLevel(x + 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + 1, y + .5f), true))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x + 1, y + 1), new Vector2(x + 1, y), Vector2.left, height);
                if (data.ElevationLevel(x, y - 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y), false))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x + 1, y), new Vector2(x, y), Vector2.up, height);
                if (data.ElevationLevel(x, y + 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y + 1), false))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x, y + 1), new Vector2(x + 1, y + 1), Vector2.down, height);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Raised Platform Upper Floors", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.FloorTexture), 972);
            var seamMesh = MakeMesh("Raised Platform Cap Seams", seamVertices, seamTriangles, seamColors, seamUvs);
            var seamShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var seamMaterial = new Material(seamShader) { color = Color.white };
            materials.Add(seamMaterial);
            CreateLayer(seamMesh.name, seamMesh, seamMaterial, 973);
        }

        private static void AddPlatformCapSeam(List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, Vector2 from, Vector2 to, Vector2 inward, float height)
        {
            var a = IsoWorld.Project(from) + Vector2.up * height;
            var b = IsoWorld.Project(to) + Vector2.up * height;
            var c = IsoWorld.Project(to + inward * .13f) + Vector2.up * height;
            var d = IsoWorld.Project(from + inward * .13f) + Vector2.up * height;
            AddScreenQuad(vertices, triangles, colors, uvs, a, b, c, d,
                new Color(.035f, .026f, .02f, .88f), from, to);
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
                // Only screen-facing (+X/+Y) platform faces are visible in this projection. The
                // -X/-Y faces are behind the upper floor; rendering them in a 2D pipeline makes
                // them appear through that floor as interior walls.
                if (data.ElevationLevel(x + 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + 1, y + .5f), true))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y), new Vector2(x + 1, y + 1), height, color * .84f);
                if (data.ElevationLevel(x, y + 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y + 1), false))
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y + 1), new Vector2(x, y + 1), height, color);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Raised Platform Fascias", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.WallTexture), 970);
            var texture = Resources.Load<Texture2D>(profile.WallTexture);
            var readabilityShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var readability = new Material(readabilityShader)
            {
                color = new Color(.9f, .9f, .9f, .52f),
                mainTexture = texture
            };
            materials.Add(readability);
            CreateLayer("Raised Platform Readability", mesh, readability, 971);
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
            var plinth = height * .18f;
            var frieze = height * .78f;

            // A platform edge is a vertical architectural tile, not a stretched shadow quad.
            // Use the full biome wall texture range and explicit masonry bands so height remains
            // readable even outside local lights.
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                0f, plinth, color * .62f, from, to, 0f, .18f);
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                plinth, frieze, color * .94f, from, to, .18f, .76f);
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                frieze, height, color * 1.22f, from, to, .76f, 1f);

            var upperFrom = lowerFrom + Vector2.up * height;
            var upperTo = lowerTo + Vector2.up * height;
            var edge = upperTo - upperFrom;
            var lip = Vector2.Perpendicular(edge.normalized) * .045f;
            if (lip.y < 0f) lip = -lip;
            AddScreenQuad(vertices, triangles, colors, uvs, upperFrom, upperTo,
                upperTo + lip, upperFrom + lip, color * 1.12f, from, to);
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
                    // Accent files are authored as front-facing facade minisets. They may replace
                    // only a compatible horizontal wall slot; using them on both axes produced
                    // the transverse panels seen in the biome audit.
                    if (!span.Vertical && length >= 8 && section >= 2 && section <= length - 3 &&
                        (section + edgeHash) % 8 == 3)
                    {
                        var accent = (edgeHash + section) % 3;
                        role = accent == 0 ? "wall-broken" : accent == 1 ? "wall-niche" : "arcade";
                    }
                    var flip = ArchitectureSpriteLibrary.FlipForAxis(profile.Id, role, span.Vertical);
                    CreateArchitectureModule(role, anchor, flip, .985f, moduleIndex++,
                        data.BoundaryHeight(anchor));
                }
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

        private static bool FeatureReplacesWallModule(DungeonData data, Vector2 point)
        {
            foreach (var feature in data.Architecture)
            {
                // Floor-to-floor thresholds are already open in the contour. Removing nearby
                // side-wall modules again widens them into accidental rectangular holes.
                if (feature.Kind == DungeonArchitectureKind.OpenGate) continue;
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
                // Ordinary circulation is an empty threshold. The arcade artwork is a small
                // double lancet wall module and must never masquerade as a walk-through gate.
                if (feature.Kind == DungeonArchitectureKind.OpenGate)
                    continue;
                var normal = feature.Vertical ? Vector2.right : Vector2.up;
                var negativeLevel = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x - normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y - normal.y * .25f));
                var positiveLevel = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x + normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y + normal.y * .25f));
                var lowerDirection = negativeLevel < positiveLevel ? -normal : normal;
                // The sprite pivot is at the foot of its first step. Place that foot on the lower
                // half of the traversal ramp so the upper landing terminates at the raised floor.
                var stairAnchor = feature.Position + lowerDirection * .64f;
                // Width 2/3 thresholds share the same art. Stretch only screen X to close the
                // side voids while retaining the authored vertical rise and landing height.
                var stairHorizontalScale = feature.Width == 2 ? 1.28f : 1.48f;
                CreateArchitectureModule("stairs", stairAnchor, feature.Vertical, 1.03f,
                    featureIndex++, 0f, stairHorizontalScale);
            }
        }

        private void CreateArchitectureModule(string role, Vector2 anchor, bool flipX, float scale, int index,
            float elevation = 0f, float horizontalScale = -1f)
        {
            var sprite = ArchitectureSpriteLibrary.Module(profile.Id, role);
            if (sprite == null) return;

            var owner = new GameObject($"{role} · {index}");
            owner.transform.SetParent(architectureDecor, false);
            owner.transform.position = anchor;

            var visual = new GameObject("Projected Architecture");
            visual.transform.SetParent(owner.transform, false);
            ArchitectureSpriteLibrary.Placement(profile.Id, role, sprite, out var moduleScale,
                out var moduleOffset);
            visual.transform.localPosition = moduleOffset * scale;
            visual.transform.localScale = new Vector3(
                (horizontalScale > 0f ? horizontalScale : scale) * moduleScale.x,
                scale * moduleScale.y, 1f);

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

            // Architecture stays on one depth system so the stair remains joined to both platform
            // lips. Traversing actors receive their temporary stair-depth boost in IsoVisual.
            visual.AddComponent<IsoVisual>().Initialize(owner.transform, elevation, 1002, false);
        }

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
                if (roomIndex == 0)
                {
                    BuildArrivalDecor(data, bounds, hash);
                    continue;
                }
                if (roomIndex % profile.LightEveryRooms == 1)
                {
                    var lightProp = profile.Id == "ashen-catacombs" ? 2 : (roomIndex % 2 == 0 ? 0 : 8);
                    CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f),
                        profile.Id == "ashen-catacombs" ? 1f : .72f, "Biome Light", false, lightDecor);
                }

                // Theme rooms are authored compositions, not circular noise. Most rooms remain
                // quiet; selected rooms reserve either a wall bay, a corner, or (rarely) a formal
                // central landmark. The profile only supplies biome art, so the spatial grammar is
                // identical for all five biomes.
                if (roomIndex >= data.Rooms.Count - 1 || bounds.width < 7 || bounds.height < 7 ||
                    hash % 100 >= Mathf.RoundToInt(46f * profile.DecorDensity))
                    continue;

                var primary = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
                if (bounds.width >= 13 && bounds.height >= 11 && (hash / 31) % 7 == 0)
                    BuildCentralTheme(data, bounds, hash, primary);
                else if ((hash / 17 & 1) == 0)
                    BuildWallTheme(data, bounds, hash, primary);
                else
                    BuildCornerTheme(data, bounds, hash, primary);
            }
        }

        private struct ThemeBay
        {
            public Vector2 Anchor;
            public Vector2 Tangent;
            public Vector2 Inward;

            public ThemeBay(Vector2 anchor, Vector2 tangent, Vector2 inward)
            {
                Anchor = anchor;
                Tangent = tangent;
                Inward = inward;
            }
        }

        private void BuildWallTheme(DungeonData data, RectInt bounds, int hash, int primary)
        {
            if (!TryChooseWallBay(data, bounds, hash, out var bay)) return;
            if (!CreateProp(data, primary, bay.Anchor, .82f + hash % 3 * .06f,
                    "Wall Theme · Primary", true, structuralDecor)) return;

            // A readable triptych: two companions follow the wall and an offering sits in front.
            // Details are subordinate to the primary and are never spawned if it failed.
            PlaceThemeDetail(data, bounds, hash, bay.Anchor - bay.Tangent * 1.35f, 0);
            PlaceThemeDetail(data, bounds, hash, bay.Anchor + bay.Tangent * 1.35f, 1);
            if ((hash / 13) % 3 != 0)
                PlaceThemeDetail(data, bounds, hash, bay.Anchor + bay.Inward * 1.05f, 2);
        }

        private void BuildCornerTheme(DungeonData data, RectInt bounds, int hash, int primary)
        {
            var corners = new[]
            {
                new ThemeBay(new Vector2(bounds.xMin + 1.35f, bounds.yMin + 1.35f), Vector2.right, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMax - 1.35f, bounds.yMin + 1.35f), Vector2.left, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMax - 1.35f, bounds.yMax - 1.35f), Vector2.left, Vector2.down),
                new ThemeBay(new Vector2(bounds.xMin + 1.35f, bounds.yMax - 1.35f), Vector2.right, Vector2.down)
            };
            ThemeBay chosen = default;
            var bestScore = float.MinValue;
            for (var offset = 0; offset < corners.Length; offset++)
            {
                var candidate = corners[(hash + offset) % corners.Length];
                var score = ThemeClearance(data, bounds, candidate.Anchor, true);
                if (score <= bestScore) continue;
                bestScore = score;
                chosen = candidate;
            }
            if (bestScore < 1.85f || !CreateProp(data, primary, chosen.Anchor, .78f,
                    "Corner Theme · Primary", true, structuralDecor)) return;
            PlaceThemeDetail(data, bounds, hash, chosen.Anchor + chosen.Tangent * 1.2f, 0);
            PlaceThemeDetail(data, bounds, hash, chosen.Anchor + chosen.Inward * 1.2f, 1);
        }

        private void BuildCentralTheme(DungeonData data, RectInt bounds, int hash, int primary)
        {
            var center = (Vector2)bounds.center;
            if (ThemeClearance(data, bounds, center, false) < 2.5f ||
                !CreateProp(data, primary, center, .9f, "Central Theme · Primary", true, structuralDecor))
                return;

            // Deliberate axial arrangement, leaving broad diagonal combat lanes around it.
            var axis = (hash & 1) == 0 ? Vector2.right : Vector2.up;
            PlaceThemeDetail(data, bounds, hash, center - axis * 1.65f, 0);
            PlaceThemeDetail(data, bounds, hash, center + axis * 1.65f, 1);
        }

        private bool TryChooseWallBay(DungeonData data, RectInt bounds, int hash, out ThemeBay chosen)
        {
            var candidates = new[]
            {
                new ThemeBay(new Vector2(bounds.center.x, bounds.yMax - 1.25f), Vector2.right, Vector2.down),
                new ThemeBay(new Vector2(bounds.xMax - 1.25f, bounds.center.y), Vector2.up, Vector2.left),
                new ThemeBay(new Vector2(bounds.center.x, bounds.yMin + 1.25f), Vector2.right, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMin + 1.25f, bounds.center.y), Vector2.up, Vector2.right)
            };
            chosen = default;
            var bestScore = float.MinValue;
            for (var offset = 0; offset < candidates.Length; offset++)
            {
                var candidate = candidates[(hash + offset) % candidates.Length];
                var score = ThemeClearance(data, bounds, candidate.Anchor, true);
                score = Mathf.Min(score, ThemeClearance(data, bounds,
                    candidate.Anchor - candidate.Tangent * 1.35f, true));
                score = Mathf.Min(score, ThemeClearance(data, bounds,
                    candidate.Anchor + candidate.Tangent * 1.35f, true));
                if (score <= bestScore) continue;
                bestScore = score;
                chosen = candidate;
            }
            return bestScore >= 1.85f;
        }

        private float ThemeClearance(DungeonData data, RectInt bounds, Vector2 position, bool keepCenterClear)
        {
            if (position.x < bounds.xMin + .8f || position.x > bounds.xMax - .8f ||
                position.y < bounds.yMin + .8f || position.y > bounds.yMax - .8f ||
                !data.IsFloor(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return -1f;
            if (keepCenterClear && Vector2.Distance(position, bounds.center) < 2.15f) return -1f;
            var clearance = Mathf.Min(Vector2.Distance(position, data.CellCenter(data.StartCell)),
                Vector2.Distance(position, data.CellCenter(data.ExitCell)));
            foreach (var feature in data.Architecture)
                clearance = Mathf.Min(clearance, Vector2.Distance(position, feature.Position));
            return clearance;
        }

        private void PlaceThemeDetail(DungeonData data, RectInt bounds, int hash, Vector2 position, int member)
        {
            if (ThemeClearance(data, bounds, position, false) < 1.65f) return;
            var propIndex = profile.ClutterProps[(hash / (member + 3) + member * 5) % profile.ClutterProps.Length];
            CreateProp(data, propIndex, position, .48f + member % 2 * .08f,
                "Theme Detail", false, clutterDecor);
        }

        private void BuildArrivalDecor(DungeonData data, RectInt bounds, int hash)
        {
            // The arrival room is a deliberately quiet miniset: recognizable biome dressing,
            // enough light to read the only door, and no blocking altar or random clutter in the
            // player's first steps. Enemy spawning already excludes room zero.
            var center = (Vector2)bounds.center;
            var doorPosition = center + Vector2.down;
            foreach (var feature in data.Architecture)
                if (feature.Kind == DungeonArchitectureKind.ClosedDoor &&
                    feature.Position.x >= bounds.xMin - .1f && feature.Position.x <= bounds.xMax + .1f &&
                    feature.Position.y >= bounds.yMin - .1f && feature.Position.y <= bounds.yMax + .1f)
                {
                    doorPosition = feature.Position;
                    break;
                }
            var doorDelta = doorPosition - center;
            var shrinePosition = center;
            if (Mathf.Abs(doorDelta.x) >= Mathf.Abs(doorDelta.y))
                shrinePosition.x = doorDelta.x >= 0f ? bounds.xMin + 1.15f : bounds.xMax - 1.15f;
            else
                shrinePosition.y = doorDelta.y >= 0f ? bounds.yMin + 1.15f : bounds.yMax - 1.15f;
            var shrineIndex = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
            CreateProp(data, shrineIndex, shrinePosition, .72f,
                "Arrival Shrine · " + profile.Id, false, structuralDecor);

            var lightProp = profile.Id == "ashen-catacombs" ? 2 : ((hash & 1) == 0 ? 0 : 8);
            CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.05f, bounds.yMin + 1.05f),
                profile.Id == "ashen-catacombs" ? .76f : .62f, "Arrival Vigil", false, lightDecor);

            var offeringIndex = profile.ClutterProps[(hash / 11) % profile.ClutterProps.Length];
            CreateProp(data, offeringIndex, new Vector2(bounds.xMax - 1.05f, bounds.yMin + 1.05f), .5f,
                "Arrival Offering", false, clutterDecor);
        }

        private bool CreateProp(DungeonData data, int index, Vector2 position, float scale, string objectName, bool blocks,
            Transform group)
        {
            if (!data.IsFloor(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return false;
            foreach (var feature in data.Architecture)
                if (Vector2.Distance(position, feature.Position) < 1.6f) return false;
            if (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 1.25f ||
                Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 1.25f) return false;
            if (blocks && (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 2f ||
                           Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 2f))
                blocks = false;
            if (blocks && !data.TryAddObstaclePreservingRoutes(position)) return false;
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
            return true;
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
