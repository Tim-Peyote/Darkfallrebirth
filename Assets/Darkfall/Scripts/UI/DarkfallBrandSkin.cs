using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.UI
{
    public static class DarkfallBrandSkin
    {
        private const string Root = "UI/BrandV3/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Sigil => Load("sigil");
        public static Sprite Divider => Load("divider");
        public static Sprite CornerLeft => Load("corner-left");
        public static Sprite CornerRight => Load("corner-right");
        public static Sprite Health => Load("health-icon");
        public static Sprite Shield => Load("shield-icon");
        public static Sprite Inventory => Load("inventory-icon");
        public static Sprite Pause => Load("pause-icon");

        private static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var texture = Resources.Load<Texture2D>(Root + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
