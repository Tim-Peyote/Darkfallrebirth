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
            BuildLayer(data, true, profile.FloorTint, 0, profile.FloorTexture);
            BuildLayer(data, false, profile.WallTint, .1f, profile.WallTexture);
            BuildWallEdges(data);
            BuildShadowCasters(data);
            var decorRoot = new GameObject("Decor · " + profile.Id).transform;
            decorRoot.SetParent(transform, false);
            structuralDecor = CreateGroup(decorRoot, "Structural");
            lightDecor = CreateGroup(decorRoot, "Light Sources");
            clutterDecor = CreateGroup(decorRoot, "Clutter");
            BuildDecor(data);
        }

        public void Clear()
        {
            for (var i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in materials) Destroy(material);
            meshes.Clear();
            materials.Clear();
        }

        private void BuildLayer(DungeonData data, bool floorLayer, Color color, float z, string texturePath)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var isFloor = data.IsFloor(x, y);
                if (floorLayer != isFloor) continue;
                if (!floorLayer && !TouchesFloor(data, x, y)) continue;
                AddQuad(vertices, triangles, colors, uvs, x, y, 1, 1, z, color * RandomTint(x, y));
            }

            var mesh = MakeMesh(floorLayer ? "Dungeon Floor" : "Dungeon Wall Tops", vertices, triangles, colors, uvs);
            var material = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            var texture = Resources.Load<Texture2D>(texturePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                material.mainTexture = texture;
            }
            materials.Add(material);
            CreateLayer(mesh.name, mesh, material, floorLayer ? -20 : -10);
        }

        private void BuildWallEdges(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var edgeColor = profile.ContactShadow;
            // Contact shadow only. The former .12 solid strip read as a UI border around rooms.
            const float thickness = .032f;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                if (!data.IsFloor(x, y)) continue;
                if (!data.IsFloor(x - 1, y)) AddQuad(vertices, triangles, colors, uvs, x, y, thickness, 1, -.02f, edgeColor);
                if (!data.IsFloor(x + 1, y)) AddQuad(vertices, triangles, colors, uvs, x + 1 - thickness, y, thickness, 1, -.02f, edgeColor);
                if (!data.IsFloor(x, y - 1)) AddQuad(vertices, triangles, colors, uvs, x, y, 1, thickness, -.02f, edgeColor);
                if (!data.IsFloor(x, y + 1)) AddQuad(vertices, triangles, colors, uvs, x, y + 1 - thickness, 1, thickness, -.02f, edgeColor);
            }
            var mesh = MakeMesh("Wall Edge Shadows", vertices, triangles, colors, uvs);
            var material = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            materials.Add(material);
            CreateLayer(mesh.name, mesh, material, -5);
        }

        private void BuildDecor(DungeonData data)
        {
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                if (roomIndex % profile.LightEveryRooms == 1)
                    CreateProp(data, 2, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f), 1f, "Brazier", false, lightDecor);

                var hash = ((bounds.x * 73856093) ^ (bounds.y * 19349663) ^ (roomIndex * 83492791)) & int.MaxValue;
                var first = profile.ClutterProps[hash % profile.ClutterProps.Length];
                CreateProp(data, first, new Vector2(bounds.xMax - 1.15f, bounds.yMin + 1.1f), .9f,
                    "Room Decor", first == 4, clutterDecor);
                if (bounds.width >= 11 && bounds.height >= 10 && roomIndex > 0)
                {
                    var second = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
                    CreateProp(data, second, new Vector2(bounds.xMax - 1.2f, bounds.yMax - 1.15f), .92f,
                        "Large Room Decor", true, structuralDecor);
                }
            }
        }

        private void CreateProp(DungeonData data, int index, Vector2 position, float scale, string objectName, bool blocks,
            Transform group)
        {
            var prop = new GameObject(objectName + " " + index);
            prop.transform.SetParent(group, false);
            prop.transform.position = position;
            prop.transform.localScale = Vector3.one * scale;
            var renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = EnvironmentSpriteAtlas.Prop(index);
            renderer.color = Color.white;
            renderer.sortingOrder = 7;
            DarkfallRenderMaterials.MakeLit(renderer);
            if (blocks)
            {
                data.AddObstacle(position);
                var caster = prop.AddComponent<ShadowCaster2D>();
                caster.castsShadows = true;
                caster.selfShadows = false;
                caster.alphaCutoff = .22f;
            }
            if (index == 2)
            {
                AddFlame(prop.transform, new Vector2(0, .24f), .56f, 9);
                data.AddLightSource(position + new Vector2(0, .22f), profile.FireTint, 5.8f, .16f);
            }
            else if (index == 8)
            {
                AddFlame(prop.transform, new Vector2(-.37f, .31f), .105f, 9);
                AddFlame(prop.transform, new Vector2(.19f, .29f), .10f, 9);
                AddFlame(prop.transform, new Vector2(.37f, .13f), .09f, 9);
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

        private void BuildShadowCasters(DungeonData data)
        {
            var root = new GameObject("Composite Dungeon Shadows");
            root.transform.SetParent(transform, false);
            root.AddComponent<CompositeShadowCaster2D>();
            var consumed = new bool[data.Width, data.Height];

            for (var y = 0; y < data.Height; y++)
            for (var x = 0; x < data.Width; x++)
            {
                if (consumed[x, y] || !IsBoundaryWall(data, x, y)) continue;
                var width = 1;
                while (x + width < data.Width && !consumed[x + width, y] && IsBoundaryWall(data, x + width, y)) width++;
                var height = 1;
                var canGrow = true;
                while (y + height < data.Height && canGrow)
                {
                    for (var offset = 0; offset < width; offset++)
                        if (consumed[x + offset, y + height] || !IsBoundaryWall(data, x + offset, y + height))
                        {
                            canGrow = false;
                            break;
                        }
                    if (canGrow) height++;
                }

                for (var ox = 0; ox < width; ox++)
                for (var oy = 0; oy < height; oy++) consumed[x + ox, y + oy] = true;
                CreateShadowRectangle(root.transform, x, y, width, height);
            }
        }

        private static bool IsBoundaryWall(DungeonData data, int x, int y)
        {
            return !data.IsFloor(x, y) && TouchesFloor(data, x, y);
        }

        private static void CreateShadowRectangle(Transform parent, int x, int y, int width, int height)
        {
            var shadow = new GameObject("Wall Shadow Caster");
            shadow.transform.SetParent(parent, false);
            shadow.transform.position = new Vector3(x + width * .5f, y + height * .5f, 0);
            shadow.transform.localScale = new Vector3(width, height, 1);
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

        private static bool TouchesFloor(DungeonData data, int x, int y)
        {
            for (var ox = -1; ox <= 1; ox++)
            for (var oy = -1; oy <= 1; oy++)
                if (data.IsFloor(x + ox, y + oy)) return true;
            return false;
        }

        private static float RandomTint(int x, int y)
        {
            var hash = (x * 73856093) ^ (y * 19349663);
            return .90f + Mathf.Abs(hash % 13) / 100f;
        }

        private static void AddQuad(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            float x, float y, float width, float height, float z, Color color)
        {
            var index = v.Count;
            v.Add(new Vector3(x, y, z));
            v.Add(new Vector3(x + width, y, z));
            v.Add(new Vector3(x + width, y + height, z));
            v.Add(new Vector3(x, y + height, z));
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            const float scale = .08f;
            uv.Add(new Vector2(x * scale, y * scale));
            uv.Add(new Vector2((x + width) * scale, y * scale));
            uv.Add(new Vector2((x + width) * scale, (y + height) * scale));
            uv.Add(new Vector2(x * scale, (y + height) * scale));
        }
    }
}
