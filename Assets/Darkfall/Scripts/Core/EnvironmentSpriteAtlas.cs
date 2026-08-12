using UnityEngine;
using System.Collections.Generic;

namespace Darkfall.Core
{
    public static class EnvironmentSpriteAtlas
    {
        private const int Columns = 4;
        private const int Rows = 3;
        private static readonly Sprite[] Props = new Sprite[Columns * Rows];
        private static readonly Sprite[] Flames = new Sprite[4];
        private static Texture2D texture;
        private static Texture2D flameTexture;
        private static readonly Vector2 FlameCanvas = new Vector2(256f, 341f);
        private static readonly Vector2 FlamePivotPixels = new Vector2(128f, 61.38f);
        private static readonly Dictionary<string, Sprite[]> BiomeProps = new Dictionary<string, Sprite[]>();

        public static Sprite Prop(string biome, int index)
        {
            var atlasName = biome == "ember-vaults" ? "ember" : biome == "drowned-crypt" ? "drowned" :
                biome == "charnel-gardens" ? "charnel" : biome == "obsidian-sanctum" ? "obsidian" : null;
            if (atlasName == null) return Prop(index);
            if (!BiomeProps.TryGetValue(atlasName, out var sprites))
            {
                sprites = new Sprite[Columns * Rows];
                for (var i = 0; i < sprites.Length; i++)
                {
                    var individual = Resources.Load<Texture2D>(
                        $"Sprites/Environment/Biomes/{atlasName}/decor-{i:00}");
                    if (individual == null) continue;
                    individual.filterMode = FilterMode.Bilinear;
                    individual.wrapMode = TextureWrapMode.Clamp;
                    sprites[i] = Sprite.Create(individual,
                        new Rect(0, 0, individual.width, individual.height), new Vector2(.5f, .18f), 300f,
                        0, SpriteMeshType.Tight);
                }
                // Keep the original source sheet as a compatibility fallback while every caller
                // moves to replaceable one-file modules. Runtime art never depends on re-slicing it.
                var atlas = Resources.Load<Texture2D>("Sprites/Environment/Biomes/" + atlasName + "-decor");
                if (atlas != null)
                {
                    atlas.filterMode = FilterMode.Bilinear;
                    atlas.wrapMode = TextureWrapMode.Clamp;
                    var width = atlas.width / (float)Columns;
                    var height = atlas.height / (float)Rows;
                    for (var row = 0; row < Rows; row++)
                    for (var column = 0; column < Columns; column++)
                    {
                        var cellIndex = row * Columns + column;
                        if (sprites[cellIndex] != null) continue;
                        var rect = new Rect(column * width, atlas.height - (row + 1) * height, width, height);
                        sprites[cellIndex] = Sprite.Create(atlas, rect, new Vector2(.5f, .18f), 300f,
                            0, SpriteMeshType.Tight);
                    }
                }
                BiomeProps[atlasName] = sprites;
            }
            return sprites[Mathf.Clamp(index, 0, sprites.Length - 1)] ?? Prop(index);
        }

        public static Sprite Prop(int index)
        {
            index = Mathf.Clamp(index, 0, Props.Length - 1);
            if (Props[index] != null) return Props[index];
            var individual = Resources.Load<Texture2D>("Sprites/Environment/Props/prop-" + index);
            if (individual != null)
            {
                individual.filterMode = FilterMode.Bilinear;
                individual.wrapMode = TextureWrapMode.Clamp;
                Props[index] = Sprite.Create(individual, new Rect(0, 0, individual.width, individual.height),
                    new Vector2(.5f, .24f), 300f, 0, SpriteMeshType.Tight);
                return Props[index];
            }
            texture ??= Resources.Load<Texture2D>("Sprites/Environment/dungeon-props-v2");
            if (texture == null) return RuntimeAssets.Square;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var cellWidth = texture.width / (float)Columns;
            var cellHeight = texture.height / (float)Rows;
            var row = index / Columns;
            var column = index % Columns;
            var rect = new Rect(column * cellWidth, texture.height - (row + 1) * cellHeight, cellWidth, cellHeight);
            Props[index] = Sprite.Create(texture, rect, new Vector2(.5f, .24f), 300f, 0, SpriteMeshType.FullRect);
            return Props[index];
        }

        public static Sprite Flame(int frame)
        {
            frame = Mathf.Abs(frame) % Flames.Length;
            if (Flames[frame] != null) return Flames[frame];
            var individual = Resources.Load<Texture2D>("Sprites/Environment/Flames/flame-" + frame);
            if (individual != null)
            {
                individual.filterMode = FilterMode.Bilinear;
                individual.wrapMode = TextureWrapMode.Clamp;
                // All frames share an authored 256x341 canvas and the exact same pixel pivot.
                // FullRect prevents Unity from rebuilding a different tight mesh for every flame,
                // which made the apparent base and size jump during animation.
                var pivot = new Vector2(FlamePivotPixels.x / FlameCanvas.x,
                    FlamePivotPixels.y / FlameCanvas.y);
                Flames[frame] = Sprite.Create(individual, new Rect(0, 0, FlameCanvas.x, FlameCanvas.y),
                    pivot, 360f, 0, SpriteMeshType.FullRect);
                return Flames[frame];
            }
            flameTexture ??= Resources.Load<Texture2D>("Sprites/Environment/fire-flame-4x1-v2");
            if (flameTexture == null) return RuntimeAssets.Square;
            flameTexture.filterMode = FilterMode.Bilinear;
            flameTexture.wrapMode = TextureWrapMode.Clamp;
            var cellWidth = flameTexture.width / (float)Flames.Length;
            var rect = new Rect(frame * cellWidth, 0, cellWidth, flameTexture.height);
            Flames[frame] = Sprite.Create(flameTexture, rect, new Vector2(.5f, .18f), 360f, 0,
                SpriteMeshType.FullRect);
            return Flames[frame];
        }
    }
}
