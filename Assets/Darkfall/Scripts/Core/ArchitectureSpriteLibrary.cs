using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    /// <summary>
    /// Loads the authored architecture modules as independent sprites. The source artwork stays
    /// split by biome and role so a single wall, corner or feature can be replaced without
    /// rebuilding an atlas.
    /// </summary>
    public static class ArchitectureSpriteLibrary
    {
        private const string Root = "Sprites/Environment/Architecture/";
        private const float PixelsPerUnit = 230f;
        private const float ReferenceCanvas = 362f;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Rect> WallInk = new Dictionary<string, Rect>
        {
            { "ashen-catacombs/wall-left", new Rect(61, 33, 249, 296) },
            { "ashen-catacombs/wall-right", new Rect(45, 31, 248, 295) },
            { "charnel-gardens/wall-left", new Rect(56, 25, 257, 289) },
            { "charnel-gardens/wall-right", new Rect(56, 35, 257, 295) },
            { "drowned-crypt/wall-left", new Rect(83, 21, 281, 295) },
            { "drowned-crypt/wall-right", new Rect(84, 24, 254, 292) },
            { "ember-vaults/wall-left", new Rect(89, 19, 227, 272) },
            { "ember-vaults/wall-right", new Rect(64, 19, 230, 273) },
            { "obsidian-sanctum/wall-left", new Rect(99, 0, 263, 315) },
            { "obsidian-sanctum/wall-right", new Rect(71, 0, 263, 315) }
        };

        public static bool HasBiome(string biome) => Module(biome, "wall-left") != null;

        /// <summary>Normalizes how independently-authored right-wall files face the world axis.</summary>
        public static bool FlipForAxis(string biome, string role, bool vertical)
        {
            if (!vertical) return false;
            if (role != "wall-right") return true;
            // These two source sets encode wall-right with the opposite handedness. This is asset
            // metadata, not a different dungeon grammar.
            return biome == "ashen-catacombs" || biome == "drowned-crypt";
        }

        /// <summary>
        /// Every biome follows the same logical module grammar. Source files may use a different
        /// canvas size, so compensate for that canvas here instead of leaking biome-specific
        /// spacing constants into the dungeon builder.
        /// </summary>
        public static void Placement(string biome, string role, Sprite sprite, out Vector2 scale,
            out Vector2 offset)
        {
            scale = Vector2.one;
            offset = Vector2.zero;
            if (sprite == null || sprite.texture == null) return;

            var key = biome + "/" + role;
            var referenceKey = "ashen-catacombs/" + role;
            if (!WallInk.TryGetValue(key, out var ink) || !WallInk.TryGetValue(referenceKey, out var reference))
            {
                scale = new Vector2(ReferenceCanvas / sprite.texture.width,
                    ReferenceCanvas / sprite.texture.height);
                return;
            }

            scale = new Vector2(reference.width / ink.width, reference.height / ink.height);
            var referencePivot = new Vector2(ReferenceCanvas * .5f, ReferenceCanvas * .08f);
            var sourcePivot = new Vector2(sprite.texture.width * .5f, sprite.texture.height * .08f);
            var desired = new Vector2(reference.center.x - referencePivot.x,
                reference.yMin - referencePivot.y) / PixelsPerUnit;
            var actual = new Vector2(ink.center.x - sourcePivot.x,
                ink.yMin - sourcePivot.y) / PixelsPerUnit;
            offset = desired - Vector2.Scale(actual, scale);
        }

        public static Sprite Module(string biome, string role)
        {
            var key = biome + "/" + role;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var version = biome == "charnel-gardens" && (role == "wall-left" || role == "wall-right")
                ? "-02"
                : "-01";
            var texture = Resources.Load<Texture2D>(Root + key + version);
            if (texture == null)
            {
                Cache[key] = null;
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .08f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = biome + " · " + role;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
