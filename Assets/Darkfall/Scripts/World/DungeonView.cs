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
        private Transform structuralDecor;
        private Transform lightDecor;
        private Transform clutterDecor;

        public void Build(DungeonData data, int depth = 1)
        {
            Clear();
            profile = DungeonVisualProfile.ForDepth(depth);
            gameObject.name = "Dungeon · " + profile.Id;
            var contour = DungeonContour.Build(data);
            BuildContourFloor(contour);
            BuildContourWalls(contour, data.Width + data.Height);
            BuildContourShadows(contour);
            var decorRoot = new GameObject("Decor · " + profile.Id).transform;
            decorRoot.SetParent(transform, false);
            structuralDecor = CreateGroup(decorRoot, "Structural");
            lightDecor = CreateGroup(decorRoot, "Light Sources");
            clutterDecor = CreateGroup(decorRoot, "Clutter");
            BuildDecor(data);
            BuildArchitecturalAccents(data);
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

        private void BuildContourWalls(DungeonContour contour, int maximumDepth)
        {
            var wallTexture = Resources.Load<Texture2D>(profile.WallTexture);
            if (wallTexture != null)
            {
                wallTexture.wrapMode = TextureWrapMode.Repeat;
                wallTexture.filterMode = FilterMode.Bilinear;
            }

            var capVertices = new List<Vector3>();
            var capTriangles = new List<int>();
            var capColors = new List<Color>();
            var capUvs = new List<Vector2>();
            var shadowVertices = new List<Vector3>();
            var shadowTriangles = new List<int>();
            var shadowColors = new List<Color>();
            var shadowUvs = new List<Vector2>();

            foreach (var segment in contour.Segments)
            {
                AddWallCap(capVertices, capTriangles, capColors, capUvs, segment.From, segment.To);
                AddContactShadow(shadowVertices, shadowTriangles, shadowColors, shadowUvs, segment.From, segment.To);
            }

            var capMesh = MakeMesh("Continuous Wall Coping", capVertices, capTriangles, capColors, capUvs);
            var capMaterial = CreateTexturedMaterial(profile.WallTexture);
            CreateLayer(capMesh.name, capMesh, capMaterial, 900);
            var shadowMesh = MakeMesh("Continuous Wall Contact Shadow", shadowVertices, shadowTriangles,
                shadowColors, shadowUvs);
            var shadowMaterial = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            materials.Add(shadowMaterial);
            CreateLayer(shadowMesh.name, shadowMesh, shadowMaterial, -5);

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
                    var readableWall = Color.Lerp(profile.WallTint, Color.white, .17f);
                    AddWallFace(vertices, triangles, colors, uvs, segment.From, segment.To,
                        readableWall * shade);
                }
                if (vertices.Count == 0) continue;
                var mesh = MakeMesh("Contour Wall Facades · " + depth, vertices, triangles, colors, uvs);
                var material = new Material(DarkfallRenderMaterials.SpriteLit)
                    { color = Color.white, mainTexture = wallTexture };
                materials.Add(material);
                CreateLayer(mesh.name, mesh, material, 1040 + depth * IsoWorld.DepthPrecision);
            }
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

        private void AddWallCap(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to)
        {
            var a = IsoWorld.Project(from) + Vector2.up * IsoWorld.WallHeight;
            var b = IsoWorld.Project(to) + Vector2.up * IsoWorld.WallHeight;
            var direction = (b - a).normalized;
            var normal = Vector2.Perpendicular(direction) * .105f;
            AddScreenQuad(v, t, c, uv, a - normal, b - normal, b + normal, a + normal,
                Color.Lerp(profile.WallTint, Color.white, .2f), from, to);
        }

        private void BuildArchitecturalAccents(DungeonData data)
        {
            for (var roomIndex = 1; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                var anchors = new List<Vector2>();
                for (var x = bounds.xMin + 1; x < bounds.xMax - 1; x++)
                for (var y = bounds.yMin + 1; y < bounds.yMax - 1; y++)
                {
                    if (!data.IsFloor(x, y)) continue;
                    var walls = 0;
                    if (!data.IsFloor(x - 1, y)) walls++;
                    if (!data.IsFloor(x + 1, y)) walls++;
                    if (!data.IsFloor(x, y - 1)) walls++;
                    if (!data.IsFloor(x, y + 1)) walls++;
                    if (walls == 0) continue;
                    var point = new Vector2(x + .5f, y + .5f);
                    if (Vector2.Distance(point, data.CellCenter(data.StartCell)) < 2.2f ||
                        Vector2.Distance(point, data.CellCenter(data.ExitCell)) < 2.2f) continue;
                    anchors.Add(point);
                }
                if (anchors.Count == 0) continue;
                var hash = ((bounds.x * 92837111) ^ (bounds.y * 689287499) ^ (roomIndex * 283923481)) & int.MaxValue;
                var count = bounds.width * bounds.height >= 90 ? 2 : 1;
                for (var i = 0; i < count && anchors.Count > 0; i++)
                {
                    var anchorIndex = (hash + i * 17) % anchors.Count;
                    var propIndex = profile.StructuralProps[(hash / (i + 3) + i) % profile.StructuralProps.Length];
                    CreateProp(data, propIndex, anchors[anchorIndex], i == 0 ? .78f : .62f,
                        "Wall Architecture", false, structuralDecor);
                    anchors.RemoveAt(anchorIndex);
                }
            }
        }

        private void AddContactShadow(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to)
        {
            var a = IsoWorld.Project(from);
            var b = IsoWorld.Project(to);
            var normal = Vector2.Perpendicular((b - a).normalized) * .026f;
            AddScreenQuad(v, t, c, uv, a - normal, b - normal, b + normal, a + normal,
                profile.ContactShadow, from, to);
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

        private static void AddWallFace(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to, Color color)
        {
            var index = v.Count;
            var baseFrom = IsoWorld.Project(from);
            var baseTo = IsoWorld.Project(to);
            var topFrom = baseFrom + Vector2.up * IsoWorld.WallHeight;
            var topTo = baseTo + Vector2.up * IsoWorld.WallHeight;
            v.Add(new Vector3(baseFrom.x, baseFrom.y, 0));
            v.Add(new Vector3(baseTo.x, baseTo.y, 0));
            v.Add(new Vector3(topTo.x, topTo.y, 0));
            v.Add(new Vector3(topFrom.x, topFrom.y, 0));
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            var length = Vector2.Distance(from, to);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(length * .16f, 0));
            uv.Add(new Vector2(length * .16f, .22f)); uv.Add(new Vector2(0, .22f));
        }

        private void BuildDecor(DungeonData data)
        {
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                if (roomIndex % profile.LightEveryRooms == 1)
                {
                    var lightProp = profile.Id == "ashen-catacombs" ? 2 : (roomIndex % 2 == 0 ? 0 : 8);
                    CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f),
                        profile.Id == "ashen-catacombs" ? 1f : .72f, "Biome Light", false, lightDecor);
                }

                var hash = ((bounds.x * 73856093) ^ (bounds.y * 19349663) ^ (roomIndex * 83492791)) & int.MaxValue;
                var first = profile.ClutterProps[hash % profile.ClutterProps.Length];
                CreateProp(data, first, new Vector2(bounds.xMax - 1.15f, bounds.yMin + 1.1f), .9f,
                    "Room Decor", first == 4, clutterDecor);
                if (bounds.width >= 9 && bounds.height >= 8 && hash % 4 != 0)
                {
                    var secondary = profile.ClutterProps[(hash / 13 + 1) % profile.ClutterProps.Length];
                    CreateProp(data, secondary, new Vector2(bounds.xMin + 1.1f, bounds.yMin + 1.05f), .68f,
                        "Secondary Clutter", false, clutterDecor);
                }
                if (bounds.width >= 11 && bounds.height >= 10 && roomIndex > 0)
                {
                    var second = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
                    CreateProp(data, second, new Vector2(bounds.xMax - 1.2f, bounds.yMax - 1.15f), .92f,
                        "Large Room Decor", true, structuralDecor);
                }
                if (bounds.width >= 14 && bounds.height >= 12 && roomIndex > 0 && hash % 3 == 0)
                {
                    var accent = profile.StructuralProps[(hash / 19 + 1) % profile.StructuralProps.Length];
                    CreateProp(data, accent, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f), .72f,
                        "Wall Accent", false, structuralDecor);
                }

                // Layer several small, non-blocking props through the room instead of decorating
                // only its corners. Density scales with area and stays deterministic for the seed.
                var scatterCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 38f * profile.DecorDensity), 1, 6);
                var scatterAnchors = new[]
                {
                    new Vector2(.28f, .24f), new Vector2(.72f, .31f),
                    new Vector2(.36f, .72f), new Vector2(.68f, .76f)
                };
                for (var scatter = 0; scatter < scatterCount; scatter++)
                {
                    var anchor = scatterAnchors[(scatter + hash) % scatterAnchors.Length];
                    var position = new Vector2(
                        Mathf.Lerp(bounds.xMin + 1.15f, bounds.xMax - 1.15f, anchor.x),
                        Mathf.Lerp(bounds.yMin + 1.1f, bounds.yMax - 1.1f, anchor.y));
                    var propIndex = profile.ClutterProps[(hash / (scatter + 3) + scatter * 5) % profile.ClutterProps.Length];
                    CreateProp(data, propIndex, position, .46f + scatter % 2 * .12f,
                        "Ambient Clutter", false, clutterDecor);
                }
            }
        }

        private void CreateProp(DungeonData data, int index, Vector2 position, float scale, string objectName, bool blocks,
            Transform group)
        {
            if (!data.IsFloor(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return;
            if (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 1.25f ||
                Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 1.25f) return;
            if (blocks && (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 2f ||
                           Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 2f))
                blocks = false;
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
                data.AddObstacle(position);
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
                CreateShadowEdge(root.transform, segment.From, segment.To);
        }

        private static void CreateShadowEdge(Transform parent, Vector2 from, Vector2 to)
        {
            var a = IsoWorld.Project(from) + Vector2.up * (IsoWorld.WallHeight * .55f);
            var b = IsoWorld.Project(to) + Vector2.up * (IsoWorld.WallHeight * .55f);
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

    }
}
