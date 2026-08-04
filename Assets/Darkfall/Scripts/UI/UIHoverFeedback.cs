using UnityEngine;
using UnityEngine.EventSystems;

namespace Darkfall.UI
{
    /// <summary>Subtle resolution-independent hover/press response shared by cards and buttons.</summary>
    public sealed class UIHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 restingScale;
        private float target = 1f;
        private float hoverScale = 1.012f;

        public UIHoverFeedback Initialize(float scale = 1.012f)
        {
            hoverScale = scale;
            return this;
        }

        private void Awake() => restingScale = transform.localScale;

        private void Update()
        {
            var response = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, restingScale * target, response);
        }

        public void OnPointerEnter(PointerEventData eventData) => target = hoverScale;
        public void OnPointerExit(PointerEventData eventData) => target = 1f;
        public void OnPointerDown(PointerEventData eventData) => target = .985f;
        public void OnPointerUp(PointerEventData eventData) => target = hoverScale;
    }
}
