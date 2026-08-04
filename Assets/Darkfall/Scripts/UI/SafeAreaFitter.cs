using UnityEngine;

namespace Darkfall.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            if (rect == null || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            rect.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            rect.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
