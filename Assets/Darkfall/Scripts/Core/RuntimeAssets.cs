using UnityEngine;

namespace Darkfall.Core
{
    public static class RuntimeAssets
    {
        private static Sprite square;
        private static Sprite circle;
        private static Sprite glow;

        public static Sprite Square => square != null ? square : square = BuildSprite(false);
        public static Sprite Circle => circle != null ? circle : circle = BuildSprite(true);
        public static Sprite Glow => glow != null ? glow : glow = BuildGlow();

        private static Sprite BuildSprite(bool round)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = round ? "RuntimeCircle" : "RuntimeSquare",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var visible = !round || Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) <= center;
                pixels[y * size + x] = visible ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite BuildGlow()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeRadialGlow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            var center = Vector2.one * (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                var alpha = Mathf.Pow(Mathf.Clamp01(1 - distance), 2.2f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 32);
        }
    }
}
