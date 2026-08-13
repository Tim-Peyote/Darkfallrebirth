using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public static class TreasureChestSpriteLibrary
    {
        private const string Root = "Sprites/Interactables/TreasureChest/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Closed => Load("closed");
        public static Sprite Opening(int frame) => Load(frame <= 0 ? "opening-01" : "opening-02");
        public static Sprite Open => Load("open");

        private static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(Root + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Every state uses the same 1024 px canvas, pivot and full rectangular mesh. A tight
            // mesh changes its geometry when the lid opens and caused the opened chest to vanish
            // or jump on some graphics APIs/build targets.
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .072f), 500f, 0, SpriteMeshType.FullRect);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
