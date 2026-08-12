using Darkfall.Core;
using Darkfall.World;
using System.Collections;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>One-use sanctuary reward for the authored Shrine room.</summary>
    public sealed class CatacombShrineScenario : MonoBehaviour
    {
        private DungeonData dungeon;
        private PlayerController player;
        private bool consumed;
        private CatacombShrineVisual shrineVisual;

        public void Initialize(DungeonData source, PlayerController target)
        {
            dungeon = source;
            player = target;
            BuildVisual();
        }

        private void Update()
        {
            if (consumed || dungeon == null || player == null) return;
            var anchor = (Vector2)transform.position;
            var playerPosition = (Vector2)player.transform.position;
            if (!dungeon.SharesCombatElevation(anchor, playerPosition) ||
                Vector2.Distance(anchor, playerPosition) > 1.15f) return;
            consumed = true;
            player.ClearNegativeEffects();
            player.Heal(Mathf.Max(18f, player.MaxHealth * .28f));
            player.AddBarrier(12f + GameManager.Instance.Depth * 1.5f);
            CombatVfx.SpawnPulse(anchor, new Color(.92f, .72f, .32f), 1.65f, .7f);
            GameManager.Instance.ShowMessage("Святилище очистило раны и даровало защиту");
            shrineVisual?.Activate();
            StartCoroutine(FinishBlessing());
        }

        private IEnumerator FinishBlessing()
        {
            yield return new WaitForSeconds(.9f);
            shrineVisual?.Consume();
            enabled = false;
        }

        private void BuildVisual()
        {
            var visual = new GameObject("Catacomb Shrine · Animated Blessing");
            if (GameManager.Instance != null && GameManager.Instance.LevelRoot != null)
                visual.transform.SetParent(GameManager.Instance.LevelRoot, false);
            shrineVisual = visual.AddComponent<CatacombShrineVisual>();
            shrineVisual.Initialize(transform.position);
        }

        private void OnDestroy()
        {
            if (shrineVisual != null) Destroy(shrineVisual.gameObject);
        }
    }

    internal sealed class CatacombShrineVisual : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite dormant;
        private Sprite active;
        private Sprite consumed;
        private bool blessing;
        private bool spent;
        private float phase;

        public void Initialize(Vector2 logicalPosition)
        {
            dormant = Load("dormant");
            active = Load("active");
            consumed = Load("consumed");
            transform.position = IsoWorld.Project(logicalPosition, .055f);
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = dormant;
            spriteRenderer.sortingOrder = IsoWorld.SortingOrder(logicalPosition, 1016);
            DarkfallRenderMaterials.MakeLit(spriteRenderer);
            transform.localScale = Vector3.one * .34f;
        }

        public void Activate()
        {
            blessing = true;
            if (spriteRenderer != null) spriteRenderer.sprite = active;
        }

        public void Consume()
        {
            spent = true;
            blessing = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = consumed;
                spriteRenderer.color = new Color(.68f, .68f, .66f, .96f);
            }
            transform.localScale = Vector3.one * .34f;
        }

        private void Update()
        {
            if (spriteRenderer == null || spent) return;
            phase += Time.deltaTime * (blessing ? 7.2f : 1.1f);
            var wave = Mathf.Sin(phase);
            transform.localScale = Vector3.one * (.34f + wave * (blessing ? .008f : .002f));
            var value = blessing ? .96f + wave * .04f : .82f + wave * .02f;
            spriteRenderer.color = new Color(value, value, value, blessing ? 1f : .95f);
        }

        private static Sprite Load(string state)
        {
            var texture = Resources.Load<Texture2D>("Sprites/Scenarios/CatacombShrine/" + state);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .12f), 180f, 0, SpriteMeshType.FullRect);
        }
    }
}
