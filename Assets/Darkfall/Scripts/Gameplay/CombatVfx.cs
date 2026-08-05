using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.Gameplay
{
    public enum ProjectileVisualStyle { Arcane, Cursed, Shard }
    public enum StatusVisualStyle { Freeze, Stun, Fear, Poison, Ward, ArcaneCharge, Dash }

    public static class CombatVfx
    {
        private static readonly Dictionary<string, Sprite[]> Frames = new Dictionary<string, Sprite[]>();
        private static Material lineMaterial;

        public static ProjectileVisualStyle ConfigureProjectile(GameObject root, Vector2 direction, Color tint,
            bool hostile, int sortingOrder)
        {
            var style = hostile
                ? (tint.g > tint.r * .9f || tint.b > tint.r * 1.15f ? ProjectileVisualStyle.Shard : ProjectileVisualStyle.Cursed)
                : ProjectileVisualStyle.Arcane;
            var visual = new GameObject("Animated Projectile Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<IsoVisual>().Initialize(root.transform, .18f, 1040);
            visual.transform.localScale = Vector3.one * (style == ProjectileVisualStyle.Arcane ? .58f : .68f);
            if (style != ProjectileVisualStyle.Arcane)
            {
                var projectedDirection = IsoWorld.ProjectDirection(direction).normalized;
                visual.transform.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(projectedDirection.y, projectedDirection.x) * Mathf.Rad2Deg);
            }
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            var frames = LoadFrames("Projectiles", style);
            renderer.sprite = frames[0];
            visual.AddComponent<SheetAnimation>().Initialize(renderer, frames, 14f, true, false,
                style == ProjectileVisualStyle.Arcane);

            var trail = visual.AddComponent<TrailRenderer>();
            trail.time = .16f;
            trail.startWidth = style == ProjectileVisualStyle.Arcane ? .13f : .105f;
            trail.endWidth = 0f;
            trail.minVertexDistance = .035f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.sharedMaterial = LineMaterial;
            trail.startColor = new Color(tint.r, tint.g, tint.b, .72f);
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trail.sortingOrder = sortingOrder - 1;

            var light = visual.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = tint;
            light.intensity = hostile ? .72f : .95f;
            light.pointLightInnerRadius = .08f;
            light.pointLightOuterRadius = hostile ? 1.15f : 1.45f;
            light.falloffIntensity = .86f;
            light.shadowsEnabled = true;
            light.shadowIntensity = .7f;
            return style;
        }

        public static void SpawnImpact(Vector2 position, ProjectileVisualStyle style, Color tint, float scale = 1f)
        {
            var root = new GameObject(style + " Impact");
            root.transform.position = IsoWorld.Project(position, .12f);
            root.transform.localScale = Vector3.one * scale;
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = IsoWorld.SortingOrder(position, 1040);
            DarkfallRenderMaterials.MakeEmissive(renderer);
            var frames = LoadFrames("Impacts", style);
            renderer.sprite = frames[0];
            root.AddComponent<SheetAnimation>().Initialize(renderer, frames, 13f, false, true, false);
            var light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = tint;
            light.intensity = 1.15f;
            light.pointLightInnerRadius = .05f;
            light.pointLightOuterRadius = 1.7f * scale;
            light.falloffIntensity = .92f;
            root.AddComponent<FadeLight>().Initialize(light, .32f);
        }

        public static void SpawnPulse(Vector2 position, Color color, float radius, float duration = .48f)
        {
            var root = new GameObject("Ability Pulse");
            root.transform.position = IsoWorld.Project(position, .04f);
            var ring = root.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = 72;
            ring.widthMultiplier = .055f;
            ring.numCornerVertices = 3;
            ring.sharedMaterial = LineMaterial;
            ring.sortingOrder = IsoWorld.SortingOrder(position, 1040);
            root.AddComponent<ExpandingRing>().Initialize(ring, color, radius, duration);
            var light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = .82f;
            light.pointLightInnerRadius = .15f;
            light.pointLightOuterRadius = Mathf.Max(1.2f, radius * .7f);
            light.falloffIntensity = .92f;
            root.AddComponent<FadeLight>().Initialize(light, duration);
        }

        public static void SpawnAfterimage(Vector2 position, Sprite sprite, Color color, Vector2 facing)
        {
            if (sprite == null) return;
            var root = new GameObject("Dash Afterimage");
            root.transform.position = IsoWorld.Project(position);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = IsoWorld.SortingOrder(position, 1018);
            DarkfallRenderMaterials.MakeEmissive(renderer);
            root.AddComponent<FadeSprite>().Initialize(renderer, .3f, .94f);
        }

        public static void SpawnStatus(Transform target, StatusVisualStyle status, float duration, float scale = 1f)
        {
            if (target == null || duration <= 0f) return;
            var existing = target.Find("Status VFX · " + status);
            if (existing != null) Object.Destroy(existing.gameObject);

            var root = new GameObject("Status VFX · " + status);
            root.transform.SetParent(target, false);
            var projectileStyle = status == StatusVisualStyle.Freeze ? ProjectileVisualStyle.Shard :
                status == StatusVisualStyle.Stun || status == StatusVisualStyle.Ward ? ProjectileVisualStyle.Arcane :
                ProjectileVisualStyle.Cursed;
            var color = status == StatusVisualStyle.Freeze ? new Color(.28f, .78f, 1f) :
                status == StatusVisualStyle.Stun ? new Color(1f, .78f, .24f) :
                status == StatusVisualStyle.Fear ? new Color(.66f, .22f, .9f) :
                status == StatusVisualStyle.Poison ? new Color(.28f, .78f, .24f) :
                status == StatusVisualStyle.Ward ? new Color(1f, .58f, .16f) :
                status == StatusVisualStyle.Dash ? new Color(.64f, .3f, .92f) : new Color(.78f, .28f, 1f);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadFrames("Impacts", projectileStyle)[0];
            renderer.color = color;
            renderer.sortingOrder = status == StatusVisualStyle.Ward ? 16 : 28;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            var y = status == StatusVisualStyle.Stun || status == StatusVisualStyle.Fear ? 1.05f :
                status == StatusVisualStyle.Freeze ? .28f : .16f;
            root.AddComponent<IsoVisual>().Initialize(target, y, 1060);
            root.transform.localScale = Vector3.one * (.48f * scale);
            root.AddComponent<SheetAnimation>().Initialize(renderer, LoadFrames("Impacts", projectileStyle),
                status == StatusVisualStyle.Stun ? 8f : 10f, true, false, false);
            var light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = .22f;
            light.pointLightOuterRadius = .72f * scale;
            light.falloffIntensity = .9f;
            root.AddComponent<TimedStatusVisual>().Initialize(duration, status == StatusVisualStyle.Stun || status == StatusVisualStyle.Fear);
        }

        public static void ClearNegativeStatuses(Transform target)
        {
            if (target == null) return;
            var negative = new[] { StatusVisualStyle.Freeze, StatusVisualStyle.Stun, StatusVisualStyle.Fear, StatusVisualStyle.Poison };
            foreach (var status in negative)
            {
                var effect = target.Find("Status VFX · " + status);
                if (effect != null) Object.Destroy(effect.gameObject);
            }
        }

        public static void SpawnAura(Transform target, Color color, float duration, float radius)
        {
            if (target == null) return;
            var root = new GameObject("Ability Aura");
            root.transform.SetParent(target, false);
            root.AddComponent<IsoVisual>().Initialize(target, .03f, 970);
            var ring = root.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = 64;
            ring.widthMultiplier = .035f;
            ring.sharedMaterial = LineMaterial;
            ring.sortingOrder = 17;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .42f) * radius);
            }
            ring.startColor = ring.endColor = new Color(color.r, color.g, color.b, .72f);
            root.AddComponent<RotatingAura>().Initialize(duration);
            var light = root.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = .38f;
            light.pointLightOuterRadius = radius * 1.45f;
            root.AddComponent<FadeLight>().Initialize(light, duration, .14f);
        }

        public static void SpawnLightning(Vector2 from, Vector2 to, Color color)
        {
            var root = new GameObject("Arc Lightning");
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 9;
            line.widthMultiplier = .045f;
            line.sharedMaterial = LineMaterial;
            line.sortingOrder = IsoWorld.SortingOrder((from + to) * .5f, 1050);
            from = IsoWorld.Project(from, .22f);
            to = IsoWorld.Project(to, .22f);
            var perpendicular = Vector2.Perpendicular((to - from).normalized);
            for (var i = 0; i < line.positionCount; i++)
            {
                var t = i / (float)(line.positionCount - 1);
                var offset = i == 0 || i == line.positionCount - 1 ? 0f : Mathf.Sin(i * 12.31f) * .13f;
                line.SetPosition(i, Vector2.Lerp(from, to, t) + perpendicular * offset);
            }
            line.startColor = line.endColor = color;
            root.AddComponent<FadeLine>().Initialize(line, .22f);
        }

        public static void PlayScrollCast(Vector2 position, string id)
        {
            var color = id.Contains("fire") || id.Contains("meteor") || id.Contains("rage")
                ? new Color(1f, .24f, .06f) :
                id.Contains("ice") || id.Contains("time") ? new Color(.18f, .7f, 1f) :
                id.Contains("lightning") || id.Contains("invulnerability") ? new Color(1f, .82f, .22f) :
                id.Contains("barrier") || id.Contains("teleport") ? new Color(.3f, .68f, 1f) :
                new Color(.65f, .2f, .82f);
            SpawnPulse(position, color, id.Contains("earthquake") ? 6.2f : 3.2f, .56f);
        }

        private static Sprite[] LoadFrames(string group, ProjectileVisualStyle style)
        {
            var key = group + "/" + style;
            if (Frames.TryGetValue(key, out var cached)) return cached;
            var styleName = style.ToString().ToLowerInvariant();
            var result = new Sprite[4];
            for (var i = 0; i < result.Length; i++)
            {
                var texture = Resources.Load<Texture2D>($"Sprites/VFX/{group}/{styleName}/frame_{i + 1}");
                if (texture == null) { result[i] = RuntimeAssets.Glow; continue; }
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                result[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * .5f, 256f, 0, SpriteMeshType.FullRect);
            }
            Frames[key] = result;
            return result;
        }

        private static Material LineMaterial => lineMaterial != null ? lineMaterial : lineMaterial =
            new Material(Shader.Find("Sprites/Default")) { name = "Darkfall VFX Line", hideFlags = HideFlags.DontSave };
    }

    internal sealed class SheetAnimation : MonoBehaviour
    {
        private SpriteRenderer target;
        private Sprite[] frames;
        private float framesPerSecond;
        private bool loop;
        private bool destroyAtEnd;
        private bool rotate;
        private float startedAt;

        public void Initialize(SpriteRenderer renderer, Sprite[] sprites, float fps, bool shouldLoop, bool destroy,
            bool shouldRotate)
        {
            target = renderer; frames = sprites; framesPerSecond = fps; loop = shouldLoop;
            destroyAtEnd = destroy; rotate = shouldRotate; startedAt = Time.time;
        }

        private void Update()
        {
            if (target == null || frames == null || frames.Length == 0) return;
            var raw = Mathf.FloorToInt((Time.time - startedAt) * framesPerSecond);
            if (!loop && raw >= frames.Length)
            {
                if (destroyAtEnd) Destroy(gameObject);
                else target.sprite = frames[frames.Length - 1];
                return;
            }
            target.sprite = frames[loop ? raw % frames.Length : Mathf.Min(raw, frames.Length - 1)];
            if (rotate) transform.Rotate(0, 0, 45f * Time.deltaTime);
        }
    }

    internal sealed class FadeLight : MonoBehaviour
    {
        private Light2D target;
        private float initial;
        private float duration;
        private float minimum;
        private float startedAt;
        public void Initialize(Light2D light, float seconds, float min = 0f)
        { target = light; initial = light.intensity; duration = seconds; minimum = min; startedAt = Time.time; }
        private void Update()
        {
            if (target == null) return;
            var t = Mathf.Clamp01((Time.time - startedAt) / duration);
            target.intensity = Mathf.Lerp(initial, minimum, t);
        }
    }

    internal sealed class ExpandingRing : MonoBehaviour
    {
        private LineRenderer line;
        private Color color;
        private float radius;
        private float duration;
        private float startedAt;
        public void Initialize(LineRenderer target, Color tint, float finalRadius, float seconds)
        { line = target; color = tint; radius = finalRadius; duration = seconds; startedAt = Time.time; }
        private void Update()
        {
            var t = Mathf.Clamp01((Time.time - startedAt) / duration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .5f) * radius * eased);
            }
            line.startColor = line.endColor = new Color(color.r, color.g, color.b, (1f - t) * .85f);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    internal sealed class FadeSprite : MonoBehaviour
    {
        private SpriteRenderer target; private float duration; private float alpha; private float startedAt;
        public void Initialize(SpriteRenderer sprite, float seconds, float startAlpha)
        { target = sprite; duration = seconds; alpha = startAlpha; startedAt = Time.time; }
        private void Update()
        {
            var t = Mathf.Clamp01((Time.time - startedAt) / duration);
            if (target != null) target.color = new Color(target.color.r, target.color.g, target.color.b, alpha * (1f - t));
            if (t >= 1f) Destroy(gameObject);
        }
    }

    internal sealed class TimedStatusVisual : MonoBehaviour
    {
        private float expiresAt;
        private bool bob;
        private Vector3 origin;
        public void Initialize(float duration, bool shouldBob)
        { expiresAt = Time.time + duration; bob = shouldBob; origin = transform.localPosition; }
        private void Update()
        {
            if (bob) transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * 7f) * .055f);
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }

    internal sealed class FadeLine : MonoBehaviour
    {
        private LineRenderer line; private float duration; private float startedAt;
        public void Initialize(LineRenderer target, float seconds) { line = target; duration = seconds; startedAt = Time.time; }
        private void Update()
        {
            var t = Mathf.Clamp01((Time.time - startedAt) / duration);
            if (line != null)
            {
                var start = line.startColor; var end = line.endColor;
                start.a = end.a = 1f - t; line.startColor = start; line.endColor = end;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }

    internal sealed class RotatingAura : MonoBehaviour
    {
        private float expiresAt;
        public void Initialize(float duration) => expiresAt = Time.time + duration;
        private void Update()
        {
            transform.Rotate(0, 0, 38f * Time.deltaTime);
            var pulse = .96f + Mathf.Sin(Time.time * 7f) * .04f;
            transform.localScale = Vector3.one * pulse;
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
