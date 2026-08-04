using UnityEngine;

namespace Darkfall.Core
{
    public static class GameSpriteAtlas
    {
        private static readonly Sprite[] sprites = new Sprite[8];
        private static Texture2D atlas;

        public static Sprite Hero(HeroClass hero) => Get(hero == HeroClass.Mage ? 0 : hero == HeroClass.Warrior ? 1 : 2);
        public static Sprite Enemy(string type)
        {
            if (string.IsNullOrEmpty(type)) return Get(3);
            var lower = type.ToLowerInvariant();
            if (lower.Contains("archer") || lower.Contains("spitter") || lower.Contains("assassin")) return Get(4);
            if (lower.Contains("mage") || lower.Contains("wraith") || lower.Contains("demon") ||
                lower.Contains("lich") || lower.Contains("dragon")) return Get(5);
            return Get(3);
        }
        public static Sprite Chest(bool open) => Get(open ? 7 : 6);

        private static Sprite Get(int index)
        {
            if (sprites[index] != null) return sprites[index];
            atlas ??= Resources.Load<Texture2D>("Sprites/entities-atlas-v1");
            if (atlas == null) return RuntimeAssets.Circle;
            var cellWidth = atlas.width / 8f;
            var x = Mathf.Round(index * cellWidth);
            var rect = new Rect(x, 90, Mathf.Min(Mathf.Ceil(cellWidth), atlas.width - x), Mathf.Min(540, atlas.height - 90));
            sprites[index] = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.35f), 480, 0, SpriteMeshType.Tight);
            return sprites[index];
        }
    }
}
