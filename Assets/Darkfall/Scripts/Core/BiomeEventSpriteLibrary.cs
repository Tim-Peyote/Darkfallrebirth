using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public static class BiomeEventSpriteLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string biome, int index)
        {
            var key = biome + "/event-" + Mathf.Clamp(index, 0, 5).ToString("00");
            if (Cache.TryGetValue(key, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>("Sprites/Environment/Events/" + key);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .12f), 300f, 0, SpriteMeshType.Tight);
            Cache[key] = sprite;
            return sprite;
        }
    }
}
