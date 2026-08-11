#if UNITY_EDITOR
using Darkfall.Core;
using UnityEditor;
using UnityEngine;

namespace Darkfall.Editor
{
    [InitializeOnLoad]
    public static class DarkfallReleaseSmokeRunner
    {
        private const string RequestKey = "Darkfall.ReleaseSmoke.Requested";

        static DarkfallReleaseSmokeRunner()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Darkfall/Validation/Run Release Smoke")]
        public static void Run()
        {
            EditorPrefs.SetBool(RequestKey, true);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!EditorPrefs.GetBool(RequestKey, false)) return;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += EnterPlayModeForRequestedSmoke;
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += StartRequestedSmoke;
        }

        private static void EnterPlayModeForRequestedSmoke()
        {
            if (!EditorApplication.isPlaying && EditorPrefs.GetBool(RequestKey, false))
                EditorApplication.isPlaying = true;
        }

        private static void StartRequestedSmoke()
        {
            if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(RequestKey, false)) return;
            EditorPrefs.DeleteKey(RequestKey);
            if (GameManager.Instance == null)
            {
                Debug.LogError("DARKFALL_RELEASE_SMOKE_FAIL: GameManager was not bootstrapped in Play Mode");
                EditorApplication.isPlaying = false;
                return;
            }
            GameManager.Instance.BeginReleaseSmoke();
        }

        public static void RunBatch()
        {
            Run();
        }
    }
}
#endif
