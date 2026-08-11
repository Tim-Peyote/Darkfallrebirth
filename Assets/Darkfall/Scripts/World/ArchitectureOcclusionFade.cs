using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Locally fades only an elevated architecture module that covers the hero.</summary>
    public sealed class ArchitectureOcclusionFade : MonoBehaviour
    {
        private Renderer[] renderers;
        private Color[] baseColors;
        private float architectureElevation;
        private float visibility = 1f;

        public void Initialize(float elevation)
        {
            architectureElevation = elevation;
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
                baseColors[i] = RendererColor(renderers[i]);
        }

        private void LateUpdate()
        {
            var manager = GameManager.Instance;
            var player = manager?.Player;
            var dungeon = manager?.Dungeon;
            if (player == null || dungeon == null || renderers == null) return;

            var playerElevation = dungeon.SurfaceHeight(player.transform.position);
            var shouldFade = architectureElevation > playerElevation + .24f && CoversPlayer(player, dungeon);
            var target = shouldFade ? .22f : 1f;
            visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime *
                (shouldFade ? 5.8f : 3.8f));
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var color = baseColors[i];
                color.a *= visibility;
                SetRendererColor(renderers[i], color);
            }
        }

        private bool CoversPlayer(Darkfall.Gameplay.PlayerController player, DungeonData dungeon)
        {
            var point = (Vector2)IsoWorld.Project(player.transform.position,
                dungeon.SurfaceHeight(player.transform.position) + .34f);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                var bounds = renderer.bounds;
                // The torso, not the complete sprite gutter, determines whether the wall blocks
                // interaction readability. A small margin avoids rapid toggling at module seams.
                if (point.x >= bounds.min.x - .08f && point.x <= bounds.max.x + .08f &&
                    point.y >= bounds.min.y - .12f && point.y <= bounds.max.y + .16f) return true;
            }
            return false;
        }

        private static Color RendererColor(Renderer renderer) => renderer is SpriteRenderer sprite
            ? sprite.color : renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer is SpriteRenderer sprite) sprite.color = color;
        }
    }
}
