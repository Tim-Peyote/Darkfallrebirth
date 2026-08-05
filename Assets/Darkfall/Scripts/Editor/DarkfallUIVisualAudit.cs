#if UNITY_EDITOR
using System.IO;
using Darkfall.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.Editor
{
    [InitializeOnLoad]
    public static class DarkfallUIVisualAudit
    {
        // A file sentinel keeps runtime visual verification reproducible without editor UI automation.
        private const string Request = "/tmp/darkfall-ui-visual-audit.request";
        private const string TitleOutput = "/tmp/darkfall-title-runtime.png";
        private const string HeroOutput = "/tmp/darkfall-heroes-runtime.png";
        private const string HudOutput = "/tmp/darkfall-hud-runtime.png";
        private const string InventoryOutput = "/tmp/darkfall-inventory-runtime.png";
        private const string ActiveKey = "Darkfall.UIVisualAudit.Active";
        private static double enteredAt;
        private static int phase;

        static DarkfallUIVisualAudit()
        {
            if (File.Exists(Request))
            {
                File.Delete(Request);
                SessionState.SetBool(ActiveKey, true);
            }
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying)
            {
                enteredAt = EditorApplication.timeSinceStartup;
                phase = 0;
                EditorApplication.update += Tick;
            }
            else EditorApplication.update += TryStart;
        }

        private static void TryStart()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= TryStart;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Darkfall/Visual Audit/Main Menu")]
        private static void RequestCapture()
        {
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= TryStart;
            EditorApplication.update += TryStart;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enteredAt = EditorApplication.timeSinceStartup;
                phase = 0;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            }
        }

        private static void Tick()
        {
            var elapsed = EditorApplication.timeSinceStartup - enteredAt;
            if (phase == 0 && elapsed > 2.5)
            {
                ScreenCapture.CaptureScreenshot(TitleOutput);
                phase = 1;
            }
            else if (phase == 1 && elapsed > 4.0)
            {
                InvokeButton("НОВАЯ ИГРА");
                phase = 2;
            }
            else if (phase == 2 && elapsed > 5.5)
            {
                ScreenCapture.CaptureScreenshot(HeroOutput);
                phase = 3;
            }
            else if (phase == 3 && elapsed > 7.5)
            {
                InvokeButton("НАЧАТЬ ПОГРУЖЕНИЕ");
                phase = 4;
            }
            else if (phase == 4 && elapsed > 12.5)
            {
                ScreenCapture.CaptureScreenshot(HudOutput);
                phase = 5;
            }
            else if (phase == 5 && elapsed > 14.0)
            {
                InvokeButton("I");
                phase = 6;
            }
            else if (phase == 6 && elapsed > 15.5)
            {
                ScreenCapture.CaptureScreenshot(InventoryOutput);
                phase = 7;
            }
            else if (phase == 7 && elapsed > 17.5)
            {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.isPlaying = false;
                phase = 8;
            }
        }

        private static bool InvokeButton(string value)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                var label = button.GetComponentInChildren<Text>();
                if (button.gameObject.name != value && (label == null || label.text != value)) continue;
                button.onClick.Invoke();
                Debug.Log("Darkfall UI audit: invoked " + value);
                return true;
            }
            Debug.LogWarning("Darkfall UI audit: button not found: " + value);
            return false;
        }
    }
}
#endif
