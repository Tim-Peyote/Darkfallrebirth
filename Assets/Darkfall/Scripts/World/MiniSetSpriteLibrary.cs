using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Authored, floorless sprites for semantic mini-sets.</summary>
    public static class MiniSetSpriteLibrary
    {
        private const string Root = "Sprites/Environment/MiniSets/ashen-catacombs/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(DungeonMiniSetKind kind)
        {
            var name = kind switch
            {
                DungeonMiniSetKind.StatueNiche => "statue-niche",
                DungeonMiniSetKind.SideChapel => "side-chapel",
                DungeonMiniSetKind.Colonnade => "colonnade",
                DungeonMiniSetKind.RuinedCorner => "ruined-corner",
                DungeonMiniSetKind.RubbleBlock => "rubble-block",
                DungeonMiniSetKind.CollapsedWall => "ruined-corner",
                // The runtime assembles campfires from the canonical static brazier body and a
                // separate flame renderer. Do not return the retired full-body animation here.
                DungeonMiniSetKind.Campfire => "campfire-unlit",
                DungeonMiniSetKind.Altar => "altar",
                _ => null
            };
            return string.IsNullOrEmpty(name) ? null : Load(name);
        }

        private static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Root + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .035f), 180f, 0, SpriteMeshType.Tight);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
