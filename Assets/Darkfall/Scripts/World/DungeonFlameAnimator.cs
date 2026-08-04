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
        private Vector3 baseScale;
        private float visibility;

        public void Initialize(int sortingOrder)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(0);
            spriteRenderer.sortingOrder = sortingOrder;
            DarkfallRenderMaterials.MakeEmissive(spriteRenderer);
            frameDuration = Random.Range(.075f, .105f);
            frame = Random.Range(0, 4);
            baseScale = transform.localScale;
            nextFrame = Time.time + Random.Range(0f, frameDuration);
        }

        private void Update()
        {
            if (spriteRenderer == null) return;
            var dungeon = GameManager.Instance?.Dungeon;
            var position = transform.position;
            var targetVisibility = dungeon != null && dungeon.IsVisible(
                Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)) ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 7f);
            spriteRenderer.color = new Color(1, 1, 1, visibility);
            if (Time.time < nextFrame) return;
            frame = (frame + 1) % 4;
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(frame);
            var breathe = 1f + Mathf.Sin((Time.time + GetInstanceID() * .01f) * 12f) * .025f;
            transform.localScale = baseScale * breathe;
            nextFrame = Time.time + frameDuration;
        }
    }
}
