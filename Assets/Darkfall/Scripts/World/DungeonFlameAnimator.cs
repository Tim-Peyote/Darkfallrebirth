using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    public sealed class DungeonFlameAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer bodyRenderer;
        private int relativeSortingOrder;
        private int frame;
        private float nextFrame;
        private float frameDuration;

        public void Initialize(int sortingOrder)
        {
            relativeSortingOrder = sortingOrder;
            bodyRenderer = transform.parent != null ? transform.parent.GetComponent<SpriteRenderer>() : null;
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(0);
            spriteRenderer.color = Color.white;
            RefreshSortingOrder();
            DarkfallRenderMaterials.MakeEmissive(spriteRenderer);
            frameDuration = Random.Range(.075f, .105f);
            frame = Random.Range(0, 4);
            nextFrame = Time.time + Random.Range(0f, frameDuration);
        }

        private void Update()
        {
            if (spriteRenderer == null) return;
            // The scene fog owns visibility for the entire brazier. Fading this child separately
            // produced explored bowls with no fire and made duplicated variants appear on screen.
            // Keep the authored flame opaque and let the common fog/light pass hide the composition.
            if (Time.time < nextFrame) return;
            AdvanceFrame();
            nextFrame = Time.time + frameDuration;
        }

        private void AdvanceFrame()
        {
            frame = (frame + 1) % 4;
            spriteRenderer.sprite = EnvironmentSpriteAtlas.Flame(frame);
        }

#if UNITY_EDITOR
        // Visual audits use the exact runtime frame transition without waiting on wall-clock time.
        // Only the child sprite changes; fixture position and scale remain authored and immutable.
        public void AdvanceFrameForAudit() => AdvanceFrame();
        public Sprite CurrentSpriteForAudit => spriteRenderer != null ? spriteRenderer.sprite : null;
#endif

        private void LateUpdate() => RefreshSortingOrder();

        private void RefreshSortingOrder()
        {
            if (spriteRenderer == null) return;
            if (bodyRenderer == null && transform.parent != null)
                bodyRenderer = transform.parent.GetComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerID = bodyRenderer != null ? bodyRenderer.sortingLayerID : 0;
            spriteRenderer.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 0) + relativeSortingOrder;
        }
    }
}
