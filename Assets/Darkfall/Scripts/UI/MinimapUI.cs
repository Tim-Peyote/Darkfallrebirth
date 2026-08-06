using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    public sealed class MinimapUI : MonoBehaviour
    {
        private GameManager game;
        private RawImage target;
        private Texture2D texture;
        private int width;
        private int height;
        private int textureSize;
        private float nextRefresh;

        public void Initialize(GameManager manager, RawImage image)
        {
            game = manager;
            target = image;
        }

        private void Update()
        {
            if (game?.Dungeon == null || game.Player == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            if (texture == null || width != game.Dungeon.Width || height != game.Dungeon.Height) Rebuild();
            Draw();
        }

        private void Rebuild()
        {
            if (texture != null) Destroy(texture);
            width = game.Dungeon.Width;
            height = game.Dungeon.Height;
            textureSize = width + height - 1;
            texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            target.texture = texture;
        }

        private void Draw()
        {
            var colors = new Color32[textureSize * textureSize];
            var unknown = new Color32(2, 2, 3, 255);
            var exploredFloor = new Color32(42, 39, 36, 255);
            var visibleFloor = new Color32(86, 74, 58, 255);
            for (var pixel = 0; pixel < colors.Length; pixel++) colors[pixel] = unknown;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!game.Dungeon.IsExplored(x, y)) continue;
                SetCell(colors, x, y, game.Dungeon.IsVisible(x, y) ? visibleFloor : exploredFloor);
            }
            foreach (var enemy in EnemyController.Snapshot())
                if (enemy != null && IsVisible(enemy.transform.position))
                    Set(colors, enemy.transform.position, enemy.IsBoss ? new Color32(235, 42, 35, 255) : new Color32(146, 38, 31, 255));
            var portal = ExitPortal.Active;
            if (portal != null)
            {
                if (portal.IsEmpowered)
                    SetMarker(colors, portal.transform.position, new Color32(245, 126, 35, 255));
            }
            Set(colors, game.Player.transform.position, new Color32(231, 189, 92, 255));
            texture.SetPixels32(colors);
            texture.Apply(false);
        }

        private bool IsVisible(Vector2 position) =>
            game.Dungeon.IsVisible(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));

        private void Set(Color32[] pixels, Vector2 position, Color32 color)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt(position.x), 0, width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(position.y), 0, height - 1);
            var pixel = ProjectCell(x, y);
            Paint(pixels, pixel.x, pixel.y, color, 1);
        }

        private void SetMarker(Color32[] pixels, Vector2 position, Color32 color)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt(position.x), 0, width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(position.y), 0, height - 1);
            var pixel = ProjectCell(x, y);
            Paint(pixels, pixel.x, pixel.y, color, 2);
        }

        private void SetCell(Color32[] pixels, int x, int y, Color32 color)
        {
            var pixel = ProjectCell(x, y);
            Paint(pixels, pixel.x, pixel.y, color, 0);
            Paint(pixels, pixel.x + 1, pixel.y, color, 0);
        }

        private Vector2Int ProjectCell(int x, int y) => new Vector2Int(
            x - y + height - 1,
            width + height - 2 - x - y);

        private void Paint(Color32[] pixels, int centerX, int centerY, Color32 color, int radius)
        {
            for (var y = centerY - radius; y <= centerY + radius; y++)
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || y < 0 || x >= textureSize || y >= textureSize) continue;
                if (radius > 0 && Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY) > radius) continue;
                pixels[y * textureSize + x] = color;
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
