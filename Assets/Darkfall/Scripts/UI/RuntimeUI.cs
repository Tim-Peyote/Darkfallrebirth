using System.Collections;
using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Darkfall.UI
{
    public sealed class RuntimeUI : MonoBehaviour
    {
        private GameManager game;
        private Font font;
        private Font boldFont;
        private Font headingFont;
        private Canvas canvas;
        private GameObject menu;
        private GameObject titlePage;
        private GameObject heroSelectPage;
        private GameObject menuNavigationShade;
        private GameObject hud;
        private GameObject pause;
        private GameObject settingsMenu;
        private GameObject shop;
        private GameObject levelComplete;
        private GameObject records;
        private GameObject gameOver;
        private Text heroInfo;
        private readonly Image[] heroCards = new Image[3];
        private Text stats;
        private Text toast;
        private Text gameOverStats;
        private Image healthFill;
        private Text healthText;
        private Image heroPortrait;
        private Image abilityFrame;
        private Image abilityCooldownFill;
        private Text abilityText;
        private readonly Text[] quickSlotTexts = new Text[3];
        private readonly Image[] quickSlotIcons = new Image[3];
        private GameObject bossBar;
        private Image bossFill;
        private Text bossName;
        private GameObject statusPanel;
        private Text statusEffects;
        private Text interactionHint;
        private GameObject interactionPrompt;
        private float nextHudRefresh;
        private Text shopGold;
        private Image shopHero;
        private Text shopHeroStats;
        private UIHeroIdleAnimator shopHeroAnimator;
        private HeroClass shopAnimatedClass;
        private bool shopAnimatorInitialized;
        private readonly Text[] shopOfferTexts = new Text[5];
        private readonly Button[] shopOfferButtons = new Button[5];
        private readonly Image[] shopOfferImages = new Image[5];
        private Text shopSelectionTitle;
        private Text shopSelectionDescription;
        private Text shopSelectionProgress;
        private Text shopBuyText;
        private Button shopBuyButton;
        private int selectedShopOffer = -1;
        private HeroClass selected = HeroClass.Mage;
        private Coroutine toastRoutine;
        private readonly List<PlayerStatusSnapshot> visibleStatuses = new List<PlayerStatusSnapshot>(8);

        public void Initialize(GameManager manager)
        {
            game = manager;
            font = Resources.Load<Font>("Fonts/PTSans-Regular");
            boldFont = Resources.Load<Font>("Fonts/PTSans-Bold");
            // One UI family prevents Cyrillic fallback and baseline drift between screens.
            headingFont = boldFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (boldFont == null) boldFont = font;
            // One Cyrillic UI family prevents the title/body mismatch visible in mixed fallbacks.
            headingFont = boldFont;
            BuildEventSystem();
            BuildCanvas();
            BuildMenu();
            BuildHud();
            canvas.gameObject.AddComponent<InventoryUI>().Initialize(game, font);
            BuildPause();
            BuildSettings();
            BuildShop();
            BuildLevelComplete();
            BuildRecords();
            BuildGameOver();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            canvas.gameObject.AddComponent<DeveloperConsoleUI>().Initialize(game, font, boldFont);
#endif
            game.StatsChanged += Refresh;
            game.OverlayRequested += ShowToast;
            ShowMenu();
        }

        private void OnDestroy()
        {
            if (game == null) return;
            game.StatsChanged -= Refresh;
            game.OverlayRequested -= ShowToast;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsMenu != null && settingsMenu.activeSelf) { settingsMenu.SetActive(false); return; }
                if (records != null && records.activeSelf) { records.SetActive(false); return; }
            }
            if (game == null || hud == null || !hud.activeSelf || game.Player == null || game.Inventory == null || Time.unscaledTime < nextHudRefresh) return;
            nextHudRefresh = Time.unscaledTime + .1f;
            Refresh();
        }

        private static void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var eventObject = new GameObject("EventSystem");
            var eventSystem = eventObject.AddComponent<EventSystem>();
            // The project intentionally reads gameplay keys directly. Disabling named UI
            // navigation axes keeps the EventSystem independent from the legacy Input Manager.
            eventSystem.sendNavigationEvents = false;
            eventObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventObject);
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Darkfall UI");
            DontDestroyOnLoad(canvasObject);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildMenu()
        {
            menu = new GameObject("Main Menu", typeof(RectTransform), typeof(RawImage));
            menu.transform.SetParent(canvas.transform, false);
            Stretch(menu.GetComponent<RectTransform>());
            var background = menu.GetComponent<RawImage>();
            var backgroundTexture = Resources.Load<Texture2D>("Art/Main");
            background.texture = backgroundTexture != null ? backgroundTexture : Texture2D.whiteTexture;
            background.color = backgroundTexture != null ? Color.white : new Color(.012f, .014f, .018f, 1f);
            menu.AddComponent<RawImageCover>();

            Panel("Atmosphere", menu.transform, new Color(.005f, .006f, .008f, .22f));
            menuNavigationShade = Panel("Navigation Shade", menu.transform, new Color(.004f, .005f, .007f, .76f));
            SetRect(menuNavigationShade.GetComponent<RectTransform>(), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(690, 1080), new Vector2(345, 0));

            var safe = Panel("Menu Safe Area", menu.transform, Color.clear);
            safe.GetComponent<Image>().raycastTarget = false;
            safe.AddComponent<SafeAreaFitter>();

            titlePage = Panel("Title Page", safe.transform, Color.clear);
            titlePage.GetComponent<Image>().raycastTarget = false;
            var rail = Panel("Title Navigation", titlePage.transform, Color.clear);
            rail.GetComponent<Image>().raycastTarget = false;
            SetRect(rail.GetComponent<RectTransform>(), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(560, 900), new Vector2(306, 0));
            var wordmark = AddText(rail.transform,
                "<b>DARKFALL</b>\n<size=27><color=#B89D78>DEPTHS</color></size>", 56,
                new Vector2(0, 286), new Vector2(460, 132), new Color(.94f, .84f, .65f), TextAnchor.MiddleLeft);
            wordmark.lineSpacing = .82f;
            AddDivider(rail.transform, 210, 460);
            AddButton(rail.transform, "НОВАЯ ИГРА", new Vector2(-20, 132), new Vector2(420, 58), ShowHeroSelect, new Color(.9f, .55f, .18f));
            AddButton(rail.transform, "НАСТРОЙКИ", new Vector2(-20, 62), new Vector2(420, 54), () => settingsMenu.SetActive(true));
            AddButton(rail.transform, "РЕКОРДЫ", new Vector2(-20, -4), new Vector2(420, 54), ShowRecords);
            AddButton(rail.transform, "ВЫХОД", new Vector2(-20, -70), new Vector2(420, 54), QuitGame);
            AddText(rail.transform, $"ЛУЧШАЯ ГЛУБИНА  {game.Save.bestDepth}     ПОБЕЖДЕНО  {game.Save.totalKills}",
                16, new Vector2(0, -326), new Vector2(500, 34), new Color(.67f, .61f, .51f), TextAnchor.MiddleLeft);

            heroSelectPage = Panel("Hero Select Page", safe.transform, new Color(.004f, .005f, .007f, .46f));
            AddText(heroSelectPage.transform, "ВЫБЕРИТЕ ГЕРОЯ", 40, new Vector2(0, 424), new Vector2(900, 56), new Color(.94f, .84f, .66f), TextAnchor.MiddleCenter);
            heroInfo = AddText(heroSelectPage.transform, "", 17, new Vector2(0, 370), new Vector2(900, 30), new Color(.72f, .69f, .63f), TextAnchor.MiddleCenter);
            CreateHeroCard(heroSelectPage.transform, HeroClass.Mage, 0, new Vector2(-410, -22), 0f);
            CreateHeroCard(heroSelectPage.transform, HeroClass.Warrior, 1, new Vector2(0, -22), 2.1f);
            CreateHeroCard(heroSelectPage.transform, HeroClass.Rogue, 2, new Vector2(410, -22), 4.2f);
            AddButton(heroSelectPage.transform, "НАЗАД", new Vector2(-730, -420), new Vector2(220, 58), ShowTitlePage);
            AddButton(heroSelectPage.transform, "НАЧАТЬ ПОГРУЖЕНИЕ", new Vector2(0, -420), new Vector2(520, 64), game.StartRun, new Color(.9f, .55f, .18f));
            Select(HeroClass.Mage);
            ShowTitlePage();
        }

        private void BuildHud()
        {
            hud = Panel("HUD", canvas.transform, new Color(0, 0, 0, 0));
            hud.GetComponent<Image>().raycastTarget = false;
            hud.AddComponent<SafeAreaFitter>();

            var statsPanel = Panel("Player HUD", hud.transform, Color.clear);
            DarkFantasyHudSkin.Apply(statsPanel.GetComponent<Image>(), DarkFantasyHudSkin.Player);
            var statsAnchor = Application.isMobilePlatform ? new Vector2(0, 0) : new Vector2(0, 1);
            var statsPosition = Application.isMobilePlatform ? new Vector2(239, 62) : new Vector2(239, -62);
            SetRect(statsPanel.GetComponent<RectTransform>(), statsAnchor, statsAnchor, new Vector2(454, 108), statsPosition);
            var healthTrack = Panel("Health Track", statsPanel.transform, Color.white);
            DarkFantasySkin.Apply(healthTrack.GetComponent<Image>(), DarkFantasySkin.HealthBar);
            SetRect(healthTrack.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(302, 22), new Vector2(55, -15));
            var fillObject = Panel("Health Fill", healthTrack.transform, Color.white);
            healthFill = fillObject.GetComponent<Image>();
            healthFill.sprite = DarkFantasySkin.HealthFill;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            heroPortrait = new GameObject("Hero Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            heroPortrait.transform.SetParent(statsPanel.transform, false);
            heroPortrait.preserveAspect = true;
            heroPortrait.raycastTarget = false;
            SetRect(heroPortrait.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(84, 94), new Vector2(-171, -1));
            stats = AddText(statsPanel.transform, string.Empty, 16, new Vector2(55, 25), new Vector2(302, 24),
                new Color(.88f, .81f, .67f), TextAnchor.MiddleLeft);
            healthText = AddText(statsPanel.transform, "", 14, new Vector2(55, -15), new Vector2(286, 20), Color.white, TextAnchor.MiddleCenter);

            statusPanel = Panel("Timed Statuses", hud.transform, Color.white);
            DarkFantasySkin.Apply(statusPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.72f, .54f, .26f));
            SetRect(statusPanel.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(720, 50), new Vector2(0, -112));
            statusEffects = AddText(statusPanel.transform, "", 14, Vector2.zero, new Vector2(684, 30),
                new Color(.82f, .75f, .59f), TextAnchor.MiddleCenter);
            statusPanel.SetActive(false);

            toast = AddText(hud.transform, string.Empty, 21, new Vector2(0, 382), new Vector2(680, 38),
                new Color(.91f, .78f, .48f), TextAnchor.MiddleCenter);

            var stickBase = Panel("Move Stick", hud.transform, new Color(0.2f, 0.28f, 0.4f, 0.32f));
            SetRect(stickBase.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(190, 190), new Vector2(140, 140));
            var knob = Panel("Knob", stickBase.transform, new Color(0.55f, 0.7f, 0.9f, 0.5f));
            SetRect(knob.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75, 75), Vector2.zero);
            stickBase.AddComponent<VirtualStick>().Initialize(knob.GetComponent<RectTransform>());
            stickBase.SetActive(Application.isMobilePlatform);

            var attack = AddButton(hud.transform, "АТАКА", new Vector2(-145, 140), new Vector2(165, 165), null, new Color(0.55f, 0.1f, 0.12f));
            SetRect(attack.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(165, 165), new Vector2(-145, 140));
            attack.AddComponent<HoldAttackButton>();
            attack.SetActive(false);
            var ability = AddButton(hud.transform, "НАВЫК\nQ", Vector2.zero, new Vector2(104, 104), () => GameInput.TouchAbilityRequested = true);
            abilityFrame = ability.GetComponent<Image>();
            DarkFantasyHudSkin.Apply(abilityFrame, DarkFantasyHudSkin.Ability);
            var abilitySize = Application.isMobilePlatform ? new Vector2(92, 92) : new Vector2(62, 62);
            var abilityPosition = Application.isMobilePlatform ? new Vector2(-82, 78) : Vector2.zero;
            var abilityAnchor = Application.isMobilePlatform ? new Vector2(1, 0) : new Vector2(0, 0);
            SetRect(ability.GetComponent<RectTransform>(), abilityAnchor, abilityAnchor, abilitySize, abilityPosition);
            abilityText = ability.GetComponentInChildren<Text>();
            abilityText.fontSize = Application.isMobilePlatform ? 18 : 16;
            abilityText.color = new Color(.89f, .82f, .69f);
            abilityCooldownFill = Panel("Cooldown Shade", ability.transform, new Color(.008f, .009f, .011f, .82f)).GetComponent<Image>();
            abilityCooldownFill.type = Image.Type.Filled;
            abilityCooldownFill.fillMethod = Image.FillMethod.Radial360;
            abilityCooldownFill.fillOrigin = (int)Image.Origin360.Top;
            abilityCooldownFill.fillClockwise = true;
            abilityCooldownFill.raycastTarget = false;
            SetRect(abilityCooldownFill.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Application.isMobilePlatform ? new Vector2(76, 76) : new Vector2(58, 58), Vector2.zero);
            abilityText.transform.SetAsLastSibling();

            var pauseButton = AddButton(hud.transform, "II", Vector2.zero, new Vector2(58, 58), game.TogglePause);
            DarkFantasyHudSkin.Apply(pauseButton.GetComponent<Image>(), DarkFantasyHudSkin.PauseButton);
            var pausePosition = Application.isMobilePlatform ? new Vector2(-226, -28) : new Vector2(-83, -251);
            SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(42, 42), pausePosition);
            var inventoryButton = AddButton(hud.transform, "I", Vector2.zero, new Vector2(58, 58), () => InventoryUI.Instance?.Toggle());
            DarkFantasyHudSkin.Apply(inventoryButton.GetComponent<Image>(), DarkFantasyHudSkin.InventoryButton);
            var inventoryPosition = Application.isMobilePlatform ? new Vector2(-274, -28) : new Vector2(-133, -251);
            SetRect(inventoryButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(42, 42), inventoryPosition);

            var quickBar = Panel("Quick Slots", hud.transform, Color.clear);
            DarkFantasyHudSkin.Apply(quickBar.GetComponent<Image>(), DarkFantasyHudSkin.Quickbar);
            var quickAnchor = new Vector2(.5f, 0);
            var quickPosition = Application.isMobilePlatform ? new Vector2(0, 56) : new Vector2(0, 52);
            var quickSize = Application.isMobilePlatform ? new Vector2(360, 108) : new Vector2(420, 82);
            SetRect(quickBar.GetComponent<RectTransform>(), quickAnchor, quickAnchor, quickSize, quickPosition);
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var slotSize = Application.isMobilePlatform ? 72f : 58f;
                var quick = AddButton(quickBar.transform, $"{i + 1}\n—", Vector2.zero, new Vector2(slotSize, slotSize),
                    () => { game.Inventory.UseQuickSlot(index, game.Player); Refresh(); });
                DarkFantasySkin.Apply(quick.GetComponent<Image>(), DarkFantasySkin.Slot);
                SetRect(quick.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(slotSize, slotSize), Application.isMobilePlatform
                        ? new Vector2(-120 + i * 80, 0)
                        : new Vector2(-137 + i * 70, 0));
                quickSlotTexts[i] = quick.GetComponentInChildren<Text>();
                quickSlotTexts[i].fontSize = 16;
                quickSlotTexts[i].alignment = TextAnchor.LowerCenter;
                var iconObject = new GameObject("Item Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(quick.transform, false);
                SetRect(iconObject.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f),
                    new Vector2(42, 42), new Vector2(0, 4));
                quickSlotIcons[i] = iconObject.GetComponent<Image>();
                quickSlotIcons[i].preserveAspect = true;
                quickSlotIcons[i].raycastTarget = false;
                quickSlotIcons[i].gameObject.SetActive(false);
                quickSlotTexts[i].transform.SetAsLastSibling();
            }
            if (!Application.isMobilePlatform)
            {
                var divider = Panel("Ability Divider", quickBar.transform, new Color(.67f, .48f, .22f, .75f));
                SetRect(divider.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(2, 54), new Vector2(68, 0));
                divider.GetComponent<Image>().raycastTarget = false;
                ability.transform.SetParent(quickBar.transform, false);
                SetRect(ability.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    new Vector2(70, 70), new Vector2(139, 0));
            }
            ability.transform.SetAsLastSibling();

            bossBar = Panel("Boss Health", hud.transform, Color.clear);
            DarkFantasyHudSkin.Apply(bossBar.GetComponent<Image>(), DarkFantasyHudSkin.Boss);
            SetRect(bossBar.GetComponent<RectTransform>(), new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(600, 76), new Vector2(0, -50));
            var bossTrack = Panel("Boss Track", bossBar.transform, new Color(.065f, .012f, .016f, 1));
            DarkFantasySkin.Apply(bossTrack.GetComponent<Image>(), DarkFantasySkin.HealthBar);
            SetRect(bossTrack.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(548, 16), new Vector2(0, -9));
            var bossFillObject = Panel("Boss Fill", bossTrack.transform, new Color(.63f, .025f, .035f, 1));
            bossFill = bossFillObject.GetComponent<Image>();
            bossFill.sprite = DarkFantasySkin.HealthFill;
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossName = AddText(bossBar.transform, "", 17, new Vector2(0, 16), new Vector2(540, 24), new Color(.91f, .81f, .67f), TextAnchor.MiddleCenter);
            bossBar.SetActive(false);

            interactionPrompt = Panel("Interaction Prompt", hud.transform, Color.clear);
            DarkFantasyHudSkin.Apply(interactionPrompt.GetComponent<Image>(), DarkFantasyHudSkin.Prompt);
            SetRect(interactionPrompt.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(360, 52), new Vector2(0, 120));
            interactionHint = AddText(interactionPrompt.transform, "", 15, Vector2.zero, new Vector2(320, 26),
                new Color(.94f, .81f, .52f), TextAnchor.MiddleCenter);

            var minimapFrame = Panel("Minimap Frame", hud.transform, Color.clear);
            DarkFantasyHudSkin.Apply(minimapFrame.GetComponent<Image>(), DarkFantasyHudSkin.Minimap);
            SetRect(minimapFrame.GetComponent<RectTransform>(), new Vector2(1,1), new Vector2(1,1), new Vector2(190, 190), new Vector2(-108, -128));
            var minimapObject = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage));
            minimapObject.transform.SetParent(minimapFrame.transform, false);
            SetRect(minimapObject.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(166, 166), Vector2.zero);
            minimapObject.AddComponent<MinimapUI>().Initialize(game, minimapObject.GetComponent<RawImage>());
        }

        private void BuildPause()
        {
            pause = Panel("Pause", canvas.transform, new Color(0, 0, 0, 0.75f));
            var card = Panel("Pause Card", pause.transform, new Color(0.035f, 0.045f, 0.085f, 0.98f));
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620, 690), Vector2.zero);
            AddText(card.transform, "ПАУЗА", 44, new Vector2(0, 275), new Vector2(500, 70), new Color(.88f, .75f, .48f), TextAnchor.MiddleCenter);
            AddDivider(card.transform, 235, 470);
            AddButton(card.transform, "ПРОДОЛЖИТЬ", new Vector2(0, 185), new Vector2(430, 62), game.Resume);
            AddAudioSetting(card.transform, "ОБЩАЯ ГРОМКОСТЬ", 92, game.Save.masterVolume, value => game.Save.masterVolume = value);
            AddAudioSetting(card.transform, "МУЗЫКА", 2, game.Save.musicVolume, value => game.Save.musicVolume = value);
            AddAudioSetting(card.transform, "ЭФФЕКТЫ", -88, game.Save.sfxVolume, value => game.Save.sfxVolume = value);
            AddButton(card.transform, game.Save.audioEnabled ? "ОТКЛЮЧИТЬ ЗВУК" : "ВКЛЮЧИТЬ ЗВУК",
                new Vector2(0, -190), new Vector2(430, 60), () =>
                {
                    game.Save.audioEnabled = !game.Save.audioEnabled;
                    ApplyAudioSettings();
                });
            AddButton(card.transform, "В ГЛАВНОЕ МЕНЮ", new Vector2(0, -270), new Vector2(430, 60), game.ReturnToMenu, new Color(0.35f, 0.12f, 0.15f));
        }

        private void BuildSettings()
        {
            settingsMenu = Panel("Settings", canvas.transform, new Color(0, 0, 0, .88f));
            var card = Panel("Settings Card", settingsMenu.transform, new Color(.03f, .038f, .072f, .99f));
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(620, 620), Vector2.zero);
            AddText(card.transform, "НАСТРОЙКИ", 42, new Vector2(0, 240), new Vector2(520, 65), new Color(.88f, .75f, .48f), TextAnchor.MiddleCenter);
            AddDivider(card.transform, 202, 470);
            AddAudioSetting(card.transform, "ОБЩАЯ ГРОМКОСТЬ", 125, game.Save.masterVolume, value => game.Save.masterVolume = value);
            AddAudioSetting(card.transform, "МУЗЫКА", 35, game.Save.musicVolume, value => game.Save.musicVolume = value);
            AddAudioSetting(card.transform, "ЭФФЕКТЫ", -55, game.Save.sfxVolume, value => game.Save.sfxVolume = value);
            AddButton(card.transform, "ЗВУК ВКЛ / ВЫКЛ", new Vector2(0, -170), new Vector2(440, 65), () =>
            {
                game.Save.audioEnabled = !game.Save.audioEnabled;
                ApplyAudioSettings();
            });
            AddButton(card.transform, "НАЗАД", new Vector2(0, -260), new Vector2(440, 65), () => settingsMenu.SetActive(false));
            settingsMenu.SetActive(false);
        }

        private void AddAudioSetting(Transform parent, string label, float y, float value, System.Action<float> setter)
        {
            AddText(parent, label, 19, new Vector2(0, y + 38), new Vector2(420, 35), new Color(0.78f, 0.72f, 0.62f), TextAnchor.MiddleCenter);
            var slider = AddSlider(parent, new Vector2(0, y), value);
            slider.onValueChanged.AddListener(current => { setter(current); ApplyAudioSettings(); });
        }

        private void ApplyAudioSettings()
        {
            game.Audio.ApplySettings(game.Save);
            SaveService.Save(game.Save);
        }

        private void BuildGameOver()
        {
            gameOver = Panel("Game Over", canvas.transform, new Color(0.04f, 0, 0.01f, 0.92f));
            var card = Panel("Game Over Card", gameOver.transform, new Color(0.08f, 0.025f, 0.035f, 0.98f));
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(0.85f, 0.55f, 0.58f));
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620, 540), Vector2.zero);
            AddText(card.transform, "ТЬМА ПОГЛОТИЛА ГЕРОЯ", 40, new Vector2(0, 180), new Vector2(560, 100), new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter);
            gameOverStats = AddText(card.transform, string.Empty, 25, new Vector2(0, 60), new Vector2(500, 120), Color.white, TextAnchor.MiddleCenter);
            AddButton(card.transform, "НОВАЯ ПОПЫТКА", new Vector2(0, -65), new Vector2(430, 76), game.StartRun);
            AddButton(card.transform, "В ГЛАВНОЕ МЕНЮ", new Vector2(0, -165), new Vector2(430, 76), game.ReturnToMenu, new Color(0.3f, 0.12f, 0.15f));
        }

        private void BuildShop()
        {
            shop = new GameObject("Sanctuary Shop", typeof(RectTransform), typeof(RawImage));
            shop.transform.SetParent(canvas.transform, false);
            Stretch(shop.GetComponent<RectTransform>());
            var background = shop.GetComponent<RawImage>();
            background.texture = Resources.Load<Texture2D>("Art/shop-sanctuary");
            background.color = background.texture != null ? Color.white : new Color(.025f, .018f, .012f, 1f);
            background.raycastTarget = false;
            shop.AddComponent<RawImageCover>();

            var veil = Panel("Sanctuary Atmosphere", shop.transform, new Color(.006f, .007f, .008f, .34f));
            veil.GetComponent<Image>().raycastTarget = false;
            var safe = Panel("Shop Safe Area", shop.transform, Color.clear);
            safe.GetComponent<Image>().raycastTarget = false;
            safe.AddComponent<SafeAreaFitter>();

            var header = Panel("Merchant Header", safe.transform, Color.white);
            DarkFantasySkin.Apply(header.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.72f, .48f, .20f));
            SetRect(header.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(1740, 92), new Vector2(0, -62));
            AddText(header.transform, "УБЕЖИЩЕ СКУПЩИКА", 32, new Vector2(-600, 8), new Vector2(470, 42),
                new Color(.94f, .79f, .51f), TextAnchor.MiddleLeft);
            AddText(header.transform, "Реликвии и услуги между погружениями", 15, new Vector2(-545, -24), new Vector2(580, 24),
                DarkFantasySkin.MutedText, TextAnchor.MiddleLeft);
            shopGold = AddText(header.transform, string.Empty, 22, new Vector2(650, 0), new Vector2(360, 44),
                new Color(.95f, .72f, .28f), TextAnchor.MiddleRight);

            var heroCard = Panel("Traveller", safe.transform, Color.white);
            DarkFantasySkin.Apply(heroCard.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.36f, .28f, .19f));
            SetRect(heroCard.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(460, 790), new Vector2(-622, -48));
            AddText(heroCard.transform, "ПУТНИК", 18, new Vector2(0, 344), new Vector2(360, 34),
                new Color(.82f, .68f, .44f), TextAnchor.MiddleCenter);
            AddDivider(heroCard.transform, 315, 342);
            shopHero = new GameObject("Hero Preview", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shopHero.transform.SetParent(heroCard.transform, false);
            shopHero.preserveAspect = true;
            shopHero.raycastTarget = false;
            SetRect(shopHero.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(360, 470), new Vector2(0, 78));
            shopHeroAnimator = shopHero.gameObject.AddComponent<UIHeroIdleAnimator>();
            var statsSurface = Panel("Traveller Stats", heroCard.transform, Color.white);
            DarkFantasySkin.Apply(statsSurface.GetComponent<Image>(), DarkFantasySkin.Slot);
            SetRect(statsSurface.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(386, 188), new Vector2(0, -266));
            shopHeroStats = AddText(statsSurface.transform, string.Empty, 16, new Vector2(8, 0), new Vector2(322, 146),
                new Color(.86f, .82f, .73f), TextAnchor.MiddleLeft);

            var offersCard = Panel("Relic Counter", safe.transform, Color.white);
            DarkFantasySkin.Apply(offersCard.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(.62f, .40f, .17f));
            SetRect(offersCard.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(1120, 790), new Vector2(280, -48));
            AddText(offersCard.transform, "ДОСТУПНЫЕ УСИЛЕНИЯ", 26, new Vector2(-295, 342), new Vector2(420, 40),
                DarkFantasySkin.Text, TextAnchor.MiddleLeft);
            AddText(offersCard.transform, "Выберите товар, затем подтвердите сделку", 15, new Vector2(300, 342), new Vector2(430, 28),
                DarkFantasySkin.MutedText, TextAnchor.MiddleRight);
            AddDivider(offersCard.transform, 310, 1010);
            for (var i = 0; i < 5; i++)
            {
                var index = i;
                var offer = AddButton(offersCard.transform, string.Empty, new Vector2(-198, 244 - i * 104), new Vector2(612, 86),
                    () => SelectShopOffer(index), new Color(.20f, .14f, .08f));
                shopOfferButtons[i] = offer.GetComponent<Button>();
                shopOfferImages[i] = offer.GetComponent<Image>();
                shopOfferTexts[i] = offer.GetComponentInChildren<Text>();
                shopOfferTexts[i].alignment = TextAnchor.MiddleLeft;
                shopOfferTexts[i].fontSize = 17;
                shopOfferTexts[i].lineSpacing = .92f;
                var offerTextRect = shopOfferTexts[i].rectTransform;
                offerTextRect.sizeDelta = new Vector2(548, 70);
                offerTextRect.anchoredPosition = new Vector2(18, 0);
            }

            var detail = Panel("Selected Offer", offersCard.transform, Color.white);
            DarkFantasySkin.Apply(detail.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.45f, .31f, .17f));
            SetRect(detail.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(342, 500), new Vector2(334, 36));
            AddText(detail.transform, "ВЫБРАНО", 14, new Vector2(0, 208), new Vector2(280, 24),
                new Color(.67f, .55f, .37f), TextAnchor.MiddleLeft);
            shopSelectionTitle = AddText(detail.transform, "ВЫБЕРИТЕ УСИЛЕНИЕ", 22, new Vector2(0, 158), new Vector2(280, 68),
                DarkFantasySkin.Text, TextAnchor.MiddleLeft);
            shopSelectionTitle.font = boldFont;
            shopSelectionDescription = AddText(detail.transform, "Сначала осмотрите предложение на витрине.", 16, new Vector2(0, 62), new Vector2(280, 100),
                new Color(.78f, .76f, .69f), TextAnchor.UpperLeft);
            shopSelectionProgress = AddText(detail.transform, string.Empty, 15, new Vector2(0, -62), new Vector2(280, 86),
                DarkFantasySkin.MutedText, TextAnchor.UpperLeft);
            var buy = AddButton(detail.transform, "КУПИТЬ", new Vector2(0, -190), new Vector2(282, 62), PurchaseSelectedShopOffer,
                new Color(.66f, .38f, .12f));
            shopBuyButton = buy.GetComponent<Button>();
            shopBuyText = buy.GetComponentInChildren<Text>();

            AddButton(offersCard.transform, "ПОКИНУТЬ УБЕЖИЩЕ", new Vector2(250, -340), new Vector2(510, 60), game.ContinueAfterShop,
                new Color(.48f, .27f, .10f));
            AddText(offersCard.transform, "Следующая глубина ждёт", 14, new Vector2(-308, -340), new Vector2(370, 28),
                DarkFantasySkin.MutedText, TextAnchor.MiddleLeft);
            shop.SetActive(false);
        }

        private void BuildLevelComplete()
        {
            levelComplete = Panel("Level Complete", canvas.transform, new Color(.005f, .015f, .025f, .9f));
            var card = Panel("Level Complete Card", levelComplete.transform, new Color(.025f, .05f, .075f, .99f));
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(.45f, .85f, 1f));
            SetRect(card.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(720, 460), Vector2.zero);
            AddText(card.transform, "ГЛУБИНА ПРОЙДЕНА", 42, new Vector2(0, 130), new Vector2(620, 80), new Color(.6f, .9f, 1f), TextAnchor.MiddleCenter);
            AddText(card.transform, "Путь вниз открыт. Здоровье не восстанавливается автоматически.", 21,
                new Vector2(0, 35), new Vector2(610, 80), Color.white, TextAnchor.MiddleCenter);
            AddButton(card.transform, "СПУСТИТЬСЯ ГЛУБЖЕ", new Vector2(0, -100), new Vector2(500, 72), game.NextLevel, new Color(.1f, .4f, .55f));
            levelComplete.SetActive(false);
        }

        private void BuildRecords()
        {
            records = Panel("Records", canvas.transform, new Color(0, 0, 0, .91f));
            var card = Panel("Records Card", records.transform, new Color(.028f, .035f, .065f, .99f));
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(820, 760), Vector2.zero);
            AddText(card.transform, "ЛЕТОПИСЬ ПОГРУЖЕНИЙ", 39, new Vector2(0, 310), new Vector2(720, 66),
                new Color(1f, .78f, .35f), TextAnchor.MiddleCenter);
            AddDivider(card.transform, 270, 680);
            var list = AddText(card.transform, "", 19, new Vector2(0, 0), new Vector2(690, 500), new Color(.84f, .82f, .78f), TextAnchor.UpperLeft);
            list.gameObject.name = "Records List";
            AddButton(card.transform, "НАЗАД", new Vector2(0, -315), new Vector2(360, 58), () => records.SetActive(false));
            records.SetActive(false);
        }

        private void ShowRecords()
        {
            var list = records.transform.Find("Records Card/Records List")?.GetComponent<Text>();
            if (list != null)
            {
                var value = " #    ГЕРОЙ       ГЛУБИНА    УБИЙСТВА    ВРЕМЯ\n\n";
                for (var i = 0; i < game.Save.topRecords.Count; i++)
                {
                    var record = game.Save.topRecords[i];
                    var minutes = Mathf.FloorToInt(record.seconds / 60);
                    var seconds = Mathf.FloorToInt(record.seconds % 60);
                    value += $"{i + 1,2}.   {record.hero,-10}   {record.depth,4}         {record.kills,5}       {minutes:00}:{seconds:00}\n";
                }
                if (game.Save.topRecords.Count == 0) value += "Пока нет завершённых забегов.";
                list.text = value;
            }
            records.SetActive(true);
        }

        private void RefreshShop()
        {
            shopGold.text = $"ЗОЛОТО   {game.Gold:0}";
            if (game.Player != null)
            {
                shopHero.sprite = DirectionalSpriteAtlas.HeroPortrait(game.Player.Hero.heroClass);
                if (!shopAnimatorInitialized || shopAnimatedClass != game.Player.Hero.heroClass)
                {
                    shopAnimatedClass = game.Player.Hero.heroClass;
                    shopAnimatorInitialized = true;
                    shopHeroAnimator.Initialize(shopAnimatedClass, .35f);
                }
                // Preview art has different occupied silhouette heights inside the same 256px
                // canvas. Compensate through the RectTransform so every traveller owns the same
                // visual weight in the merchant scene without stretching individual frames.
                shopHero.rectTransform.sizeDelta = shopAnimatedClass == HeroClass.Rogue ? new Vector2(500, 560) :
                    shopAnimatedClass == HeroClass.Warrior ? new Vector2(370, 480) : new Vector2(360, 470);
                shopHeroStats.text = $"{game.Player.Hero.displayName.ToUpperInvariant()}\n\n" +
                                     $"ЗДОРОВЬЕ   {game.Player.Health:0} / {game.Player.MaxHealth:0}\n" +
                                     $"УРОН       {game.Player.Damage:0.#}\n" +
                                     $"ЗАЩИТА     {game.Player.Defense:0.#}\n" +
                                     $"КРИТ       {game.Player.CriticalChance * 100f:0}%";
            }
            for (var i = 0; i < shopOfferTexts.Length; i++)
            {
                if (i >= game.ShopOffers.Length)
                {
                    shopOfferButtons[i].gameObject.SetActive(false);
                    continue;
                }
                shopOfferButtons[i].gameObject.SetActive(true);
                var offer = game.ShopOffers[i];
                var count = game.PurchaseCount(offer.id);
                var maxed = count >= offer.maxPurchases;
                var sold = game.IsShopOfferSold(i);
                var state = sold ? "ПРОДАНО" : maxed ? "ПРЕДЕЛ" : $"{game.ShopPrice(offer)} ЗОЛ.";
                shopOfferTexts[i].text = $"{offer.name.ToUpperInvariant()}\n{offer.description}    ·    {state}";
                shopOfferButtons[i].interactable = true;
                shopOfferImages[i].color = sold || maxed ? new Color(.42f, .42f, .41f, .72f) :
                    i == selectedShopOffer ? new Color(1.12f, .92f, .62f, 1f) : Color.white;
            }
            RefreshSelectedShopOffer();
        }

        private void SelectShopOffer(int index)
        {
            if (index < 0 || index >= game.ShopOffers.Length) return;
            selectedShopOffer = index;
            RefreshShop();
        }

        private void PurchaseSelectedShopOffer()
        {
            if (selectedShopOffer < 0) return;
            if (game.BuyShopOffer(selectedShopOffer)) RefreshShop();
        }

        private void RefreshSelectedShopOffer()
        {
            if (selectedShopOffer < 0 || selectedShopOffer >= game.ShopOffers.Length)
            {
                shopSelectionTitle.text = "ВЫБЕРИТЕ УСИЛЕНИЕ";
                shopSelectionDescription.text = "Осмотрите предложение на витрине. Покупка произойдёт только после подтверждения.";
                shopSelectionProgress.text = string.Empty;
                shopBuyText.text = "КУПИТЬ";
                shopBuyButton.interactable = false;
                return;
            }

            var offer = game.ShopOffers[selectedShopOffer];
            var count = game.PurchaseCount(offer.id);
            var price = game.ShopPrice(offer);
            var sold = game.IsShopOfferSold(selectedShopOffer);
            var maxed = count >= offer.maxPurchases;
            shopSelectionTitle.text = offer.name.ToUpperInvariant();
            shopSelectionDescription.text = offer.description;
            shopSelectionProgress.text = $"РАЗВИТИЕ   {count} / {offer.maxPurchases}\n" +
                                         $"СТОИМОСТЬ   {price} ЗОЛ.\n\nОдна покупка за визит";
            shopBuyButton.interactable = !sold && !maxed && game.Gold >= price;
            shopBuyText.text = sold ? "ПРОДАНО" : maxed ? "ПРЕДЕЛ ДОСТИГНУТ" :
                game.Gold < price ? $"НЕ ХВАТАЕТ {price - game.Gold} ЗОЛ." : $"КУПИТЬ ЗА {price}";
        }

        private void CreateHeroCard(Transform parent, HeroClass heroClass, int index, Vector2 position, float phase)
        {
            var definition = HeroDefinition.Create(heroClass);
            var card = Panel(definition.displayName, parent, new Color(.02f, .021f, .023f, .96f));
            var cardImage = card.GetComponent<Image>();
            DarkFantasySkin.Apply(cardImage, DarkFantasySkin.Tooltip);
            var cardButton = card.AddComponent<Button>();
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(() => Select(heroClass));
            card.AddComponent<UIHoverFeedback>().Initialize(1.008f);
            var selectionOutline = card.AddComponent<Outline>();
            selectionOutline.effectColor = new Color(.35f, .29f, .19f, .42f);
            selectionOutline.effectDistance = new Vector2(1f, -1f);
            selectionOutline.useGraphicAlpha = true;
            heroCards[index] = cardImage;
            SetRect(card.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(360, 690), position);

            var portrait = new GameObject("Animated Hero", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            portrait.transform.SetParent(card.transform, false);
            portrait.sprite = DirectionalSpriteAtlas.HeroPortrait(heroClass);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var portraitWidth = heroClass == HeroClass.Mage ? 360f : 340f;
            SetRect(portrait.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(portraitWidth, 430), new Vector2(0, 92));
            portrait.gameObject.AddComponent<UIHeroIdleAnimator>().Initialize(heroClass, phase * .17f);

            AddText(card.transform, definition.displayName.ToUpperInvariant(), 28, new Vector2(0, -157), new Vector2(320, 42),
                new Color(.92f, .84f, .7f), TextAnchor.MiddleCenter);
            AddText(card.transform, definition.description, 16, new Vector2(0, -215), new Vector2(310, 72),
                new Color(.72f, .7f, .66f), TextAnchor.MiddleCenter);
            AddText(card.transform, $"HP  {definition.maxHealth:0}     УРОН  {definition.damage:0}     ЗАЩИТА  {definition.defense:0}",
                15, new Vector2(0, -263), new Vector2(320, 32), new Color(.79f, .65f, .42f), TextAnchor.MiddleCenter);
            AddButton(card.transform, "ВЫБРАТЬ", new Vector2(0, -310), new Vector2(300, 54), () => Select(heroClass));
        }

        private void ShowTitlePage()
        {
            if (menuNavigationShade != null) menuNavigationShade.SetActive(true);
            if (titlePage != null) titlePage.SetActive(true);
            if (heroSelectPage != null) heroSelectPage.SetActive(false);
        }

        private void ShowHeroSelect()
        {
            if (menuNavigationShade != null) menuNavigationShade.SetActive(false);
            if (titlePage != null) titlePage.SetActive(false);
            if (heroSelectPage != null) heroSelectPage.SetActive(true);
            Select(selected);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Select(HeroClass heroClass)
        {
            selected = heroClass;
            game.SelectHero(heroClass);
            var definition = HeroDefinition.Create(heroClass);
            if (heroInfo != null)
            {
                heroInfo.text = $"ВЫБРАНО: {definition.displayName.ToUpperInvariant()}";
                heroInfo.color = new Color(.82f, .72f, .54f);
            }
            for (var i = 0; i < heroCards.Length; i++)
            {
                if (heroCards[i] == null) continue;
                var active = i == (int)heroClass;
                heroCards[i].color = active ? Color.Lerp(Color.white, DarkFantasySkin.Gold, .08f) : new Color(.82f, .82f, .82f, .94f);
                var outline = heroCards[i].GetComponent<Outline>();
                if (outline == null) continue;
                outline.effectColor = active ? new Color(.92f, .61f, .22f, .9f) : new Color(.25f, .22f, .17f, .38f);
                outline.effectDistance = active ? new Vector2(1.5f, -1.5f) : new Vector2(1f, -1f);
            }
        }

        public void ShowMenu()
        {
            menu.SetActive(true);
            ShowTitlePage();
            hud.SetActive(false);
            pause.SetActive(false);
            settingsMenu.SetActive(false);
            shop.SetActive(false);
            levelComplete.SetActive(false);
            records.SetActive(false);
            gameOver.SetActive(false);
            GameInput.Reset();
        }

        public void ShowGame()
        {
            menu.SetActive(false);
            pause.SetActive(false);
            gameOver.SetActive(false);
            shop.SetActive(false);
            levelComplete.SetActive(false);
            hud.SetActive(true);
            Refresh();
        }

        public void ShowPause(bool visible)
        {
            pause.SetActive(visible);
            GameInput.Reset();
        }

        public void ShowShop()
        {
            menu.SetActive(false);
            hud.SetActive(false);
            pause.SetActive(false);
            gameOver.SetActive(false);
            shop.SetActive(true);
            levelComplete.SetActive(false);
            selectedShopOffer = game.ShopOffers.Length > 0 ? 0 : -1;
            RefreshShop();
            GameInput.Reset();
        }

        public void ShowLevelComplete()
        {
            hud.SetActive(false);
            pause.SetActive(false);
            shop.SetActive(false);
            levelComplete.SetActive(true);
            GameInput.Reset();
        }

        public void ShowGameOver()
        {
            hud.SetActive(false);
            pause.SetActive(false);
            gameOver.SetActive(true);
            gameOverStats.text = $"Достигнутая глубина: {game.Depth}\nПобеждено врагов: {game.SessionKills}\nСобрано золота: {game.Gold}";
            GameInput.Reset();
        }

        private void Refresh()
        {
            if (stats == null || game == null || game.Player == null || game.Inventory == null) return;
            stats.text = $"{game.Player.Hero.displayName}   •   ГЛУБИНА {game.Depth}   •   ВРАГИ {EnemyController.Count}   •   {game.Gold} ЗОЛ.";
            heroPortrait.sprite = DirectionalSpriteAtlas.HeroPortrait(game.Player.Hero.heroClass);
            healthFill.fillAmount = game.Player.MaxHealth <= 0 ? 0 : game.Player.Health / game.Player.MaxHealth;
            healthText.text = $"{game.Player.Health:0} / {game.Player.MaxHealth:0}";
            var abilityRemaining = game.Player.AbilityCooldownRemaining;
            var abilityReady = abilityRemaining <= .05f;
            if (abilityText != null)
            {
                abilityText.text = abilityReady
                    ? $"<color=#F2C36B><b>{AbilityName(game.Player.Hero.heroClass)}</b></color>\nQ"
                    : $"<color=#B8B1A5>{AbilityName(game.Player.Hero.heroClass)}</color>\n<b>{abilityRemaining:0.0}</b>";
                abilityText.color = abilityReady ? new Color(1f, .86f, .58f) : new Color(.76f, .74f, .7f);
            }
            if (abilityCooldownFill != null)
            {
                abilityCooldownFill.gameObject.SetActive(!abilityReady);
                abilityCooldownFill.fillAmount = Mathf.Clamp01(abilityRemaining / AbilityCooldownDuration(game.Player.Hero.heroClass));
            }
            if (abilityFrame != null)
                abilityFrame.color = abilityReady ? new Color(1f, .88f, .58f, 1f) : new Color(.62f, .62f, .62f, 1f);
            for (var i = 0; i < quickSlotTexts.Length; i++)
            {
                var baseId = game.Inventory.QuickSlots[i];
                var item = string.IsNullOrEmpty(baseId) ? null : LegacyCatalog.Item(baseId);
                var count = string.IsNullOrEmpty(baseId) ? 0 : game.Inventory.Count(baseId);
                var instance = FindInventoryItem(baseId);
                quickSlotTexts[i].text = item == null ? $"{i + 1}\n—" : $"{i + 1}     ×{count}";
                quickSlotIcons[i].gameObject.SetActive(instance != null);
                if (instance != null) quickSlotIcons[i].sprite = RuntimeItemIcons.Get(instance);
            }
            EnemyController activeBoss = null;
            foreach (var enemy in EnemyController.Snapshot()) if (enemy != null && enemy.IsBoss) { activeBoss = enemy; break; }
            bossBar.SetActive(activeBoss != null);
            if (activeBoss != null)
            {
                bossName.text = activeBoss.DisplayName;
                bossFill.fillAmount = activeBoss.MaxHealth <= 0 ? 0 : activeBoss.Health / activeBoss.MaxHealth;
            }
            game.Player.GetStatusSnapshots(visibleStatuses);
            var effectText = "";
            var visibleCount = Mathf.Min(visibleStatuses.Count, 4);
            for (var i = 0; i < visibleCount; i++)
            {
                var effect = visibleStatuses[i];
                var color = effect.Negative ? "#E57A69" : "#D5B563";
                if (i > 0) effectText += "      ";
                effectText += $"<color={color}><b>{(effect.Negative ? "▼" : "▲")} {effect.Label}</b>" +
                              (effect.Remaining > .05f ? $"  ·  {effect.Remaining:0.0}с" : "") + "</color>";
            }
            if (visibleStatuses.Count > visibleCount) effectText += $"      +{visibleStatuses.Count - visibleCount}";
            statusEffects.text = effectText;
            var hasStatuses = visibleStatuses.Count > 0;
            statusPanel.SetActive(hasStatuses);
            if (hasStatuses)
            {
                var panelWidth = Mathf.Clamp(250f + (visibleCount - 1) * 145f, 250f, 720f);
                statusPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(panelWidth, 50);
                statusEffects.rectTransform.sizeDelta = new Vector2(panelWidth - 36, 30);
            }
            var portalDistance = ExitPortal.DistanceToNearest(game.Player);
            var chestDistance = TreasureChest.DistanceToNearest(game.Player);
            if (portalDistance <= 1.45f)
                interactionHint.text = EnemyController.Count > 0
                    ? $"[E] СПУСТИТЬСЯ  ·  ВРАГОВ ОСТАНЕТСЯ {EnemyController.Count}"
                    : "[E] СПУСТИТЬСЯ ГЛУБЖЕ";
            else
                interactionHint.text = chestDistance <= 1.35f
                    ? (EnemyController.FindNearest(game.Player.transform.position, 150f / 32f) == null ? "[E] ОТКРЫТЬ СУНДУК" : "СУНДУК НЕДОСТУПЕН В БОЮ")
                    : "";
            interactionPrompt.SetActive(!string.IsNullOrEmpty(interactionHint.text));
        }

        private ItemInstance FindInventoryItem(string baseId)
        {
            if (string.IsNullOrEmpty(baseId)) return null;
            foreach (var item in game.Inventory.Slots)
                if (item != null && item.baseId == baseId) return item;
            return null;
        }

        private static string AbilityName(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Rogue: return "РЫВОК";
                case HeroClass.Warrior: return "СТРАЖ";
                default: return "ВЗРЫВ";
            }
        }

        private static float AbilityCooldownDuration(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Rogue: return 3f;
                case HeroClass.Warrior: return 8f;
                default: return 12f;
            }
        }

        private void ShowToast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toast.text = message;
            yield return new WaitForSecondsRealtime(2.5f);
            toast.text = string.Empty;
        }

        private GameObject Panel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private void AddDivider(Transform parent, float y, float width)
        {
            var divider = Panel("Divider", parent, new Color(.72f, .51f, .22f, .72f));
            var image = divider.GetComponent<Image>();
            image.sprite = null;
            image.raycastTarget = false;
            SetRect(divider.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(width, 1.5f), new Vector2(0, y));
        }

        private Image AddBrandIcon(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var icon = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(parent, false);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position);
            return icon;
        }

        private void SetButtonIcon(GameObject button, Sprite sprite, Vector2 size)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = string.Empty;
            AddBrandIcon(button.transform, "Icon", sprite, Vector2.zero, size);
        }

        private static void StyleHudElement(GameObject target, Color border)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
            }
            var outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        private Text AddText(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var label = textObject.GetComponent<Text>();
            label.font = size >= 32 ? headingFont : font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = size >= 32 ? FontStyle.Bold : FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
            label.raycastTarget = false;
            SetRect(textObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dimensions, position);
            return label;
        }

        private GameObject AddButton(Transform parent, string label, Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dimensions, position);
            var image = buttonObject.GetComponent<Image>();
            DarkFantasySkin.Apply(image, DarkFantasySkin.Button,
                color.HasValue ? Color.Lerp(Color.white, color.Value, 0.25f) : Color.white);
            var button = buttonObject.GetComponent<Button>();
            if (action != null) button.onClick.AddListener(action);
            buttonObject.AddComponent<UIHoverFeedback>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.08f, .92f, 1f);
            colors.pressedColor = new Color(.72f, .62f, .48f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.35f, .33f, .3f, .65f);
            colors.fadeDuration = .09f;
            button.colors = colors;
            var buttonText = AddText(buttonObject.transform, label,
                Mathf.Clamp(Mathf.RoundToInt(dimensions.y * .32f), 15, 23), Vector2.zero,
                dimensions - new Vector2(18, 12), Color.white, TextAnchor.MiddleCenter);
            buttonText.font = boldFont;
            buttonText.fontStyle = FontStyle.Normal;
            return buttonObject;
        }

        private Slider AddSlider(Transform parent, Vector2 position, float value)
        {
            var root = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(420, 32), position);

            var background = Panel("Track", root.transform, Color.white);
            SetRect(background.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(400, 10), Vector2.zero);
            DarkFantasySkin.Apply(background.GetComponent<Image>(), DarkFantasySkin.HealthBar);

            var fillArea = Panel("Fill Area", root.transform, Color.clear);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, .5f);
            fillAreaRect.anchorMax = new Vector2(1, .5f);
            fillAreaRect.sizeDelta = new Vector2(-20, 8);
            fillAreaRect.anchoredPosition = Vector2.zero;
            var fill = Panel("Fill", fillArea.transform, Color.white);
            fill.GetComponent<Image>().sprite = DarkFantasySkin.GoldFill;
            fill.GetComponent<Image>().type = Image.Type.Sliced;

            var handleArea = Panel("Handle Slide Area", root.transform, Color.clear);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, .5f);
            handleAreaRect.anchorMax = new Vector2(1, .5f);
            handleAreaRect.sizeDelta = new Vector2(-20, 24);
            handleAreaRect.anchoredPosition = Vector2.zero;
            var handle = Panel("Handle", handleArea.transform, Color.white);
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(18, 22), Vector2.zero);
            DarkFantasySkin.Apply(handle.GetComponent<Image>(), DarkFantasySkin.Button);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            return slider;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
