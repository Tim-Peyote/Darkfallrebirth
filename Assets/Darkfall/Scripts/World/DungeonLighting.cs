using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.World
{
    public sealed class DungeonLighting : MonoBehaviour
    {
        public void Build(DungeonData dungeon, PlayerController player, DungeonVisualProfile profile = null)
        {
            var resolvedProfile = profile ?? DungeonVisualProfile.ForDepth(1);
            CreateGlobalAmbient(resolvedProfile);

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
            playerLight.AddComponent<NoxPlayerFreeformLight>().Initialize(dungeon, player, resolvedProfile);
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
            // Unseen space must remain threatening. The player lobe and authored lamps lift the
            // primary light buffer well above this floor; the ambient only preserves a trace of
            // material and silhouette instead of revealing the entire viewport.
            ambient.intensity = Mathf.Clamp(profile.AmbientIntensity - .045f, .205f, .265f);
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
            light.blendStyleIndex = 0;
            // Keep authored fire/magic identity without letting adjacent wall modules alternate
            // between cold blue and hot orange. The source hue is now a restrained accent.
            var neutralWarm = new Color(.94f, .82f, .67f, 1f);
            light.color = Color.Lerp(neutralWarm,
                new Color(source.Color.r, source.Color.g, source.Color.b, 1f), .38f);
            light.intensity = 1.08f * Mathf.Lerp(.82f, 1f, source.Color.a);
            light.pointLightInnerRadius = Mathf.Min(.65f, source.Radius * .14f);
            light.pointLightOuterRadius = source.Radius;
            light.falloffIntensity = .88f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            light.shadowsEnabled = true;
            light.shadowIntensity = .46f;
            light.shadowSoftness = 1f;
            return light;
        }

        internal static Light2D ConfigureFreeformLight(GameObject target, Color color, float intensity,
            float falloff, float falloffIntensity)
        {
            var light = target.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;
            // Lit dungeon sprites use the renderer's primary light buffer. Local values must sit
            // above the global darkness floor; sub-unit values below that floor visually behave
            // like a shadow instead of illumination on this Multiply blend style.
            light.blendStyleIndex = 0;
            light.color = new Color(color.r, color.g, color.b, 1f);
            light.intensity = intensity * Mathf.Lerp(.7f, 1f, color.a);
            light.shapeLightFalloffSize = falloff;
            light.falloffIntensity = falloffIntensity;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            // The polygon itself is ray-clipped against DungeonData.BlocksVision. Enabling URP
            // shadows here applies occlusion a second time and creates long black wedges behind
            // wall sprites, especially at isometric corners.
            light.shadowsEnabled = false;
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

        public static float TraceDistance(DungeonData dungeon, Vector2 origin, Vector2 direction,
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
                if ((x != originX || y != originY) && dungeon.BlocksVision(sample))
                {
                    blocked = true;
                    return distance;
                }
            }
            blocked = false;
            return maximum;
        }

        public static float PlayerVisionRadius(Vector2 facing, Vector2 direction,
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
        private const int RayCount = 192;
        private const float ShapeRefreshInterval = .033f;
        private PlayerController player;
        private DungeonData dungeon;
        private Light2D outerLight;
        private Light2D nearLight;
        private Light2D coreLight;
        private Light2D stableLocalLight;
        private NoxVisibilityCurtain visibilityCurtain;
        private readonly Vector3[] outerPath = new Vector3[RayCount];
        private readonly float[] rawDistances = new float[RayCount];
        private readonly float[] clippedDistances = new float[RayCount];
        private readonly float[] targetOuterDistances = new float[RayCount];
        private readonly float[] displayedOuterDistances = new float[RayCount];
        private readonly Vector2[] directions = new Vector2[RayCount];
        private readonly bool[] blockedRays = new bool[RayCount];
        private float nextShapeRefresh;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private LineRenderer debugContour;
        private Material debugMaterial;
#endif

        public void Initialize(DungeonData data, PlayerController target, DungeonVisualProfile profile)
        {
            player = target;
            dungeon = data;
            // Direction and occlusion are expressed by the darkness curtain. Keeping the broad
            // illumination itself as a stable, low-saturation point light avoids URP rebuilding a
            // large Freeform Light2D while the actor moves — the source of distant flashing and
            // hard colour bands on wall modules.
            // The readable player aura is luminance contrast, not an orange colour wash.
            // Keep the broad pool close to neutral white and reserve warmth for the tiny core.
            outerLight = ConfigureLocalPointLight(gameObject, new Color(1f, .98f, .94f, 1f),
                .78f, 1.75f, 7.15f, false);
            var nearObject = new GameObject("Near Soft Fill Light");
            nearObject.transform.SetParent(transform, false);
            nearLight = ConfigureLocalPointLight(nearObject, new Color(1f, .96f, .90f, 1f),
                .76f, .82f, 3.25f, true);

            var coreObject = new GameObject("Warm Light Core");
            coreObject.transform.SetParent(transform, false);
            coreLight = ConfigureLocalPointLight(coreObject, new Color(1f, .88f, .70f, 1f),
                .80f, .24f, 1.72f, true);

            // Freeform geometry is rebuilt as the actor crosses visibility-cell boundaries. A
            // restrained warm point light prevents the actor's immediate pool from blinking out
            // for a frame while URP retessellates the clipped contour; it is deliberately weak and
            // soft, so the directional wall-clipped lobe still defines exploration.
            var stableObject = new GameObject("Stable Local Light Floor");
            stableObject.transform.SetParent(transform, false);
            stableLocalLight = stableObject.AddComponent<Light2D>();
            stableLocalLight.lightType = Light2D.LightType.Point;
            stableLocalLight.blendStyleIndex = 0;
            stableLocalLight.color = new Color(1f, .92f, .80f, 1f);
            stableLocalLight.intensity = .34f;
            stableLocalLight.pointLightInnerRadius = .12f;
            stableLocalLight.pointLightOuterRadius = 1.65f;
            stableLocalLight.falloffIntensity = .96f;
            stableLocalLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            stableLocalLight.shadowsEnabled = false;

            var curtainObject = new GameObject("Nox Visibility Darkness");
            curtainObject.transform.SetParent(transform, false);
            visibilityCurtain = curtainObject.AddComponent<NoxVisibilityCurtain>();
            visibilityCurtain.Initialize(dungeon, player, profile);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDebugContour();
#endif
            RefreshOcclusionTargets(true);
            ApplySmoothedShape(1f);
        }

        private void LateUpdate()
        {
            if (player == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F9))
            {
                debugContour.enabled = !debugContour.enabled;
                Debug.Log("Darkfall lighting debug: " + (debugContour.enabled ? "ON" : "OFF"));
            }
#endif
            if (Time.unscaledTime >= nextShapeRefresh)
            {
                nextShapeRefresh = Time.unscaledTime + ShapeRefreshInterval;
                RefreshOcclusionTargets(false);
            }
            // Only the inexpensive darkness mesh is animated now. Exponential interpolation
            // prevents cell-sized jumps when a ray changes its hit wall.
            ApplySmoothedShape(1f - Mathf.Exp(-4.8f * Time.unscaledDeltaTime));
        }

        private bool RefreshOcclusionTargets(bool snap)
        {
            if (dungeon == null || player == null) return false;
            var origin = (Vector2)player.transform.position;
            var facing = player.FacingDirection.sqrMagnitude > .001f
                ? player.FacingDirection.normalized
                : Vector2.right;

            // The polygon is rebuilt from real dungeon visibility instead of being allowed to
            // wash over an architectural sprite. Both layers share the same wall hit, then the
            // near layer is shortened again to produce distance-dependent illumination.
            var changed = false;
            for (var ray = 0; ray < RayCount; ray++)
            {
                // IsoWorld.Project has a negative determinant: it mirrors winding. Generate the
                // logical contour clockwise so the projected Freeform Light2D path remains CCW,
                // as required by URP's shape tessellator. The opposite winding makes the falloff
                // face outwards and can visually turn a light lobe into a dark mask.
                var angle = -ray * Mathf.PI * 2f / RayCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                directions[ray] = direction;
                var maximum = DungeonLighting.PlayerVisionRadius(facing, direction, 10.1f, 3.35f);
                var distance = DungeonLighting.TraceDistance(dungeon, origin, direction, maximum, out var blocked);
                blockedRays[ray] = blocked;
                rawDistances[ray] = blocked ? Mathf.Max(.12f, distance - .11f) : distance;
            }
            // Tiny angular smoothing removes saw teeth without leaking the beam around corners.
            DungeonLighting.SmoothCircularDistances(rawDistances, clippedDistances, .035f);
            for (var ray = 0; ray < RayCount; ray++)
            {
                var direction = directions[ray];
                var outerDistance = clippedDistances[ray];
                targetOuterDistances[ray] = outerDistance;
                if (Mathf.Abs(displayedOuterDistances[ray] - outerDistance) > .012f) changed = true;
                if (!snap) continue;
                displayedOuterDistances[ray] = outerDistance;
            }
            return changed;
        }

        private void ApplySmoothedShape(float blend)
        {
            for (var ray = 0; ray < RayCount; ray++)
            {
                displayedOuterDistances[ray] = Mathf.Lerp(displayedOuterDistances[ray], targetOuterDistances[ray], blend);
                outerPath[ray] = IsoWorld.ProjectDirection(directions[ray] * displayedOuterDistances[ray]);
            }
            visibilityCurtain?.SetContour(directions, displayedOuterDistances, blockedRays);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugContour != null)
            {
                debugContour.positionCount = RayCount;
                debugContour.SetPositions(outerPath);
            }
#endif
        }

        private static Light2D ConfigureLocalPointLight(GameObject target, Color color, float intensity,
            float innerRadius, float outerRadius, bool castShadows)
        {
            var light = target.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = innerRadius;
            light.pointLightOuterRadius = outerRadius;
            light.falloffIntensity = .92f;
            light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
            light.shadowsEnabled = castShadows;
            light.shadowIntensity = .38f;
            light.shadowSoftness = 1f;
            return light;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void BuildDebugContour()
        {
            var debugObject = new GameObject("Lighting Debug · F9");
            debugObject.transform.SetParent(transform, false);
            debugContour = debugObject.AddComponent<LineRenderer>();
            debugContour.useWorldSpace = false;
            debugContour.loop = true;
            debugContour.widthMultiplier = .028f;
            debugContour.numCornerVertices = 2;
            debugContour.startColor = new Color(.25f, 1f, .55f, .92f);
            debugContour.endColor = new Color(1f, .72f, .18f, .92f);
            debugContour.sortingOrder = 31000;
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            debugMaterial = new Material(shader) { color = Color.white };
            debugContour.sharedMaterial = debugMaterial;
            debugContour.enabled = false;
        }

        private void OnDestroy()
        {
            if (debugMaterial != null) Destroy(debugMaterial);
        }
#endif
    }

    /// <summary>
    /// Light2D can brighten the visible area, but it cannot make the already ambient-lit world
    /// outside that area unknown. This transparent radial curtain uses the exact same wall-clipped
    /// contour as the player light and darkens every world renderer (actors and decoration included)
    /// while remaining below the screen-space HUD.
    /// </summary>
    internal sealed class NoxVisibilityCurtain : MonoBehaviour
    {
        private const int CurtainRingCount = 8;
        private const float OuterDistance = 42f;
        private Mesh curtainMesh;
        private Material curtainMaterial;
        private Vector3[] curtainVertices;
        private Color32[] curtainColors;
        private NoxAtmosphericParticles atmosphericParticles;

        public void Initialize(DungeonData dungeon, PlayerController player, DungeonVisualProfile profile)
        {
            curtainMesh = new Mesh { name = "Nox Visibility Curtain", hideFlags = HideFlags.DontSave };
            curtainMesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = curtainMesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            curtainMaterial = new Material(shader)
            {
                name = "Nox Visibility Curtain Material",
                hideFlags = HideFlags.DontSave,
                mainTexture = Texture2D.whiteTexture,
                color = Color.white
            };
            renderer.sharedMaterial = curtainMaterial;
            renderer.sortingOrder = 30000;

            var atmosphereObject = new GameObject("Physical Dungeon Atmosphere");
            atmosphereObject.transform.SetParent(transform, false);
            atmosphericParticles = atmosphereObject.AddComponent<NoxAtmosphericParticles>();
            atmosphericParticles.Initialize(dungeon, player, profile);
        }

        public void SetContour(Vector2[] directions, float[] distances, bool[] blockedRays)
        {
            if (curtainMesh == null || directions == null || distances == null || directions.Length == 0) return;
            var rayCount = Mathf.Min(directions.Length, distances.Length);
            if (curtainVertices == null || curtainVertices.Length != rayCount * CurtainRingCount)
            {
                BuildRadialMesh(curtainMesh, rayCount, CurtainRingCount,
                    out curtainVertices, out curtainColors, false);
            }

            for (var ray = 0; ray < rayCount; ray++)
            {
                var direction = directions[ray];
                var boundary = distances[ray];
                // A single continuous profile is intentional. Switching profiles whenever a
                // sampling ray barely touches a wall made the edge flash behind the player.
                // The broad transparent lead-in also removes the knife-sharp base at walls.
                SetCurtainVertex(ray, 0, direction, boundary - 1.55f, 0);
                SetCurtainVertex(ray, 1, direction, boundary - 1.05f, 1);
                SetCurtainVertex(ray, 2, direction, boundary - .55f, 7);
                SetCurtainVertex(ray, 3, direction, boundary + .05f, 24);
                SetCurtainVertex(ray, 4, direction, boundary + .78f, 64);
                SetCurtainVertex(ray, 5, direction, boundary + 1.65f, 122);
                SetCurtainVertex(ray, 6, direction, boundary + 2.8f, 181);
                // The far curtain is the "explored but not currently visible" state. Unknown
                // cells receive an additional near-black layer from FogOfWarView.
                SetCurtainVertex(ray, 7, direction, OuterDistance, 216);
            }
            curtainMesh.vertices = curtainVertices;
            curtainMesh.colors32 = curtainColors;
            curtainMesh.RecalculateBounds();
            atmosphericParticles?.SetVisibilityContour(directions, distances);
        }

        private void SetCurtainVertex(int ray, int ring, Vector2 direction, float distance, byte alpha)
        {
            var index = ray * CurtainRingCount + ring;
            curtainVertices[index] = IsoWorld.ProjectDirection(direction * Mathf.Max(.08f, distance));
            curtainColors[index] = new Color32(0, 0, 0, alpha);
        }

        private static void BuildRadialMesh(Mesh target, int rayCount, int ringCount,
            out Vector3[] targetVertices, out Color32[] targetColors, bool worldUv)
        {
            var vertexCount = rayCount * ringCount;
            targetVertices = new Vector3[vertexCount];
            targetColors = new Color32[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[rayCount * (ringCount - 1) * 6];
            for (var ray = 0; ray < rayCount; ray++)
            {
                var next = (ray + 1) % rayCount;
                for (var ring = 0; ring < ringCount; ring++)
                    uv[ray * ringCount + ring] = worldUv
                        ? new Vector2(ray / (float)rayCount * 3f, ring / (float)(ringCount - 1) * 3f)
                        : Vector2.one * .5f;
                for (var ring = 0; ring < ringCount - 1; ring++)
                {
                    var triangle = (ray * (ringCount - 1) + ring) * 6;
                    var a = ray * ringCount + ring;
                    var b = next * ringCount + ring;
                    var c = ray * ringCount + ring + 1;
                    var d = next * ringCount + ring + 1;
                    triangles[triangle] = a;
                    triangles[triangle + 1] = b;
                    triangles[triangle + 2] = c;
                    triangles[triangle + 3] = c;
                    triangles[triangle + 4] = b;
                    triangles[triangle + 5] = d;
                }
            }
            target.Clear();
            target.vertices = targetVertices;
            target.colors32 = targetColors;
            target.uv = uv;
            target.triangles = triangles;
        }

        private void OnDestroy()
        {
            if (curtainMesh != null) Destroy(curtainMesh);
            if (curtainMaterial != null) Destroy(curtainMaterial);
        }
    }

    /// <summary>
    /// Sparse, engine-simulated underground air. Particles are emitted only inside the current
    /// wall-clipped visibility polygon, advected by ParticleSystem noise, and culled when they
    /// reach architecture. This keeps atmosphere volumetric and moving instead of painting a
    /// translucent screen-space lobe over the level.
    /// </summary>
    internal sealed class NoxAtmosphericParticles : MonoBehaviour
    {
        private const int MaxParticles = 160;
        private DungeonData dungeon;
        private PlayerController player;
        private ParticleSystem particles;
        private ParticleSystem.Particle[] buffer;
        private Texture2D smokeTexture;
        private Material smokeMaterial;
        private Vector2[] contourDirections;
        private float[] contourDistances;
        private Color tint;
        private float density;
        private Vector2 drift;
        private float emissionAccumulator;

        public void Initialize(DungeonData data, PlayerController target, DungeonVisualProfile profile)
        {
            dungeon = data;
            player = target;
            tint = profile.AtmosphereTint;
            density = Mathf.Clamp(profile.AtmosphereDensity, .5f, 1.7f);
            drift = profile.AtmosphereDrift * 13f;
            buffer = new ParticleSystem.Particle[MaxParticles];

            particles = gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            // World space is essential: the atmosphere belongs to the dungeon, not to the
            // player's light rig. Existing wisps must remain anchored while the actor walks.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.86f, 3.65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.separateAxes = true;
            noise.strengthX = .17f * density;
            noise.strengthY = .085f * density;
            noise.strengthZ = 0f;
            noise.frequency = .19f;
            noise.scrollSpeed = .12f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.octaveMultiplier = .45f;
            noise.octaveScale = 2f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(.08f, 0f), new GradientAlphaKey(.88f, .12f),
                    new GradientAlphaKey(.76f, .76f), new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .58f), new Keyframe(.18f, .92f),
                new Keyframe(.64f, 1.16f), new Keyframe(1f, .74f)));
            var rotationOverLifetime = particles.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-.075f, .075f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // IsoVisual adds the player-light rig's depth order to this offset. Eighty places the
            // air above floor, but below actors and wall facades; 29990 made it a HUD-like veil.
            renderer.sortingOrder = 80;
            smokeTexture = CreateSoftSmokeParticle();
            // Sprite-Lit is not a particle shader and silently drops the particle vertex streams
            // on the URP 2D renderer. Use the actual URP particle path; lighting interaction is
            // applied below by dispersing density around visible authored light volumes.
            var shader = Resources.Load<Shader>("Shaders/DungeonAtmosphereParticle") ??
                         Shader.Find("Particles/Standard Unlit") ??
                         Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            smokeMaterial = new Material(shader)
            {
                name = "Physical Dungeon Air Material",
                hideFlags = HideFlags.DontSave,
                mainTexture = smokeTexture,
                color = Color.white
            };
            if (smokeMaterial.HasProperty("_BaseMap")) smokeMaterial.SetTexture("_BaseMap", smokeTexture);
            if (smokeMaterial.HasProperty("_BaseColor")) smokeMaterial.SetColor("_BaseColor", Color.white);
            if (smokeMaterial.HasProperty("_Tint")) smokeMaterial.SetColor("_Tint", Color.white);
            // Runtime-created URP particle materials otherwise keep the shader's opaque defaults
            // on some renderer configurations, which makes the system submit no useful alpha.
            if (smokeMaterial.HasProperty("_Surface")) smokeMaterial.SetFloat("_Surface", 1f);
            if (smokeMaterial.HasProperty("_Blend")) smokeMaterial.SetFloat("_Blend", 0f);
            if (smokeMaterial.HasProperty("_SrcBlend")) smokeMaterial.SetFloat("_SrcBlend", 5f);
            if (smokeMaterial.HasProperty("_DstBlend")) smokeMaterial.SetFloat("_DstBlend", 10f);
            if (smokeMaterial.HasProperty("_ZWrite")) smokeMaterial.SetFloat("_ZWrite", 0f);
            // Seed actor-relative shader state before the first render. Large generated floors can
            // otherwise show one frame with the default origin before Update reaches this system.
            smokeMaterial.SetVector("_PlayerPosition", transform.position);
            smokeMaterial.SetFloat("_ClearInner", .32f);
            smokeMaterial.SetFloat("_ClearOuter", 1.55f);
            smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            smokeMaterial.renderQueue = 3000;
            renderer.sharedMaterial = smokeMaterial;
            particles.Play(false);
        }

        public void SetVisibilityContour(Vector2[] directions, float[] distances)
        {
            if (directions == null || distances == null) return;
            var count = Mathf.Min(directions.Length, distances.Length);
            if (contourDirections == null || contourDirections.Length != count)
            {
                contourDirections = new Vector2[count];
                contourDistances = new float[count];
            }
            System.Array.Copy(directions, contourDirections, count);
            System.Array.Copy(distances, contourDistances, count);
        }

        private void Update()
        {
            if (particles == null || player == null || contourDistances == null || contourDistances.Length == 0)
                return;
            // The material cuts the fog per pixel around the actor. Doing this in the shader is
            // important: fading a large particle only from its centre left a translucent disc
            // over the hero even though the simulated cloud itself was world-anchored.
            if (smokeMaterial != null)
            {
                smokeMaterial.SetVector("_PlayerPosition", transform.position);
                smokeMaterial.SetFloat("_ClearInner", .32f);
                smokeMaterial.SetFloat("_ClearOuter", 1.55f);
            }
            emissionAccumulator += Time.unscaledDeltaTime * Mathf.Lerp(8f, 14f,
                Mathf.InverseLerp(.5f, 1.7f, density));
            while (emissionAccumulator >= 1f)
            {
                emissionAccumulator -= 1f;
                EmitVisibleParticle();
            }
            CullParticlesAgainstArchitecture();
        }

        private void EmitVisibleParticle()
        {
            var ray = Random.Range(0, contourDistances.Length);
            var allowed = contourDistances[ray];
            // Corridors often give most rays less than two logical cells before a wall. The old
            // threshold therefore switched atmosphere off exactly at room-to-corridor throats.
            if (allowed < .52f) return;
            // Atmosphere occupies the unexplored volume ahead, not a halo glued to the actor.
            var inner = Mathf.Min(1.45f, allowed * .48f);
            var outer = allowed * .86f;
            if (outer <= inner + .04f) return;
            var distance = Mathf.Lerp(inner, outer, Mathf.Sqrt(Random.value));
            var logicalOffset = contourDirections[ray] * distance;
            var world = (Vector2)player.transform.position + logicalOffset;
            if (IsSafeArrival(world)) return;
            var projectedOffset = (Vector2)IsoWorld.ProjectDirection(logicalOffset);
            var projectedVelocity = (Vector2)IsoWorld.ProjectDirection(drift + Random.insideUnitCircle * .018f);
            var distanceFade = Mathf.InverseLerp(.35f, 7.5f, distance);
            var alpha = Mathf.Lerp(.13f, .34f, distanceFade) * density;
            var emit = new ParticleSystem.EmitParams
            {
                position = (Vector2)transform.position + projectedOffset,
                velocity = projectedVelocity,
                startLifetime = Random.Range(9f, 15f),
                // Mostly small torn wisps with occasional broad banks read as suspended volume;
                // hundreds of equally large billboards collapsed into a flat coloured overlay.
                startSize = Mathf.Lerp(.86f, 3.65f, Mathf.Pow(Random.value, 1.65f)),
                rotation = Random.Range(0f, 360f),
                startColor = new Color(tint.r, tint.g, tint.b, alpha)
            };
            particles.Emit(emit, 1);
        }

        private void CullParticlesAgainstArchitecture()
        {
            // Domain reloads and edit/play transitions can keep the component alive for one frame
            // after its managed buffer was cleared. Never feed a null array into Unity bindings.
            if (particles == null) return;
            if (buffer == null || buffer.Length != MaxParticles)
                buffer = new ParticleSystem.Particle[MaxParticles];
            var count = particles.GetParticles(buffer);
            var playerPosition = (Vector2)player.transform.position;
            var projectedPlayerPosition = (Vector2)transform.position;
            for (var i = 0; i < count; i++)
            {
                var logicalOffset = IsoWorld.UnprojectDirection((Vector2)buffer[i].position - projectedPlayerPosition);
                var distance = logicalOffset.magnitude;
                var allowed = ContourDistance(logicalOffset);
                var world = playerPosition + logicalOffset;
                if (distance > allowed * .94f || dungeon.BlocksVision(world) || IsSafeArrival(world))
                    buffer[i].remainingLifetime = 0f;
                else
                {
                    // Dense air accumulates with distance, while the player's light and authored
                    // lamps burn a softer, thinner volume through it. This is independent from the
                    // black visibility curtain and remains visible as moving matter.
                    var distanceDensity = Mathf.Lerp(.42f, .86f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.7f, 8.5f, distance))) * density;
                    // The actor burns a local pocket through independently moving world fog.
                    // This changes particle opacity, never their position, so the cloud does not
                    // follow the player like an overlay.
                    // Per-pixel player dispersal belongs to the shader. Fading a whole billboard
                    // here as well erased its distant half and made small rooms look fog-free.
                    var dispersal = 1f;
                    for (var lightIndex = 0; lightIndex < dungeon.LightSources.Count; lightIndex++)
                    {
                        var source = dungeon.LightSources[lightIndex];
                        var lightDistance = Vector2.Distance(world, source.Position);
                        if (lightDistance >= source.Radius) continue;
                        dispersal *= Mathf.Lerp(.58f, 1f, lightDistance / source.Radius);
                    }
                    Color color = buffer[i].startColor;
                    var variation = Mathf.Lerp(.72f, 1.22f, Hash01(buffer[i].randomSeed));
                    color.r = Mathf.Clamp01(tint.r * variation);
                    color.g = Mathf.Clamp01(tint.g * Mathf.Lerp(.82f, 1.12f, variation));
                    color.b = Mathf.Clamp01(tint.b * Mathf.Lerp(1.16f, .84f, variation));
                    color.a = distanceDensity * dispersal * Mathf.Lerp(.68f, 1.18f,
                        Hash01(buffer[i].randomSeed ^ 0x9e3779b9u));
                    buffer[i].startColor = color;
                }
            }
            particles.SetParticles(buffer, count);
        }

        private bool IsSafeArrival(Vector2 world)
        {
            if (dungeon == null || dungeon.Rooms.Count == 0) return false;
            return dungeon.Rooms[0].bounds.Contains(Vector2Int.FloorToInt(world));
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215f;
        }

        private float ContourDistance(Vector2 logicalOffset)
        {
            if (logicalOffset.sqrMagnitude < .001f) return 0f;
            var angle = Mathf.Atan2(logicalOffset.y, logicalOffset.x);
            if (angle > 0f) angle -= Mathf.PI * 2f;
            var normalized = -angle / (Mathf.PI * 2f);
            var ray = Mathf.RoundToInt(normalized * contourDistances.Length) % contourDistances.Length;
            return contourDistances[Mathf.Clamp(ray, 0, contourDistances.Length - 1)];
        }

        private static Texture2D CreateSoftSmokeParticle()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Soft Dungeon Air Particle",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var uv = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                // An elongated, broken wisp reads as suspended air. The old broad radial blob
                // overlapped into a uniform grey wash and looked like a screen-space mask.
                var warped = new Vector2(uv.x * .78f + Mathf.Sin(uv.y * 4.2f) * .11f, uv.y * 1.22f);
                var radial = Mathf.Clamp01(1f - warped.magnitude);
                var broad = Mathf.PerlinNoise(x * .041f + 8.3f, y * .041f + 17.1f);
                var detail = Mathf.PerlinNoise(x * .113f + 31.7f, y * .097f + 4.6f);
                var filaments = Mathf.SmoothStep(.43f, .79f, broad * .68f + detail * .32f);
                var alpha = Mathf.Pow(radial, 1.38f) * filaments;
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (smokeMaterial != null) Destroy(smokeMaterial);
            if (smokeTexture != null) Destroy(smokeTexture);
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
            // Decorative fire breathes; it never strobes or makes distant walls flash.
            amount = Mathf.Clamp(flickerAmount, 0, .075f);
            dungeon = data;
            sourcePosition = position;
            seed = Mathf.Abs(transform.position.x * 9.31f + transform.position.y * 5.77f + GetInstanceID());
            var x = Mathf.FloorToInt(sourcePosition.x);
            var y = Mathf.FloorToInt(sourcePosition.y);
            visibility = dungeon != null && dungeon.IsExplored(x, y) ? 1f : 0f;
            light2D.intensity = baseIntensity * visibility;
        }

        private void Update()
        {
            if (light2D == null) return;
            var slow = Mathf.PerlinNoise(seed, Time.time * 3.2f) * 2f - 1f;
            var fast = Mathf.PerlinNoise(seed + 17.3f, Time.time * 7.1f) * 2f - 1f;
            var noise = slow * .82f + fast * .18f;
            var x = Mathf.FloorToInt(sourcePosition.x);
            var y = Mathf.FloorToInt(sourcePosition.y);
            // Once discovered, an authored lamp belongs to the remembered world. Toggling it on
            // current line-of-sight made distant rooms pulse whenever a single visibility ray
            // crossed a cell boundary; the unknown-state overlay already hides undiscovered light.
            var targetVisibility = dungeon != null && dungeon.IsExplored(x, y) ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 2.2f);
            light2D.intensity = baseIntensity * (1f + noise * amount) * visibility;
        }
    }
}
