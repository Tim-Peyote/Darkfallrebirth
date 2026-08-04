using UnityEngine;

namespace Darkfall.Gameplay
{
    public static class GameInput
    {
        public static Vector2 TouchMove { get; set; }
        public static bool TouchAttack { get; set; }
        public static bool TouchAbilityRequested { get; set; }
        public static bool InventoryPressed => Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab);
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);
        public static int QuickSlotPressed =>
            Input.GetKeyDown(KeyCode.Alpha1) ? 0 : Input.GetKeyDown(KeyCode.Alpha2) ? 1 : Input.GetKeyDown(KeyCode.Alpha3) ? 2 : -1;

        public static Vector2 Move
        {
            get
            {
                var keyboard = new Vector2(
                    (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                    (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                    (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
                return keyboard.sqrMagnitude > 0.01f ? Vector2.ClampMagnitude(keyboard, 1) : TouchMove;
            }
        }

        public static bool AttackHeld => Input.GetMouseButton(0) || TouchAttack;

        public static bool ConsumeAbility()
        {
            if (Input.GetKeyDown(KeyCode.Q)) return true;
            if (!TouchAbilityRequested) return false;
            TouchAbilityRequested = false;
            return true;
        }

        public static bool RogueDashPressed => Input.GetKeyDown(KeyCode.Space);

        public static void Reset()
        {
            TouchMove = Vector2.zero;
            TouchAttack = false;
            TouchAbilityRequested = false;
        }
    }
}
