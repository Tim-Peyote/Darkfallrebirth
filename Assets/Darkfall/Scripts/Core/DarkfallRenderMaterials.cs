using UnityEngine;

namespace Darkfall.Core
{
    public static class DarkfallRenderMaterials
    {
        private static Material spriteLit;
        private static Material spriteUnlit;

        public static Material SpriteLit => spriteLit != null ? spriteLit : spriteLit = Create(
            "Universal Render Pipeline/2D/Sprite-Lit-Default", "Sprites/Default", "Darkfall Sprite Lit");

        public static Material SpriteUnlit => spriteUnlit != null ? spriteUnlit : spriteUnlit = Create(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default", "Darkfall Sprite Emissive");

        public static void MakeLit(SpriteRenderer renderer)
        {
            if (renderer != null) renderer.sharedMaterial = SpriteLit;
        }

        public static void MakeEmissive(SpriteRenderer renderer)
        {
            if (renderer != null) renderer.sharedMaterial = SpriteUnlit;
        }

        private static Material Create(string shaderName, string fallback, string materialName)
        {
            var shader = Shader.Find(shaderName) ?? Shader.Find(fallback);
            return new Material(shader) { name = materialName, hideFlags = HideFlags.DontSave };
        }
    }
}
