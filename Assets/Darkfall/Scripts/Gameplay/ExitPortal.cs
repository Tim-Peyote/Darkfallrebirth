using Darkfall.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.Gameplay
{
    public sealed class ExitPortal : MonoBehaviour
    {
        private PlayerController player;
        private bool entered;
        private Light2D portalLight;
        private SpriteRenderer spriteRenderer;
        private float visibility;

        public static void Spawn(Vector2 position, PlayerController target)
        {
            var gameObject = new GameObject("Exit Portal");
            if (GameManager.Instance?.LevelRoot != null) gameObject.transform.SetParent(GameManager.Instance.LevelRoot);
            gameObject.transform.position = position;
            var portal = gameObject.AddComponent<ExitPortal>();
            portal.player = target;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            portal.spriteRenderer = renderer;
            renderer.sprite = EnvironmentSpriteAtlas.Prop(11);
            renderer.color = Color.white;
            renderer.sortingOrder = 10;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            portal.portalLight = gameObject.AddComponent<Light2D>();
            portal.portalLight.lightType = Light2D.LightType.Point;
            portal.portalLight.color = new Color(1f, .28f, .06f);
            portal.portalLight.intensity = .72f;
            portal.portalLight.pointLightInnerRadius = .35f;
            portal.portalLight.pointLightOuterRadius = 3.4f;
            portal.portalLight.falloffIntensity = .82f;
            portal.portalLight.shadowsEnabled = true;
            portal.portalLight.shadowIntensity = .9f;
            portal.portalLight.shadowSoftness = .6f;
            gameObject.transform.localScale = Vector3.one * 1.05f;
        }

        private void Update()
        {
            var scale = 1.02f + Mathf.Sin(Time.time * 2.2f) * .035f;
            transform.localScale = Vector3.one * scale;
            var dungeon = GameManager.Instance?.Dungeon;
            var targetVisibility = dungeon != null && dungeon.IsVisible(
                Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y)) ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, Time.deltaTime * 6f);
            if (spriteRenderer != null) spriteRenderer.color = new Color(1, 1, 1, visibility);
            if (portalLight != null) portalLight.intensity = (.68f + Mathf.Sin(Time.time * 2.8f) * .09f) * visibility;
            if (!entered && player != null && Vector2.Distance(transform.position, player.transform.position) < 0.75f)
            {
                entered = true;
                GameManager.Instance.CompleteLevel();
            }
        }
    }
}
