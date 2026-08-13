using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public static class ItemSpriteAtlas
    {
        private static readonly HashSet<string> MappedIds = new HashSet<string>
        {
            "sword", "axe", "staff", "wand", "dagger", "crossbow", "shield",
            "grimoire", "orb", "robe", "leather", "plate", "helmet", "hood", "cap", "gloves", "belt", "boots", "amulet", "ring",
            "potion", "speed_potion", "strength_potion", "defense_potion", "regen_potion", "combo_potion",
            "purification_potion", "mystery_potion", "gold_pouch",
            "scroll_werewolf", "scroll_stone", "scroll_fire_explosion", "scroll_ice_storm", "scroll_lightning",
            "scroll_earthquake", "scroll_clone", "scroll_teleport", "scroll_invisibility", "scroll_time",
            "scroll_curse", "scroll_chaos", "scroll_fear", "scroll_smoke", "scroll_meteor", "scroll_barrier",
            "scroll_rage", "scroll_invulnerability", "scroll_vampirism", "mystery_scroll"
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string baseId)
        {
            if (!HasMapping(baseId)) return null;
            if (Cache.TryGetValue(baseId, out var cached)) return cached;
            var resourceId = baseId == "grimoire" ? "scroll_barrier" : baseId == "orb" ? "amulet" : baseId;
            var texture = Resources.Load<Texture2D>("Sprites/Items/Individual/" + resourceId);
            if (texture == null)
            {
                Cache[baseId] = null;
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), Mathf.Max(texture.width, texture.height), 0, SpriteMeshType.FullRect);
            sprite.name = baseId;
            Cache[baseId] = sprite;
            return sprite;
        }

        public static bool HasMapping(string baseId)
        {
            return !string.IsNullOrEmpty(baseId) && MappedIds.Contains(baseId);
        }
    }
}
