using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>Authored door states with one canvas, pivot and transparent threshold.</summary>
    public static class DungeonDoorSpriteLibrary
    {
        private const string Root = "Sprites/Interactables/DungeonDoor/";
        // The arch is a landmark module: its crown must read above the ordinary wall cap while
        // the low side wings still overlap the neighbouring wall sockets.
        private const float PixelsPerUnit = 360f;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Closed => Load("closed");
        public static Sprite Opening(int frame) => Load(frame <= 0 ? "opening-01" : "opening-02");
        public static Sprite Open => Load("open");

        private static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Root + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // Every state is authored on the same square canvas. FullRect prevents Unity from
            // changing the renderer geometry as the transparent doorway grows between frames.
            // The authored canvases keep the masonry baseline at 18 px. Anchoring the sprite
            // there makes the plinth meet the dungeon floor instead of sinking the arch into it.
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, 18f / 512f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "Dungeon Door · " + name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
