using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    public sealed class DungeonFlameAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private int frame;
        private float nextFrame;
        private float frameDuration;
        private float visibility;

        public void Initialize(int sortingOrder)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(0);
            spriteRenderer.sortingOrder = sortingOrder;
            DarkfallRenderMaterials.MakeEmissive(spriteRenderer);
            frameDuration = Random.Range(.075f, .105f);
            frame = Random.Range(0, 4);
            nextFrame = Time.time + Random.Range(0f, frameDuration);
        }

        private void Update()
        {
            if (spriteRenderer == null) return;
            var dungeon = GameManager.Instance?.Dungeon;
            var projectedOwner = GetComponentInParent<IsoVisual>();
            var position = projectedOwner != null ? projectedOwner.LogicalPosition : (Vector2)transform.position;
            var x = Mathf.FloorToInt(position.x);
            var y = Mathf.FloorToInt(position.y);
            // A discovered light source must not turn into an inexplicably empty bowl as soon
            // as it leaves the current visibility polygon. Fog still controls unexplored rooms;
            // once discovered, the flame and its persistent authored light remain consistent.
            var targetVisibility = dungeon != null && (dungeon.IsVisible(x, y) || dungeon.IsExplored(x, y))
                ? 1f
                : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 7f);
            spriteRenderer.color = new Color(1, 1, 1, visibility);
            if (Time.time < nextFrame) return;
            frame = (frame + 1) % 4;
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(frame);
            nextFrame = Time.time + frameDuration;
        }
    }
}
