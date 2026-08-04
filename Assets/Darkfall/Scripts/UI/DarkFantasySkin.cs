using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    /// <summary>
    /// Runtime, resolution-independent Darkfall UI primitives. Every element is a
    /// rounded 9-slice surface, so panels never stretch their corners or inherit
    /// neighbouring pixels from a raster atlas.
    /// </summary>
    public static class DarkFantasySkin
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static readonly Color Ink = new Color(.018f, .022f, .026f, .98f);
        public static readonly Color Surface = new Color(.035f, .041f, .045f, .96f);
        public static readonly Color Raised = new Color(.055f, .058f, .058f, .98f);
        public static readonly Color Line = new Color(.34f, .29f, .20f, .72f);
        public static readonly Color Gold = new Color(.82f, .63f, .30f, 1f);
        public static readonly Color Text = new Color(.91f, .89f, .84f, 1f);
        public static readonly Color MutedText = new Color(.62f, .63f, .61f, 1f);

        public static Sprite Panel => Get("panel", new Color(.048f, .045f, .039f, .985f),
            new Color(.018f, .020f, .022f, .985f), new Color(.37f, .29f, .16f, .82f), 14f, 1.5f);
        public static Sprite Tooltip => Get("surface", new Color(.040f, .045f, .048f, .97f),
            new Color(.018f, .021f, .024f, .97f), new Color(.23f, .24f, .23f, .72f), 12f, 1.25f);
        public static Sprite Button => Get("button", new Color(.095f, .087f, .072f, .99f),
            new Color(.039f, .040f, .039f, .99f), new Color(.46f, .34f, .17f, .88f), 10f, 1.35f);
        public static Sprite Slot => Get("slot", new Color(.054f, .061f, .066f, .99f),
            new Color(.022f, .026f, .030f, .99f), new Color(.24f, .27f, .28f, .92f), 8f, 1.2f);
        public static Sprite HealthBar => Get("bar-track", new Color(.044f, .043f, .039f, 1f),
            new Color(.016f, .017f, .018f, 1f), new Color(.29f, .25f, .20f, .9f), 11f, 1.2f);
        public static Sprite HealthFill => Get("health-fill", new Color(.86f, .26f, .12f, 1f),
            new Color(.38f, .025f, .025f, 1f), new Color(.95f, .48f, .22f, .9f), 10f, .9f);
        public static Sprite GoldFill => Get("gold-fill", new Color(.95f, .67f, .22f, 1f),
            new Color(.48f, .25f, .06f, 1f), new Color(1f, .78f, .34f, .9f), 8f, .8f);

        public static void Apply(Image image, Sprite sprite, Color? tint = null)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = tint.HasValue ? Color.Lerp(Color.white, tint.Value, .10f) : Color.white;

            var oldOutline = image.GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
            if (sprite == Slot)
            {
                var targetOutline = oldOutline ?? image.gameObject.AddComponent<Outline>();
                targetOutline.enabled = true;
                targetOutline.effectColor = new Color(0, 0, 0, 0);
                targetOutline.effectDistance = new Vector2(1f, -1f);
                targetOutline.useGraphicAlpha = true;
            }
        }

        private static Sprite Get(string name, Color top, Color bottom, Color border, float radius, float borderWidth)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Darkfall UI " + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            var half = new Vector2(size * .5f - 1f, size * .5f - 1f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f);
                var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
                var outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude;
                var distance = outside + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                var alpha = Mathf.Clamp01(.75f - distance);
                if (alpha <= 0) { pixels[y * size + x] = Color.clear; continue; }

                var vertical = Mathf.SmoothStep(0, 1, y / (size - 1f));
                var fill = Color.Lerp(bottom, top, vertical);
                // A restrained inner highlight gives depth without bevelled raster ornament.
                if (y > size * .72f) fill = Color.Lerp(fill, Color.white, .025f * ((y - size * .72f) / (size * .28f)));
                var borderMask = Mathf.Clamp01((distance + borderWidth + .5f) / Mathf.Max(.75f, borderWidth));
                var color = Color.Lerp(fill, border, borderMask);
                color.a *= alpha;
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var borderPixels = Mathf.Ceil(radius + borderWidth + 2f);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, new Vector4(borderPixels, borderPixels, borderPixels, borderPixels));
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
