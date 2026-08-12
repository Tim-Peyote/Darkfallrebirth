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
                DungeonMiniSetKind.Campfire => "campfire-01",
                DungeonMiniSetKind.Altar => "altar",
                _ => null
            };
            return string.IsNullOrEmpty(name) ? null : Load(name);
        }

        public static Sprite CampfireFrame(int frame) => Load("campfire-0" + (Mathf.Abs(frame) % 4 + 1));
        public static Sprite CampfireUnlit => Load("campfire-unlit");

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

    public sealed class MiniSetCampfireAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private int frame;
        private float nextFrame;
        private float frameDuration;
        private Vector3 baseScale;

        public void Initialize(SpriteRenderer target)
        {
            spriteRenderer = target;
            frame = Random.Range(0, 4);
            frameDuration = Random.Range(.09f, .125f);
            baseScale = transform.localScale;
            nextFrame = Time.unscaledTime + Random.Range(0f, frameDuration);
            if (spriteRenderer != null) spriteRenderer.sprite = MiniSetSpriteLibrary.CampfireFrame(frame);
        }

        private void Update()
        {
            if (spriteRenderer == null || Time.unscaledTime < nextFrame) return;
            frame = (frame + 1) % 4;
            spriteRenderer.sprite = MiniSetSpriteLibrary.CampfireFrame(frame);
            var breathe = 1f + Mathf.Sin((Time.unscaledTime + GetInstanceID() * .013f) * 11f) * .012f;
            transform.localScale = baseScale * breathe;
            nextFrame = Time.unscaledTime + frameDuration;
        }
    }
}
