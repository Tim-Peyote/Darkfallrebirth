using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    [RequireComponent(typeof(RawImage))]
    public sealed class RawImageCover : MonoBehaviour
    {
        private RawImage image;
        private Vector2 lastSize;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            Apply();
        }

        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (image == null) image = GetComponent<RawImage>();
            if (image.texture == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0 || size == lastSize) return;
            lastSize = size;
            var viewAspect = size.x / size.y;
            var textureAspect = image.texture.width / (float)image.texture.height;
            if (viewAspect > textureAspect)
            {
                var height = textureAspect / viewAspect;
                image.uvRect = new Rect(0, (1f - height) * .5f, 1, height);
            }
            else
            {
                var width = viewAspect / textureAspect;
                image.uvRect = new Rect((1f - width) * .5f, 0, width, 1);
            }
        }
    }
}
