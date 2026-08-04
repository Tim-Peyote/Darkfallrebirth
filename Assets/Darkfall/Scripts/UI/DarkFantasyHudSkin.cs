using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    public static class DarkFantasyHudSkin
    {
        public static Sprite Player => DarkFantasySkin.Tooltip;
        public static Sprite Boss => DarkFantasySkin.Button;
        public static Sprite Minimap => DarkFantasySkin.Tooltip;
        public static Sprite Quickbar => DarkFantasySkin.Tooltip;
        public static Sprite Ability => DarkFantasySkin.Slot;
        public static Sprite InventoryButton => DarkFantasySkin.Slot;
        public static Sprite PauseButton => DarkFantasySkin.Slot;
        public static Sprite Prompt => DarkFantasySkin.Button;

        public static void Apply(Image image, Sprite sprite)
        {
            DarkFantasySkin.Apply(image, sprite, sprite == Ability
                ? new Color(1f, .58f, .18f, 1f)
                : Color.white);
        }
    }
}
