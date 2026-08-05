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
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static bool HasBiome(string biome) => Module(biome, "wall-left") != null;

        public static Sprite Module(string biome, string role)
        {
            var key = biome + "/" + role;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = Resources.Load<Texture2D>(Root + key + "-01");
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
