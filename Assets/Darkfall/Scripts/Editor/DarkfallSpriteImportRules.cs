#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Darkfall.Editor
{
    public sealed class DarkfallSpriteImportRules : AssetPostprocessor
    {
        private const string Characters = "Assets/Darkfall/Resources/Sprites/Characters/";
        private const string Items = "Assets/Darkfall/Resources/Sprites/Items/Individual/";

        private void OnPreprocessTexture()
        {
            var isCharacter = assetPath.StartsWith(Characters, System.StringComparison.Ordinal);
            var isItem = assetPath.StartsWith(Items, System.StringComparison.Ordinal);
            if (!isCharacter && !isItem) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = isCharacter ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
#endif
