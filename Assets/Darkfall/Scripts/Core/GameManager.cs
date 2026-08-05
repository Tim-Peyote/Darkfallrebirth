using System;
using System.Collections.Generic;
using Darkfall.Gameplay;
using Darkfall.UI;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameBalance Balance { get; private set; }
        public DungeonData Dungeon { get; private set; }
        public PlayerController Player { get; private set; }
        public AudioService Audio { get; private set; }
        public bool IsPaused { get; private set; } = true;
        public int Depth { get; private set; } = 1;
        public int Gold { get; private set; }
        public int SessionKills { get; private set; }
        public HeroClass SelectedHero { get; private set; } = HeroClass.Mage;
        public SaveData Save { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public Transform LevelRoot => levelRoot;
        public LegacyShopUpgrade[] ShopOffers { get; private set; } = Array.Empty<LegacyShopUpgrade>();
        public bool IsBlockingModal => blockingModal;
        public float SessionSeconds => Mathf.Max(0, Time.realtimeSinceStartup - runStartedAt);

        public event Action StatsChanged;
        public event Action<string> OverlayRequested;

        private DungeonView dungeonView;
        private Transform levelRoot;
        private RuntimeUI runtimeUI;
        private int runSeed;
        private readonly Dictionary<string, int> shopPurchases = new Dictionary<string, int>();
        private bool[] shopOfferSold = Array.Empty<bool>();
        private bool blockingModal;
        private float runStartedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("Darkfall Runtime").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Save = SaveService.Load();
            if (Save.topRecords == null) Save.topRecords = new List<RunRecord>();
            Inventory = new InventorySystem();
            shopPurchases.Clear();
            ShopOffers = Array.Empty<LegacyShopUpgrade>();
            shopOfferSold = Array.Empty<bool>();
            Balance = Resources.Load<GameBalance>("Config/GameBalance");
            if (Balance == null) Balance = GameBalance.RuntimeDefault();

            SetupCamera();
            Audio = gameObject.AddComponent<AudioService>();
            Audio.Initialize(Save.masterVolume);
            Audio.ApplySettings(Save);
            runtimeUI = gameObject.AddComponent<RuntimeUI>();
            runtimeUI.Initialize(this);
            Audio.PlayMusic("Main");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Player != null)
            {
                if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) InventoryUI.Instance.Close();
                else TogglePause();
            }
            if (Player == null || IsPaused) return;
            if (EnemyController.Count == 0 && ExitPortal.Active != null && !ExitPortal.Active.IsEmpowered)
                OpenExitPortal();
            if (GameInput.InteractPressed && !ExitPortal.InteractNearest(Player)) TreasureChest.InteractNearest(Player);
            var quickSlot = GameInput.QuickSlotPressed;
            if (quickSlot >= 0) Inventory.UseQuickSlot(quickSlot, Player);
        }

        private static void SetupCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.backgroundColor = new Color(0.008f, 0.01f, 0.025f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0, -10);
        }

        public void SelectHero(HeroClass hero)
        {
            SelectedHero = hero;
            StatsChanged?.Invoke();
        }

        public void StartRun()
        {
            Depth = 1;
            Gold = 0;
            SessionKills = 0;
            Inventory = new InventorySystem();
            shopPurchases.Clear();
            ShopOffers = Array.Empty<LegacyShopUpgrade>();
            shopOfferSold = Array.Empty<bool>();
            runSeed = Environment.TickCount;
            runStartedAt = Time.realtimeSinceStartup;
            BuildLevel();
            IsPaused = false;
            blockingModal = false;
            Time.timeScale = 1;
            Audio.SetPaused(false);
            Audio.PlayMusic("stage1");
            runtimeUI.ShowGame();
        }

        private void BuildLevel()
        {
            ExitPortal.ResetRegistry();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            EnemyController.ClearRegistry();
            GameInput.Reset();
            levelRoot = new GameObject("Generated Level").transform;
            Dungeon = DungeonGenerator.Generate(Balance, Depth, runSeed + Depth * 7919);
            dungeonView = new GameObject("Dungeon").AddComponent<DungeonView>();
            dungeonView.transform.SetParent(levelRoot, false);
            dungeonView.Build(Dungeon, Depth);

            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(levelRoot);
            playerObject.transform.position = Dungeon.CellCenter(Dungeon.StartCell);
            Player = playerObject.AddComponent<PlayerController>();
            Player.Initialize(HeroDefinition.Create(SelectedHero), Dungeon);
            var lighting = new GameObject("Dungeon Lighting").AddComponent<DungeonLighting>();
            lighting.transform.SetParent(levelRoot, false);
            lighting.Build(Dungeon, Player, DungeonVisualProfile.ForDepth(Depth));
            var fog = new GameObject("Fog of War").AddComponent<FogOfWarView>();
            fog.transform.SetParent(levelRoot, false);
            fog.Initialize(Dungeon, Player);

            var isBossLevel = Depth % Balance.bossEveryLevels == 0;
            if (isBossLevel)
            {
                SpawnEnemy(Dungeon.ExitCell, true);
            }
            else
            {
                var enemyBudget = EnemyBudgetForDepth(Balance, Depth);
                for (var i = 0; i < enemyBudget; i++)
                {
                    SpawnEnemy(PickSpawnCell(i), false);
                }
                var chestCount = Mathf.Clamp(1 + Depth / 4, 1, 4);
                for (var i = 0; i < chestCount; i++)
                    TreasureChest.Spawn(Dungeon.CellCenter(PickSpawnCell(enemyBudget + i + 3)), Player);
            }
            var portal = ExitPortal.Spawn(Dungeon.CellCenter(Dungeon.ExitCell), Player);
            if (EnemyController.Count == 0) portal.Empower();
            NotifyStatsChanged();
        }

        private Vector2Int PickSpawnCell(int index)
        {
            var roomIndex = 1 + index % Mathf.Max(1, Dungeon.Rooms.Count - 1);
            var room = Dungeon.Rooms[roomIndex];
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var x = UnityEngine.Random.Range(room.bounds.xMin + 1, room.bounds.xMax - 1);
                var y = UnityEngine.Random.Range(room.bounds.yMin + 1, room.bounds.yMax - 1);
                var cell = new Vector2Int(x, y);
                if (Dungeon.CanOccupy(Dungeon.CellCenter(cell), .22f)) return cell;
            }
            return room.Center;
        }

        public static int EnemyBudgetForDepth(GameBalance balance, int depth)
        {
            if (balance == null) return 0;
            return Mathf.Clamp(
                balance.baseEnemyCount + Mathf.FloorToInt(Mathf.Max(0, depth - 1) * 1.5f),
                balance.baseEnemyCount,
                80);
        }

        private void SpawnEnemy(Vector2Int cell, bool boss)
        {
            LegacyEnemy definition;
            if (boss)
            {
                var bosses = LegacyCatalog.Data.bosses;
                definition = bosses[Mathf.Max(0, Depth / Balance.bossEveryLevels - 1) % bosses.Length];
            }
            else
            {
                var eligible = new System.Collections.Generic.List<LegacyEnemy>();
                foreach (var candidate in LegacyCatalog.Data.enemies)
                    if (candidate.levelRequirement <= Depth) eligible.Add(candidate);
                definition = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            }
            var enemyObject = new GameObject(definition.type);
            enemyObject.transform.SetParent(levelRoot);
            enemyObject.transform.position = Dungeon.CellCenter(cell);
            enemyObject.AddComponent<EnemyController>().Initialize(Dungeon, Player, Depth, boss, definition);
        }

        public void SpawnSummonedSkeleton(Vector2 position)
        {
            if (Dungeon == null || Player == null || !Dungeon.CanOccupy(position)) return;
            var definition = LegacyCatalog.Data.enemies[0];
            var enemyObject = new GameObject(definition.type);
            enemyObject.transform.SetParent(levelRoot);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<EnemyController>().Initialize(Dungeon, Player, Depth, false, definition);
        }

        public void SpawnMimic(Vector2 position)
        {
            if (Dungeon == null || Player == null || !Dungeon.CanOccupy(position, .22f)) return;
            var definition = new LegacyEnemy
            {
                type = "Chest Mimic",
                color = "#FFFFFF",
                hp = 48f,
                damage = 21f,
                speed = 68f,
                attackRange = 46f,
                reward = 38f,
                levelRequirement = 1,
                levelTier = 1,
                abilities = Array.Empty<string>()
            };
            var enemyObject = new GameObject(definition.type);
            enemyObject.transform.SetParent(levelRoot);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<EnemyController>().Initialize(Dungeon, Player, Depth, false, definition);
            CombatVfx.SpawnPulse(position, new Color(.72f, .18f, .055f), 1.4f, .3f);
            ShowMessage("Сундук оказался мимиком!");
        }

        public void EnemyDefeated(Vector2 position, bool boss, float reward)
        {
            SessionKills++;
            Save.totalKills++;
            Audio.PlayEffect("enemy_die");
            var goldChance = boss ? 1f : reward >= 30 ? .6f : .3f;
            if (UnityEngine.Random.value < goldChance)
                Pickup.SpawnGold(position + Vector2.left * .25f, Player,
                    Mathf.FloorToInt(reward * (1 + (Depth - 1) * .08f)));
            var lootChance = Depth <= 4 ? .15f : Depth <= 10 ? .20f : .25f;
            if (boss || UnityEngine.Random.value < lootChance)
                Pickup.SpawnItem(position + Vector2.right * .25f, Player, InventorySystem.GenerateLoot(Depth));
            if (EnemyController.Count <= 1) OpenExitPortal();
            NotifyStatsChanged();
        }

        private void OpenExitPortal()
        {
            if (ExitPortal.Active == null || ExitPortal.Active.IsEmpowered) return;
            ExitPortal.Active.Empower();
            OverlayRequested?.Invoke("Этаж зачищен — портал отмечен на карте");
        }

        public void NextLevel()
        {
            Depth++;
            Save.bestDepth = Mathf.Max(Save.bestDepth, Depth);
            SaveService.Save(Save);
            Audio.PlayEffect("item_pickup");
            if (Depth > 1 && (Depth - 1) % 3 == 0)
            {
                PrepareShop();
                IsPaused = true;
                blockingModal = true;
                Time.timeScale = 0;
                Audio.SetPaused(true);
                runtimeUI.ShowShop();
            }
            else ContinueAfterShop();
        }

        public void CompleteLevel()
        {
            if (Player == null || IsPaused) return;
            IsPaused = true;
            blockingModal = true;
            Time.timeScale = 0;
            Audio.SetPaused(true);
            runtimeUI.ShowLevelComplete();
            Audio.PlayMusic("Level_Complite");
        }

        private void PrepareShop()
        {
            var pool = new List<LegacyShopUpgrade>();
            foreach (var upgrade in LegacyCatalog.Data.shop)
                if (PurchaseCount(upgrade.id) < upgrade.maxPurchases) pool.Add(upgrade);
            ShopOffers = new LegacyShopUpgrade[Mathf.Min(5, pool.Count)];
            shopOfferSold = new bool[ShopOffers.Length];
            for (var i = 0; i < ShopOffers.Length; i++)
            {
                var index = UnityEngine.Random.Range(0, pool.Count);
                ShopOffers[i] = pool[index];
                pool.RemoveAt(index);
            }
        }

        public int PurchaseCount(string id) => shopPurchases.TryGetValue(id, out var count) ? count : 0;
        public bool IsShopOfferSold(int offerIndex) => offerIndex >= 0 && offerIndex < shopOfferSold.Length && shopOfferSold[offerIndex];
        public int ShopPrice(LegacyShopUpgrade upgrade) =>
            Mathf.FloorToInt(upgrade.basePrice * (1 + PurchaseCount(upgrade.id) * .5f));

        public bool BuyShopOffer(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= ShopOffers.Length || Player == null || IsShopOfferSold(offerIndex)) return false;
            var upgrade = ShopOffers[offerIndex];
            var count = PurchaseCount(upgrade.id);
            if (count >= upgrade.maxPurchases) return false;
            var price = ShopPrice(upgrade);
            if (Gold < price) { ShowMessage("Недостаточно золота"); return false; }
            Gold -= price;
            shopPurchases[upgrade.id] = count + 1;
            shopOfferSold[offerIndex] = true;
            Player.ApplyShopUpgrade(upgrade);
            NotifyStatsChanged();
            return true;
        }

        public void ContinueAfterShop()
        {
            BuildLevel();
            IsPaused = false;
            blockingModal = false;
            Time.timeScale = 1;
            Audio.SetPaused(false);
            Audio.PlayMusic("stage1");
            runtimeUI.ShowGame();
            OverlayRequested?.Invoke(Depth % Balance.bossEveryLevels == 0 ? "Страж глубины пробудился" : $"Глубина {Depth}");
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            NotifyStatsChanged();
        }

        public void NotifyStatsChanged() => StatsChanged?.Invoke();
        public void ShowMessage(string message) => OverlayRequested?.Invoke(message);

        public void TogglePause()
        {
            if (blockingModal) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0 : 1;
            Audio.SetPaused(IsPaused);
            runtimeUI.ShowPause(IsPaused);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1;
            Audio.SetPaused(false);
            runtimeUI.ShowPause(false);
        }

        public void PauseForModal(bool paused)
        {
            if (!paused && blockingModal) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0 : 1;
            Audio.SetPaused(paused);
            GameInput.Reset();
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1;
            IsPaused = true;
            blockingModal = true;
            Audio.SetPaused(false);
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            Player = null;
            SaveService.Save(Save);
            Audio.PlayMusic("Main");
            runtimeUI.ShowMenu();
        }

        public void GameOver()
        {
            IsPaused = true;
            blockingModal = true;
            Time.timeScale = 0;
            Audio.SetPaused(true);
            Save.bestDepth = Mathf.Max(Save.bestDepth, Depth);
            Save.topRecords.Add(new RunRecord
            {
                depth = Depth,
                kills = SessionKills,
                seconds = SessionSeconds,
                hero = SelectedHero.ToString(),
                date = DateTime.Now.ToString("yyyy-MM-dd")
            });
            Save.topRecords.Sort((a, b) =>
            {
                var depth = b.depth.CompareTo(a.depth);
                if (depth != 0) return depth;
                var kills = b.kills.CompareTo(a.kills);
                return kills != 0 ? kills : a.seconds.CompareTo(b.seconds);
            });
            if (Save.topRecords.Count > 10) Save.topRecords.RemoveRange(10, Save.topRecords.Count - 10);
            SaveService.Save(Save);
            Audio.PlayMusic("GameOver");
            runtimeUI.ShowGameOver();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Player != null && !IsPaused) TogglePause();
            SaveService.Save(Save);
        }

        private void OnApplicationQuit() => SaveService.Save(Save);
    }
}
