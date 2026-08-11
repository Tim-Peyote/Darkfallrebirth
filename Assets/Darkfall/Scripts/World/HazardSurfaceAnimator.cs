using UnityEngine;

namespace Darkfall.World
{
    /// <summary>
    /// Animates only the connected liquid/organic surface, never its stone banks. UVs are derived
    /// from logical world coordinates, so stop-motion highlights stay perfectly joined across
    /// cell borders instead of restarting on every generated tile.
    /// </summary>
    public sealed class HazardSurfaceAnimator : MonoBehaviour
    {
        private Mesh mesh;
        private Color[] baseColors;
        private Color[] animatedColors;
        private Vector2[] flowCoordinates;
        private float speed;
        private float amplitude;
        private float nextFrame;
        private Material material;
        private Vector2 textureOffset;

        public void Initialize(Mesh target, DungeonHazardKind kind, Material surfaceMaterial)
        {
            mesh = target;
            material = surfaceMaterial;
            baseColors = mesh != null ? mesh.colors : null;
            flowCoordinates = mesh != null ? mesh.uv : null;
            animatedColors = baseColors != null ? new Color[baseColors.Length] : null;
            speed = kind == DungeonHazardKind.Lava ? 4.4f :
                kind == DungeonHazardKind.Brine ? 1.35f :
                kind == DungeonHazardKind.Bile ? 1.9f :
                kind == DungeonHazardKind.VoidRift ? 2.8f : 2.25f;
            amplitude = kind == DungeonHazardKind.Lava ? .17f :
                kind == DungeonHazardKind.VoidRift ? .14f : .1f;
        }

        private void Update()
        {
            if (mesh == null || baseColors == null || flowCoordinates == null || Time.time < nextFrame) return;
            nextFrame = Time.time + 1f / 9f;
            textureOffset.x = Mathf.Repeat(textureOffset.x + speed * .00065f, 1f);
            textureOffset.y = Mathf.Repeat(textureOffset.y + speed * .00023f, 1f);
            if (material != null) material.mainTextureOffset = textureOffset;
            for (var i = 0; i < animatedColors.Length; i++)
            {
                var wave = Mathf.Sin(Time.time * speed - flowCoordinates[i].x * Mathf.PI * 2f);
                wave = Mathf.Round(wave * 3f) / 3f;
                var value = 1f + wave * amplitude;
                var color = baseColors[i];
                animatedColors[i] = new Color(Mathf.Clamp01(color.r * value),
                    Mathf.Clamp01(color.g * value), Mathf.Clamp01(color.b * value), color.a);
            }
            mesh.colors = animatedColors;
        }
    }
}
