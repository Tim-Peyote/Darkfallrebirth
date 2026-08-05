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
            var ambientObject = new GameObject("Black Global Ambient");
            ambientObject.transform.SetParent(transform, false);
            var ambient = ambientObject.AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.color = Color.Lerp(new Color(.055f, .06f, .07f), profile.WallTint, .18f);
            ambient.intensity = .052f;
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
            light.falloffIntensity = .48f;
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
        private const int RayCount = 96;
        private PlayerController player;
        private Light2D outerLight;
        private Light2D coreLight;
        private readonly Vector3[] outerPath = new Vector3[RayCount];

        public void Initialize(DungeonData data, PlayerController target)
        {
            player = target;
            outerLight = DungeonLighting.ConfigureFreeformLight(gameObject,
                new Color(.78f, .70f, .58f, 1f), 1.34f, 3.8f);
            BuildConvexVisionShape();

            var coreObject = new GameObject("Warm Light Core");
            coreObject.transform.SetParent(transform, false);
            coreLight = coreObject.AddComponent<Light2D>();
            coreLight.lightType = Light2D.LightType.Point;
            coreLight.color = new Color(1f, .73f, .43f, 1f);
            coreLight.intensity = .34f;
            coreLight.pointLightInnerRadius = .18f;
            coreLight.pointLightOuterRadius = 1.45f;
            coreLight.falloffIntensity = .84f;
            coreLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            // The outer light already receives engine shadow casters. A second large shadowed core
            // produced doubled seams; this tiny local glow is intentionally too small to cross walls.
            coreLight.shadowsEnabled = false;
            UpdateFacing();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            var logicalFacing = player.FacingDirection.sqrMagnitude > .001f ? player.FacingDirection.normalized : Vector2.right;
            var facing = IsoWorld.ProjectDirection(logicalFacing).normalized;
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
        }

        private void BuildConvexVisionShape()
        {
            // A shifted ellipse is always convex, so URP can triangulate it deterministically.
            // Walls and props clip it through ShadowCaster2D instead of deforming the polygon.
            const float rear = 3.65f;
            const float forward = 10.6f;
            const float halfWidth = 6.15f;
            var center = (forward - rear) * .5f;
            var halfLength = (forward + rear) * .5f;
            for (var ray = 0; ray < RayCount; ray++)
            {
                var angle = ray * Mathf.PI * 2f / RayCount;
                outerPath[ray] = new Vector3(center + Mathf.Cos(angle) * halfLength,
                    Mathf.Sin(angle) * halfWidth, 0);
            }
            outerLight.SetShapePath(outerPath);
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
