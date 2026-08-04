using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class UIHeroIdleAnimator : MonoBehaviour
    {
        private static readonly int[] Sequence = { 0, 1, 2, 3, 2, 1 };
        private static readonly Dictionary<HeroClass, Sprite[]> Cache = new Dictionary<HeroClass, Sprite[]>();
        private Image image;
        private Sprite[] frames;
        private float phase;
        private int currentFrame = -1;

        public void Initialize(HeroClass heroClass, float timeOffset)
        {
            image = GetComponent<Image>();
            frames = Load(heroClass);
            phase = timeOffset;
            ApplyFrame(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length < 4) return;
            var sequenceIndex = Mathf.FloorToInt((Time.unscaledTime + phase) * 3f) % Sequence.Length;
            ApplyFrame(Sequence[sequenceIndex]);
        }

        private void ApplyFrame(int index)
        {
            if (image == null || frames == null || index < 0 || index >= frames.Length || currentFrame == index) return;
            currentFrame = index;
            image.sprite = frames[index];
            // The RectTransform never scales or rotates: all motion comes from authored frames.
            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.localRotation = Quaternion.identity;
        }

        private static Sprite[] Load(HeroClass heroClass)
        {
            if (Cache.TryGetValue(heroClass, out var cached)) return cached;
            var hero = heroClass == HeroClass.Mage ? "mage" : heroClass == HeroClass.Warrior ? "warrior" : "rogue";
            var result = new Sprite[4];
            for (var i = 0; i < result.Length; i++)
            {
                var texture = Resources.Load<Texture2D>($"Sprites/UIHeroIdle/{hero}/idle_{i + 1}");
                if (texture == null) continue;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                result[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .08f), 180f, 0, SpriteMeshType.FullRect);
                result[i].name = $"{hero}-preview-idle-{i + 1}";
            }
            Cache[heroClass] = result;
            return result;
        }
    }
}
