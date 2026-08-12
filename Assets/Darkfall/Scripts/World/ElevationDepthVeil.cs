using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>
    /// Gives elevation a stable visual hierarchy. Floors below the hero recede into a neutral,
    /// softly mottled veil; the current floor and stair landings remain clean and actionable.
    /// </summary>
    public sealed class ElevationDepthVeil : MonoBehaviour
    {
        private sealed class LevelLayer
        {
            public int Level;
            public Material Material;
            public float Alpha;
        }

        private readonly List<LevelLayer> layers = new List<LevelLayer>();
        private DungeonData dungeon;
        private Transform player;
        private Texture2D fogTexture;

        public void Initialize(DungeonData data, Transform target)
        {
            dungeon = data;
            player = target;
            fogTexture = CreateFogTexture();

            var levels = new HashSet<int>();
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
                if (data.IsFloor(x, y)) levels.Add(data.ElevationLevel(x, y));

            foreach (var level in levels) BuildLevel(level);
            Refresh(true);
        }

        private void LateUpdate() => Refresh(false);

        private void Refresh(bool snap)
        {
            if (dungeon == null || player == null) return;
            var playerHeight = dungeon.SurfaceHeight(player.position);
            foreach (var layer in layers)
            {
                var levelHeight = layer.Level * DungeonData.ElevationStepHeight;
                var separation = Mathf.Clamp01((playerHeight - levelHeight) /
                                               DungeonData.ElevationStepHeight);
                var target = separation * .62f;
                layer.Alpha = snap ? target : Mathf.MoveTowards(layer.Alpha, target,
                    Time.unscaledDeltaTime * 1.55f);
                var color = layer.Material.color;
                color.a = layer.Alpha;
                layer.Material.color = color;
            }
        }

        private void BuildLevel(int level)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
            {
                if (!dungeon.IsFloor(x, y) || dungeon.ElevationLevel(x, y) != level) continue;
                var index = vertices.Count;
                var logical = new[]
                {
                    new Vector2(x, y), new Vector2(x + 1, y),
                    new Vector2(x + 1, y + 1), new Vector2(x, y + 1)
                };
                foreach (var point in logical)
                {
                    var projected = IsoWorld.Project(point);
                    projected.y += level * DungeonData.ElevationStepHeight + .025f;
                    vertices.Add(projected);
                    colors.Add(Color.white);
                    uvs.Add(point * .105f);
                }
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            if (vertices.Count == 0) return;

            var mesh = new Mesh { name = $"Elevation Depth Veil · {level}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            var owner = new GameObject(mesh.name);
            owner.transform.SetParent(transform, false);
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var material = new Material(shader)
            {
                name = $"Elevation Depth Fog · {level}",
                mainTexture = fogTexture,
                color = new Color(.105f, .10f, .092f, 0f)
            };
            renderer.sharedMaterial = material;
            // Above all floor surfaces, below facades, actors and interaction objects.
            renderer.sortingOrder = 965;
            layers.Add(new LevelLayer { Level = level, Material = material });
        }

        private static Texture2D CreateFogTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Elevation Depth Fog",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var broad = Mathf.PerlinNoise(x * .025f + 4.7f, y * .025f + 12.3f);
                var detail = Mathf.PerlinNoise(x * .073f + 21.1f, y * .061f + 7.9f);
                var alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(118f, 226f, broad * .76f + detail * .24f));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            foreach (var layer in layers)
                if (layer.Material != null) Destroy(layer.Material);
            if (fogTexture != null) Destroy(fogTexture);
        }
    }
}
