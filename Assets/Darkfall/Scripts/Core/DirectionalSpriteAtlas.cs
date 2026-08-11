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
        private static readonly Dictionary<string, int> EnemyRightRows = new Dictionary<string, int>
        {
            { "enemy-melee-v2", 2 }, { "enemy-ranged-v2", 2 }, { "enemy-caster-v2", 1 },
            { "enemy-mimic-v1", 1 }, { "enemy-ash-warden-v1", 2 },
            { "enemy-ember-revenant-v1", 2 }, { "enemy-drowned-sentinel-v1", 2 },
            { "enemy-spore-stalker-v1", 1 }, { "enemy-obsidian-acolyte-v1", 1 }
        };
        // Stable foot baselines per authored direction. Do not derive this from every frame's
        // lowest alpha pixel: a staff, coat or attack flourish can extend below the actual foot
        // and would make the whole actor jump during attack/hurt. The files all use a 256px
        // canvas, but their directional lower gutters differ by more than 100px.
        private static readonly Dictionary<string, float> HeroFootPivots = new Dictionary<string, float>
        {
            { "mage/down", 30f / 256f }, { "mage/up", 42f / 256f },
            { "mage/left", 46f / 256f }, { "mage/right", 46f / 256f },
            { "warrior/down", 17f / 256f }, { "warrior/up", 32f / 256f },
            { "warrior/left", 38f / 256f }, { "warrior/right", 38f / 256f },
            { "rogue/down", 100f / 256f }, { "rogue/up", 111f / 256f },
            { "rogue/left", 103f / 256f }, { "rogue/right", 103f / 256f }
        };

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
            var row = DirectionRow(sheet, facing);
            var column = MotionColumn(sheet, motion, time);
            return sprites[row * Columns + column];
        }

        public static Sprite HeroPortrait(HeroClass heroClass)
        {
            var sheet = heroClass == HeroClass.Mage ? "mage-v2" :
                heroClass == HeroClass.Warrior ? "warrior-v2" : "rogue-v2";
            return Get(sheet, Vector2.down, CharacterMotion.Idle, 0f) ?? GameSpriteAtlas.Hero(heroClass);
        }

        public static float HeroDirectionScale(string sheet, Vector2 facing)
        {
            var hero = HeroName(sheet);
            if (hero == null) return 1f;
            var horizontal = Mathf.Abs(facing.x) > Mathf.Abs(facing.y);
            var direction = horizontal ? facing.x < 0f ? "left" : "right" : facing.y > 0f ? "up" : "down";
            // Normalize the median idle/walk silhouette height to the authored down-facing row.
            // This is transform-only; pixels are never resampled and the collider is unaffected.
            if (direction == "down") return 1f;
            if (hero == "mage") return direction == "up" ? 1.06f : 1.08f;
            if (hero == "warrior") return direction == "up" ? 1.07f : 1.10f;
            return direction == "up" ? 1.08f : 1.02f;
        }

        public static bool HasCompleteHeroLayout(string hero)
        {
            return HeroFootPivots.ContainsKey(hero + "/down") && HeroFootPivots.ContainsKey(hero + "/up") &&
                   HeroFootPivots.ContainsKey(hero + "/left") && HeroFootPivots.ContainsKey(hero + "/right");
        }

        public static bool HasEnemyDirectionConvention(string sheet) => EnemyRightRows.ContainsKey(sheet);

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
                // Heroes carry asymmetric weapons and authored left/right actions exist. Mirroring
                // the right side moved the staff, sword and shield to the wrong hand and discarded
                // the reviewed left gait, attack and hit frames.
                direction = facing.x < 0f ? "left" : "right";
            }
            else direction = facing.y > 0f ? "up" : "down";

            var frame = MotionFrame(motion, time);
            const float pixelsPerUnit = 180f;
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
                new Vector2(.5f, HeroPivotY(hero, direction)), pixelsPerUnit, 0, SpriteMeshType.FullRect);
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

        private static float HeroPivotY(string hero, string direction)
        {
            return HeroFootPivots.TryGetValue(hero + "/" + direction, out var pivot) ? pivot : .08f;
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

        private static int DirectionRow(string sheet, Vector2 facing)
        {
            // The generated sheets do not share one side-row convention. Melee/ranged and some
            // biome actors are front,left,right,back; caster-derived sheets are
            // front,right,left,back. Get() mirrors one canonical right-facing row, so encode that
            // per sheet instead of globally swapping every enemy to fix one mage.
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                var rightRow = EnemyRightRows.TryGetValue(sheet, out var authoredRow) ? authoredRow : 2;
                return facing.x < 0f ? rightRow == 1 ? 2 : 1 : rightRow;
            }
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
