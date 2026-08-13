using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Static fixture bodies. Fire is always a separate animated child.</summary>
    public static class FireFixtureSpriteLibrary
    {
        private const string Root = "Sprites/Environment/FireFixtures/ashen-catacombs/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite WallSconce => Load("wall-sconce-01", new Vector2(.5f, .15f), 230f);
        public static Sprite FloorCampfire => Load("floor-campfire-01", new Vector2(.5f, .14f), 230f);

        private static Sprite Load(string name, Vector2 pivot, float pixelsPerUnit)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Root + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                pivot, pixelsPerUnit, 0, SpriteMeshType.Tight);
            sprite.name = "Ashen fire fixture · " + name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
