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
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            target.texture = texture;
        }

        private void Draw()
        {
            var colors = new Color32[width * height];
            var unknown = new Color32(2, 2, 3, 255);
            var exploredFloor = new Color32(42, 39, 36, 255);
            var visibleFloor = new Color32(86, 74, 58, 255);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!game.Dungeon.IsExplored(x, y))
                {
                    colors[y * width + x] = unknown;
                    continue;
                }
                colors[y * width + x] = game.Dungeon.IsVisible(x, y) ? visibleFloor : exploredFloor;
            }
            foreach (var enemy in EnemyController.Snapshot())
                if (enemy != null && IsVisible(enemy.transform.position))
                    Set(colors, enemy.transform.position, enemy.IsBoss ? new Color32(235, 42, 35, 255) : new Color32(146, 38, 31, 255));
            var portal = ExitPortal.Active;
            if (portal != null)
            {
                var portalCell = new Vector2Int(Mathf.FloorToInt(portal.transform.position.x), Mathf.FloorToInt(portal.transform.position.y));
                if (portal.IsUnlocked || game.Dungeon.IsExplored(portalCell.x, portalCell.y))
                    SetMarker(colors, portal.transform.position, portal.IsUnlocked
                        ? new Color32(245, 126, 35, 255)
                        : new Color32(116, 100, 84, 255));
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
            pixels[y * width + x] = color;
        }

        private void SetMarker(Color32[] pixels, Vector2 position, Color32 color)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt(position.x), 0, width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(position.y), 0, height - 1);
            for (var offset = -1; offset <= 1; offset++)
            {
                var horizontal = Mathf.Clamp(x + offset, 0, width - 1);
                var vertical = Mathf.Clamp(y + offset, 0, height - 1);
                pixels[y * width + horizontal] = color;
                pixels[vertical * width + x] = color;
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
