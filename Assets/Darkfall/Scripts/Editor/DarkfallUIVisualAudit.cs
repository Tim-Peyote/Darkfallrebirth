#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Darkfall.Core;
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
        private const string OutputDirectory = "work/visual-audit/ui";
        private const string ReportOutput = "work/visual-audit/ui/layout-report.json";
        private const string ActiveKey = "Darkfall.UIVisualAudit.Active";
        private const int PageCount = 7;
        private static readonly AuditResolution[] Resolutions =
        {
            new AuditResolution("minimum", 1600, 900),
            new AuditResolution("16x9", 1920, 1080),
            new AuditResolution("16x10", 1680, 1050),
            new AuditResolution("21x9", 2560, 1080)
        };
        private static readonly List<LayoutIssue> Issues = new List<LayoutIssue>();
        private static double readyAt;
        private static int page;
        private static int resolution;
        private static bool resolutionApplied;

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
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
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
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
                return;
            }
            EditorApplication.update -= TryStart;
            EditorApplication.update += TryStart;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BeginAudit();
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                if (SessionState.GetBool(ActiveKey, false))
                {
                    EditorApplication.update -= TryStart;
                    EditorApplication.update += TryStart;
                }
                else EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            var target = Resolutions[resolution];
            if (!resolutionApplied)
            {
                resolutionApplied = true;
                readyAt = EditorApplication.timeSinceStartup + .35;
                return;
            }

            Directory.CreateDirectory(OutputDirectory);
            var pageName = PageName(page);
            var texture = CaptureFrame(pageName, target);
            var screenshot = Path.Combine(OutputDirectory,
                $"{pageName}-{target.Name}-{texture.width}x{texture.height}.png");
            File.WriteAllBytes(screenshot, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            resolution++;
            resolutionApplied = false;
            if (resolution < Resolutions.Length)
            {
                readyAt = EditorApplication.timeSinceStartup + .35;
                return;
            }

            resolution = 0;
            page++;
            if (page >= PageCount)
            {
                WriteReport();
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.isPlaying = false;
                return;
            }
            TransitionToPage(page);
            readyAt = EditorApplication.timeSinceStartup + (page == 2 || page == 6 ? 4.5 : 1.4);
        }

        private static void BeginAudit()
        {
            Directory.CreateDirectory(OutputDirectory);
            Issues.Clear();
            page = 0;
            resolution = 0;
            resolutionApplied = false;
            readyAt = EditorApplication.timeSinceStartup + 2.5;
        }

        private static string PageName(int value) => value switch
        {
            0 => "main-menu", 1 => "hero-select", 2 => "hud", 3 => "inventory",
            4 => "pause", 5 => "shop", _ => "developer"
        };

        private static void TransitionToPage(int targetPage)
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            var runtime = UnityEngine.Object.FindFirstObjectByType<RuntimeUI>();
            if (targetPage == 1) runtime?.SendMessage("ShowHeroSelect", SendMessageOptions.DontRequireReceiver);
            else if (targetPage == 2) manager?.StartRun();
            else if (targetPage == 3) InventoryUI.Instance?.Toggle();
            else if (targetPage == 4)
            {
                InventoryUI.Instance?.Toggle();
                manager?.TogglePause();
            }
            else if (targetPage == 5)
            {
                manager?.Resume();
                manager?.DeveloperOpenShop();
            }
            else if (targetPage == 6)
            {
                manager?.ContinueAfterShop();
                var consoles = UnityEngine.Object.FindObjectsByType<DeveloperConsoleUI>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (consoles.Length > 0) consoles[0].SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            }
            Debug.Log($"Darkfall UI audit: transitioned to {PageName(targetPage)}");
        }

        private static void AuditLayout(string pageName, AuditResolution requested)
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var rects = new List<Rect>();
            var names = new List<string>();
            var transforms = new List<Transform>();
            foreach (var button in buttons)
            {
                if (!button.isActiveAndEnabled) continue;
                if (!BelongsToPage(button.transform, pageName)) continue;
                var rect = ScreenRect(button.GetComponent<RectTransform>());
                var label = button.GetComponentInChildren<Text>()?.text ?? button.name;
                if (rect.xMin < -2 || rect.yMin < -2 || rect.xMax > requested.Width + 2 || rect.yMax > requested.Height + 2)
                    AddIssue(pageName, requested, label, "button_outside_view", rect);
                if (rect.width < 40 || rect.height < 40)
                    AddIssue(pageName, requested, label, "hit_area_below_40px", rect);
                rects.Add(rect);
                names.Add(label);
                transforms.Add(button.transform);
            }
            for (var i = 0; i < rects.Count; i++)
            for (var j = i + 1; j < rects.Count; j++)
            {
                if (transforms[i].IsChildOf(transforms[j]) || transforms[j].IsChildOf(transforms[i])) continue;
                var overlap = Intersection(rects[i], rects[j]);
                if (overlap.width < 4 || overlap.height < 4) continue;
                var smaller = Mathf.Min(rects[i].width * rects[i].height, rects[j].width * rects[j].height);
                if (overlap.width * overlap.height > smaller * .18f)
                    AddIssue(pageName, requested, names[i] + " ↔ " + names[j], "interactive_overlap", overlap);
            }
        }

        private static bool BelongsToPage(Transform element, string pageName)
        {
            var rootName = pageName switch
            {
                "main-menu" => "Title Page",
                "hero-select" => "Hero Select Page",
                "hud" => "HUD",
                "inventory" => "Inventory Overlay",
                "pause" => "Pause",
                "shop" => "Sanctuary Shop",
                _ => "Developer Console"
            };
            for (var current = element; current != null; current = current.parent)
                if (current.name == rootName) return true;
            return false;
        }

        private static Texture2D CaptureFrame(string pageName, AuditResolution requested)
        {
            var camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null) throw new InvalidOperationException("UI audit requires an active camera.");
            var renderTexture = RenderTexture.GetTemporary(requested.Width, requested.Height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var overrideSorting = new bool[canvases.Length];
            var sortingOrders = new int[canvases.Length];
            try
            {
                camera.targetTexture = renderTexture;
                for (var i = 0; i < canvases.Length; i++)
                {
                    modes[i] = canvases[i].renderMode;
                    cameras[i] = canvases[i].worldCamera;
                    distances[i] = canvases[i].planeDistance;
                    overrideSorting[i] = canvases[i].overrideSorting;
                    sortingOrders[i] = canvases[i].sortingOrder;
                    if (!canvases[i].isRootCanvas) continue;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = Mathf.Max(camera.nearClipPlane + .25f, 1f);
                    canvases[i].overrideSorting = true;
                    canvases[i].sortingOrder = 32760;
                }
                Canvas.ForceUpdateCanvases();
                AuditLayout(pageName, requested);
                camera.Render();
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(requested.Width, requested.Height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, requested.Width, requested.Height), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                for (var i = 0; i < canvases.Length; i++)
                {
                    if (!canvases[i].isRootCanvas) continue;
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                    canvases[i].overrideSorting = overrideSorting[i];
                    canvases[i].sortingOrder = sortingOrders[i];
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static Rect Intersection(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect();
        }

        private static void AddIssue(string pageName, AuditResolution requested, string element, string kind, Rect rect)
        {
            Issues.Add(new LayoutIssue
            {
                Page = pageName, Resolution = requested.Name, Element = element, Kind = kind,
                Rect = $"{rect.x:0},{rect.y:0},{rect.width:0},{rect.height:0}"
            });
        }

        private static void WriteReport()
        {
            var report = new LayoutReport
            {
                GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
                CapturedFrames = Resolutions.Length * PageCount,
                IssueCount = Issues.Count,
                Issues = Issues.ToArray()
            };
            File.WriteAllText(ReportOutput, JsonUtility.ToJson(report, true));
            Debug.Log($"Darkfall UI matrix audit finished: {report.CapturedFrames} frames, " +
                      $"{report.IssueCount} layout issue(s). {Path.GetFullPath(ReportOutput)}");
        }

        private readonly struct AuditResolution
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public AuditResolution(string name, int width, int height) { Name = name; Width = width; Height = height; }
        }

        [Serializable]
        private sealed class LayoutReport
        {
            public string GeneratedAtUtc;
            public int CapturedFrames;
            public int IssueCount;
            public LayoutIssue[] Issues;
        }

        [Serializable]
        private sealed class LayoutIssue
        {
            public string Page;
            public string Resolution;
            public string Element;
            public string Kind;
            public string Rect;
        }

    }
}
#endif
