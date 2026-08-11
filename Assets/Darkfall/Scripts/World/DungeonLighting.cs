using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.World
{
    public sealed class DungeonLighting : MonoBehaviour
    {
        public void Build(DungeonData dungeon, PlayerController player, DungeonVisualProfile profile = null)
        {
            CreateGlobalAmbient(profile ?? DungeonVisualProfile.ForDepth(1));

            for (var i = 0; i < dungeon.LightSources.Count; i++)
            {
                var source = dungeon.LightSources[i];
                var worldLight = CreateOccludedWorldLight(dungeon, source, i);
                worldLight.gameObject.AddComponent<UrpLightFlicker>().Initialize(
                    worldLight, source.Flicker, dungeon, source.Position);
            }

            var playerLight = new GameObject("Nox Player Freeform Light");
            playerLight.transform.SetParent(transform, false);
            playerLight.AddComponent<IsoVisual>().Initialize(player.transform, .12f, 900);
            playerLight.AddComponent<NoxPlayerFreeformLight>().Initialize(dungeon, player);
        }

        private void CreateGlobalAmbient(DungeonVisualProfile profile)
        {
            var previous = GameObject.Find("Black Global Ambient");
            if (previous != null)
            {
                var previousLight = previous.GetComponent<Light2D>();
                if (previousLight != null) previousLight.enabled = false;
            }
            var ambientObject = new GameObject("Black Global Ambient");
            ambientObject.transform.SetParent(transform, false);
            var ambient = ambientObject.AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.color = Color.Lerp(new Color(.40f, .41f, .44f), profile.WallTint, .22f);
            ambient.intensity = profile.AmbientIntensity;
            // Ambient establishes the readable darkness floor. Occlusion belongs to the Nox-style
            // player lobe and authored point lights below; allowing a closed contour to shadow the
            // global fill turns an entire room into a black slab and creates the "floating wall" band.
            ambient.shadowsEnabled = false;
        }

        private Light2D CreateOccludedWorldLight(DungeonData dungeon, DungeonLightSource source, int index)
        {
            var lightObject = new GameObject($"Shadowed World Light {index}");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = IsoWorld.Project(source.Position, .18f);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(source.Color.r, source.Color.g, source.Color.b, 1f);
            light.intensity = 1.18f * Mathf.Lerp(.72f, 1f, source.Color.a);
            light.pointLightInnerRadius = Mathf.Min(.65f, source.Radius * .14f);
            light.pointLightOuterRadius = source.Radius;
            light.falloffIntensity = .78f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            light.shadowsEnabled = true;
            light.shadowIntensity = .78f;
            light.shadowSoftness = 1f;
            return light;
        }

        internal static Light2D ConfigureFreeformLight(GameObject target, Color color, float intensity, float falloff)
        {
            var light = target.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;
            light.color = new Color(color.r, color.g, color.b, 1f);
            light.intensity = intensity * Mathf.Lerp(.7f, 1f, color.a);
            light.shapeLightFalloffSize = falloff;
            light.falloffIntensity = .82f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            light.shadowsEnabled = true;
            light.shadowIntensity = .70f;
            light.shadowSoftness = 1f;
            return light;
        }

        internal static void SmoothCircularDistances(float[] source, float[] destination, float leakTolerance)
        {
            var count = source.Length;
            for (var i = 0; i < count; i++)
            {
                var previous2 = source[(i - 2 + count) % count];
                var previous = source[(i - 1 + count) % count];
                var current = source[i];
                var next = source[(i + 1) % count];
                var next2 = source[(i + 2) % count];
                var average = (previous2 + previous * 2f + current * 3f + next * 2f + next2) / 9f;
                destination[i] = Mathf.Max(.08f, Mathf.Min(current + leakTolerance, average));
            }
        }

        internal static float TraceDistance(DungeonData dungeon, Vector2 origin, Vector2 direction,
            float maximum, out bool blocked)
        {
            var originX = Mathf.FloorToInt(origin.x);
            var originY = Mathf.FloorToInt(origin.y);
            const float step = .065f;
            for (var distance = step; distance <= maximum; distance += step)
            {
                var sample = origin + direction * distance;
                var x = Mathf.FloorToInt(sample.x);
                var y = Mathf.FloorToInt(sample.y);
                if ((x != originX || y != originY) && dungeon.BlocksVision(x, y))
                {
                    blocked = true;
                    return distance;
                }
            }
            blocked = false;
            return maximum;
        }

        internal static float PlayerVisionRadius(Vector2 facing, Vector2 direction,
            float forwardRadius = 10.6f, float rearRadius = 3.65f)
        {
            var dot = Mathf.Clamp(Vector2.Dot(facing.normalized, direction.normalized), -1f, 1f);
            var angularDistance = Mathf.Acos(dot);
            // A broad superellipse-like lobe reads as peripheral vision, not a flashlight cone.
            var forwardWeight = Mathf.Exp(-Mathf.Pow(angularDistance / 1.58f, 2.15f));
            return Mathf.Lerp(rearRadius, forwardRadius, forwardWeight);
        }
    }

    internal sealed class NoxPlayerFreeformLight : MonoBehaviour
    {
        private const int RayCount = 72;
        private const float ShapeRefreshInterval = .065f;
        private PlayerController player;
        private DungeonData dungeon;
        private Light2D outerLight;
        private Light2D nearLight;
        private Light2D coreLight;
        private readonly Vector3[] outerPath = new Vector3[RayCount];
        private readonly Vector3[] nearPath = new Vector3[RayCount];
        private readonly float[] rawDistances = new float[RayCount];
        private readonly float[] clippedDistances = new float[RayCount];
        private readonly Vector2[] directions = new Vector2[RayCount];
        private float nextShapeRefresh;

        public void Initialize(DungeonData data, PlayerController target)
        {
            player = target;
            dungeon = data;
            outerLight = DungeonLighting.ConfigureFreeformLight(gameObject,
                new Color(.72f, .67f, .58f, 1f), .34f, 1.35f);
            var nearObject = new GameObject("Near Directional Light");
            nearObject.transform.SetParent(transform, false);
            nearLight = DungeonLighting.ConfigureFreeformLight(nearObject,
                new Color(.82f, .73f, .60f, 1f), .52f, 1.05f);

            var coreObject = new GameObject("Warm Light Core");
            coreObject.transform.SetParent(transform, false);
            coreLight = coreObject.AddComponent<Light2D>();
            coreLight.lightType = Light2D.LightType.Point;
            coreLight.color = new Color(1f, .73f, .43f, 1f);
            coreLight.intensity = .20f;
            coreLight.pointLightInnerRadius = .10f;
            coreLight.pointLightOuterRadius = .92f;
            coreLight.falloffIntensity = .92f;
            coreLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            // Even the small cursor halo must stop at a wall when the player hugs its edge.
            coreLight.shadowsEnabled = true;
            coreLight.shadowIntensity = .84f;
            coreLight.shadowSoftness = .82f;
            BuildOccludedVisionShape();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            if (Time.unscaledTime < nextShapeRefresh) return;
            nextShapeRefresh = Time.unscaledTime + ShapeRefreshInterval;
            BuildOccludedVisionShape();
        }

        private void BuildOccludedVisionShape()
        {
            if (dungeon == null || player == null) return;
            var origin = (Vector2)player.transform.position;
            var facing = player.FacingDirection.sqrMagnitude > .001f
                ? player.FacingDirection.normalized
                : Vector2.right;

            // The polygon is rebuilt from real dungeon visibility instead of being allowed to
            // wash over an architectural sprite. Both layers share the same wall hit, then the
            // near layer is shortened again to produce distance-dependent illumination.
            for (var ray = 0; ray < RayCount; ray++)
            {
                var angle = ray * Mathf.PI * 2f / RayCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                directions[ray] = direction;
                var maximum = DungeonLighting.PlayerVisionRadius(facing, direction, 10.1f, 3.35f);
                var distance = DungeonLighting.TraceDistance(dungeon, origin, direction, maximum, out var blocked);
                rawDistances[ray] = blocked ? Mathf.Max(.12f, distance - .11f) : distance;
            }
            // Tiny angular smoothing removes saw teeth without leaking the beam around corners.
            DungeonLighting.SmoothCircularDistances(rawDistances, clippedDistances, .035f);
            for (var ray = 0; ray < RayCount; ray++)
            {
                var direction = directions[ray];
                var outerDistance = clippedDistances[ray];
                var nearMaximum = DungeonLighting.PlayerVisionRadius(facing, direction, 5.45f, 2.15f);
                var nearDistance = Mathf.Min(outerDistance, nearMaximum);
                outerPath[ray] = IsoWorld.ProjectDirection(direction * outerDistance);
                nearPath[ray] = IsoWorld.ProjectDirection(direction * nearDistance);
            }
            outerLight.SetShapePath(outerPath);
            nearLight.SetShapePath(nearPath);
        }
    }

    internal sealed class UrpLightFlicker : MonoBehaviour
    {
        private Light2D light2D;
        private float baseIntensity;
        private float amount;
        private float seed;
        private DungeonData dungeon;
        private Vector2 sourcePosition;
        private float visibility;

        public void Initialize(Light2D target, float flickerAmount, DungeonData data, Vector2 position)
        {
            light2D = target;
            baseIntensity = target.intensity;
            amount = Mathf.Clamp(flickerAmount, 0, .22f);
            dungeon = data;
            sourcePosition = position;
            seed = Mathf.Abs(transform.position.x * 9.31f + transform.position.y * 5.77f + GetInstanceID());
            light2D.intensity = 0;
        }

        private void Update()
        {
            if (light2D == null) return;
            var slow = Mathf.PerlinNoise(seed, Time.time * 6.4f) * 2f - 1f;
            var fast = Mathf.PerlinNoise(seed + 17.3f, Time.time * 13.7f) * 2f - 1f;
            var noise = slow * .72f + fast * .28f;
            var x = Mathf.FloorToInt(sourcePosition.x);
            var y = Mathf.FloorToInt(sourcePosition.y);
            var targetVisibility = dungeon != null && dungeon.IsVisible(x, y) ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 5.5f);
            light2D.intensity = baseIntensity * (1f + noise * amount) * visibility;
        }
    }
}
