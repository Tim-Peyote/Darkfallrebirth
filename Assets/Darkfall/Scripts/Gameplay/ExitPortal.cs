using Darkfall.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.Gameplay
{
    public sealed class ExitPortal : MonoBehaviour
    {
        public static ExitPortal Active { get; private set; }
        private PlayerController player;
        private bool entered;
        private bool unlocked;
        private Light2D portalLight;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer groundGlow;
        private float visibility;
        public bool IsUnlocked => unlocked;

        public static ExitPortal Spawn(Vector2 position, PlayerController target)
        {
            if (Active != null) return Active;
            var gameObject = new GameObject("Exit Portal");
            if (GameManager.Instance?.LevelRoot != null) gameObject.transform.SetParent(GameManager.Instance.LevelRoot);
            gameObject.transform.position = position;
            var portal = gameObject.AddComponent<ExitPortal>();
            Active = portal;
            portal.player = target;
            var glowObject = new GameObject("Portal Ground Glow");
            glowObject.transform.SetParent(gameObject.transform, false);
            glowObject.transform.localScale = Vector3.one * 1.7f;
            portal.groundGlow = glowObject.AddComponent<SpriteRenderer>();
            portal.groundGlow.sprite = RuntimeAssets.Glow;
            portal.groundGlow.sortingOrder = 8;
            portal.groundGlow.color = new Color(.5f, .08f, .025f, 0f);
            DarkfallRenderMaterials.MakeEmissive(portal.groundGlow);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            portal.spriteRenderer = renderer;
            renderer.sprite = EnvironmentSpriteAtlas.Prop(11);
            renderer.color = Color.white;
            renderer.sortingOrder = 10;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            portal.portalLight = gameObject.AddComponent<Light2D>();
            portal.portalLight.lightType = Light2D.LightType.Point;
            portal.portalLight.color = new Color(1f, .28f, .06f);
            portal.portalLight.intensity = 0f;
            portal.portalLight.pointLightInnerRadius = .35f;
            portal.portalLight.pointLightOuterRadius = 3.4f;
            portal.portalLight.falloffIntensity = .82f;
            portal.portalLight.shadowsEnabled = true;
            portal.portalLight.shadowIntensity = .9f;
            portal.portalLight.shadowSoftness = .6f;
            gameObject.transform.localScale = Vector3.one * 1.05f;
            return portal;
        }

        public void Unlock()
        {
            if (unlocked) return;
            unlocked = true;
            CombatVfx.SpawnPulse(transform.position, new Color(1f, .26f, .045f), 2.4f, .55f);
        }

        public static void ResetRegistry()
        {
            if (Active != null) Active.enabled = false;
            Active = null;
        }

        public static bool InteractNearest(PlayerController target)
        {
            if (Active == null || target == null || Vector2.Distance(Active.transform.position, target.transform.position) > 1.45f)
                return false;
            if (!Active.unlocked)
            {
                GameManager.Instance.ShowMessage($"Портал запечатан · осталось врагов: {EnemyController.Count}");
                return true;
            }
            Active.Enter();
            return true;
        }

        public static float DistanceToNearest(PlayerController target) =>
            Active == null || target == null ? float.MaxValue : Vector2.Distance(Active.transform.position, target.transform.position);

        private void Enter()
        {
            if (entered || !unlocked) return;
            entered = true;
            GameManager.Instance.CompleteLevel();
        }

        private void Update()
        {
            var scale = 1.02f + Mathf.Sin(Time.time * 2.2f) * .035f;
            transform.localScale = Vector3.one * scale;
            var dungeon = GameManager.Instance?.Dungeon;
            var targetVisibility = dungeon != null && dungeon.IsVisible(
                Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y)) ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 6f);
            var lockedTint = new Color(.42f, .38f, .36f, visibility * .72f);
            var openTint = new Color(1f, .82f, .64f, visibility);
            if (spriteRenderer != null) spriteRenderer.color = unlocked ? openTint : lockedTint;
            if (groundGlow != null)
                groundGlow.color = unlocked
                    ? new Color(1f, .16f, .025f, visibility * (.42f + Mathf.Sin(Time.time * 2.1f) * .08f))
                    : new Color(.28f, .035f, .018f, visibility * .16f);
            if (portalLight != null)
                portalLight.intensity = (unlocked ? .86f + Mathf.Sin(Time.time * 2.8f) * .12f : .12f) * visibility;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
