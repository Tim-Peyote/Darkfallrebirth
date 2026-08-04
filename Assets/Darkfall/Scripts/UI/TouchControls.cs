using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Darkfall.UI
{
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private RectTransform knob;

        public void Initialize(RectTransform knobTransform)
        {
            rect = transform as RectTransform;
            knob = knobTransform;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local))
                return;
            var radius = rect.rect.width * 0.38f;
            var movement = Vector2.ClampMagnitude(local / radius, 1);
            knob.anchoredPosition = movement * radius;
            GameInput.TouchMove = movement;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            knob.anchoredPosition = Vector2.zero;
            GameInput.TouchMove = Vector2.zero;
        }
    }

    public sealed class HoldAttackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData) => GameInput.TouchAttack = true;
        public void OnPointerUp(PointerEventData eventData) => GameInput.TouchAttack = false;
        private void OnDisable() => GameInput.TouchAttack = false;
    }
}
