using UnityEngine;

namespace Darkfall.World
{
    /// <summary>
    /// Gives signature biome landmarks a restrained stop-motion life. Motion remains on the
    /// projected visual, never on the logical obstacle, so navigation and attack ranges stay exact.
    /// </summary>
    public sealed class BiomeEventAnimator : MonoBehaviour
    {
        private Vector3 baseScale;
        private Vector3 basePosition;
        private float phase;
        private float pulse;
        private float drift;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;

        public void Initialize(string biome, int index)
        {
            baseScale = transform.localScale;
            basePosition = transform.localPosition;
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            phase = (index * 1.713f + GetInstanceID() * .0017f) % 6.28318f;

            switch (biome)
            {
                case "ember-vaults":
                    pulse = .018f;
                    drift = index == 9 || index == 11 ? .012f : .004f;
                    break;
                case "drowned-crypt":
                    pulse = index == 8 ? .014f : .007f;
                    drift = .009f;
                    break;
                case "charnel-gardens":
                    pulse = index == 8 || index == 11 ? .026f : .016f;
                    drift = .008f;
                    break;
                case "obsidian-sanctum":
                    pulse = .012f;
                    drift = .025f;
                    break;
                default:
                    pulse = index == 10 ? .012f : .005f;
                    drift = index == 10 ? .012f : .003f;
                    break;
            }
        }

        private void Update()
        {
            if (baseScale == Vector3.zero) return;
            var time = Time.time * 1.35f + phase;
            var breathe = 1f + Mathf.Sin(time) * pulse;
            transform.localScale = baseScale * breathe;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(time * .73f) * drift);
            if (spriteRenderer != null)
            {
                var luminance = 1f + Mathf.Sin(time * 1.41f) * pulse * 1.8f;
                spriteRenderer.color = new Color(
                    Mathf.Clamp01(baseColor.r * luminance), Mathf.Clamp01(baseColor.g * luminance),
                    Mathf.Clamp01(baseColor.b * luminance), baseColor.a);
            }
        }
    }
}
