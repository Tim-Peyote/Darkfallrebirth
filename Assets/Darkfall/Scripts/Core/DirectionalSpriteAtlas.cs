using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public enum CharacterMotion { Idle, Walk, Attack, Hit }

    public static class DirectionalSpriteAtlas
    {
        private const int Columns = 8;
        private const int Rows = 4;
        // Authored side cycles are contact A, passing A, contact B, passing B.
        private static readonly int[] HeroWalkOrder = { 1, 2, 3, 4 };
        private static readonly int[] HeroIdleOrder = { 1, 2, 3, 4, 3, 2 };
        private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Sprite> HeroCache = new Dictionary<string, Sprite>();

        public static Sprite Get(string sheet, Vector2 facing, CharacterMotion motion, float time)
        {
            return Get(sheet, facing, motion, time, out _);
        }

        public static Sprite Get(string sheet, Vector2 facing, CharacterMotion motion, float time, out bool flipX)
        {
            if (TryGetHero(sheet, facing, motion, time, out var heroSprite, out flipX)) return heroSprite;
            flipX = false;
            var sprites = Load(sheet);
            if (sprites == null) return null;
            // Enemy atlases historically contained independently generated left/right rows.
            // They are not phase-identical, so use the authored right row as the canonical side
            // and mirror it. This keeps feet, weapons and hit reactions consistent in both directions.
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                flipX = facing.x < 0f;
                facing = Vector2.right;
            }
            var row = DirectionRow(facing);
            var column = MotionColumn(sheet, motion, time);
            return sprites[row * Columns + column];
        }

        public static Sprite HeroPortrait(HeroClass heroClass)
        {
            var sheet = heroClass == HeroClass.Mage ? "mage-v2" :
                heroClass == HeroClass.Warrior ? "warrior-v2" : "rogue-v2";
            return Get(sheet, Vector2.down, CharacterMotion.Idle, 0f) ?? GameSpriteAtlas.Hero(heroClass);
        }

        public static Vector2 StabilizeFourWay(Vector2 facing, Vector2 current)
        {
            if (facing.sqrMagnitude < .001f) return current.sqrMagnitude > .001f ? current : Vector2.down;
            var x = Mathf.Abs(facing.x);
            var y = Mathf.Abs(facing.y);
            var currentHorizontal = Mathf.Abs(current.x) > .5f;
            // Keep the current axis around diagonals. Without this hysteresis mouse aim makes
            // adjacent directional frames alternate every update and the actor appears to jerk.
            if (currentHorizontal)
            {
                if (y > x * 1.22f) return facing.y >= 0f ? Vector2.up : Vector2.down;
                return facing.x >= 0f ? Vector2.right : Vector2.left;
            }
            if (x > y * 1.22f) return facing.x >= 0f ? Vector2.right : Vector2.left;
            return facing.y >= 0f ? Vector2.up : Vector2.down;
        }

        private static bool TryGetHero(string sheet, Vector2 facing, CharacterMotion motion, float time,
            out Sprite sprite, out bool flipX)
        {
            sprite = null;
            flipX = false;
            var hero = HeroName(sheet);
            if (hero == null) return false;

            var horizontal = Mathf.Abs(facing.x) > Mathf.Abs(facing.y);
            string direction;
            if (horizontal)
            {
                // One canonical side keeps gait, weapon and reaction timing identical in both directions.
                // SpriteRenderer.flipX affects rendering only, so gameplay colliders remain stable.
                direction = "right";
                flipX = facing.x < 0f;
            }
            else direction = facing.y > 0f ? "up" : "down";

            var frame = MotionFrame(motion, time);
            var pixelsPerUnit = 180f;
            // The generated mage contacts repeatedly used the same visible leg. The independently
            // authored left contact contains the missing anatomical phase; mirror that source for
            // right-facing playback and compensate its 209px silhouette to the 200px side baseline.
            if (hero == "mage" && horizontal && motion == CharacterMotion.Walk && frame == "walk_3")
            {
                direction = "left";
                frame = "walk_2";
                flipX = facing.x > 0f;
                pixelsPerUnit = 188f;
            }
            var path = $"Sprites/Characters/{hero}/{direction}/{frame}";
            if (HeroCache.TryGetValue(path, out sprite)) return sprite != null;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null && motion == CharacterMotion.Idle)
            {
                // Compatibility for imported characters that have not received an authored idle cycle yet.
                path = $"Sprites/Characters/{hero}/{direction}/idle";
                texture = Resources.Load<Texture2D>(path);
            }
            if (texture == null)
            {
                HeroCache[path] = null;
                return false;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .08f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = $"{hero}-{direction}-{frame}";
            HeroCache[path] = sprite;
            return true;
        }

        private static string HeroName(string sheet)
        {
            switch (sheet)
            {
                case "mage-v2": return "mage";
                case "warrior-v2": return "warrior";
                case "rogue-v2": return "rogue";
                default: return null;
            }
        }

        private static string MotionFrame(CharacterMotion motion, float time)
        {
            switch (motion)
            {
                case CharacterMotion.Walk: return $"walk_{HeroWalkOrder[Mathf.FloorToInt(time * 8f) % HeroWalkOrder.Length]}";
                case CharacterMotion.Attack: return $"attack_{Mathf.Clamp(Mathf.FloorToInt(time * 12.5f) + 1, 1, 3)}";
                case CharacterMotion.Hit: return $"hurt_{Mathf.Clamp(Mathf.FloorToInt(time * 9f) + 1, 1, 2)}";
                default: return $"idle_{HeroIdleOrder[Mathf.FloorToInt(time * 2.4f) % HeroIdleOrder.Length]}";
            }
        }

        private static Sprite[] Load(string sheet)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            if (Cache.TryGetValue(sheet, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("Sprites/Directional/" + sheet);
            if (texture == null)
            {
                Cache[sheet] = null;
                return null;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var cellWidth = texture.width / (float)Columns;
            var cellHeight = texture.height / (float)Rows;
            var result = new Sprite[Columns * Rows];
            for (var row = 0; row < Rows; row++)
            for (var column = 0; column < Columns; column++)
            {
                var rect = new Rect(column * cellWidth, texture.height - (row + 1) * cellHeight, cellWidth, cellHeight);
                result[row * Columns + column] = Sprite.Create(texture, rect, new Vector2(.5f, .18f), 180f,
                    0, SpriteMeshType.FullRect);
            }
            Cache[sheet] = result;
            return result;
        }

        private static int DirectionRow(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y)) return facing.x < 0 ? 1 : 2;
            return facing.y < 0 ? 0 : 3;
        }

        private static int MotionColumn(string sheet, CharacterMotion motion, float time)
        {
            switch (motion)
            {
                case CharacterMotion.Walk: return 2 + Mathf.FloorToInt(time * 8f) % 2;
                case CharacterMotion.Attack:
                    // The ranged sheet keeps the projectile in a separate cell; never swap the actor for that cell.
                    if (sheet == "enemy-ranged-v2") return time < .14f ? 4 : 6;
                    // Attacks are one-shot sequences. Looping during the short attack window caused
                    // the pose to jump back to wind-up before returning to idle.
                    return 4 + Mathf.Clamp(Mathf.FloorToInt(time / .093f), 0, 2);
                case CharacterMotion.Hit: return 7;
                default: return Mathf.FloorToInt(time * 2.2f) % 2;
            }
        }
    }
}
