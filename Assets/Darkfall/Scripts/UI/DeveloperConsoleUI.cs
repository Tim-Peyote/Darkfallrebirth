#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Darkfall.UI
{
    /// <summary>Runtime QA console. Compiled only for the Editor and Development Builds.</summary>
    public sealed class DeveloperConsoleUI : MonoBehaviour
    {
        private sealed class EnemyChoice
        {
            public LegacyEnemy Definition;
            public bool Boss;
            public string Label;
        }

        private GameManager game;
        private Font font;
        private Font boldFont;
        private GameObject root;
        private Text status;
        private Dropdown itemDropdown;
        private Dropdown enemyDropdown;
        private Text godModeLabel;
        private readonly List<LegacyItem> consumables = new List<LegacyItem>();
        private readonly List<EnemyChoice> enemies = new List<EnemyChoice>();
        private readonly InputField[] statInputs = new InputField[6];
        private int itemIndex;
        private int enemyIndex;

        public void Initialize(GameManager manager, Font regular, Font bold)
        {
            game = manager;
            font = regular;
            boldFont = bold ?? regular;
            foreach (var item in LegacyCatalog.Data.items)
                if (item.type == "consumable" || item.baseId.StartsWith("scroll_") || item.baseId == "mystery_scroll")
                    consumables.Add(item);
            foreach (var enemy in LegacyCatalog.Data.enemies)
                enemies.Add(new EnemyChoice { Definition = enemy, Label = enemy.type });
            foreach (var boss in LegacyCatalog.Data.bosses)
                enemies.Add(new EnemyChoice { Definition = boss, Boss = true, Label = "БОСС · " + boss.type });
            enemies.Add(new EnemyChoice
            {
                Label = "ОСОБЫЙ · Chest Mimic",
                Definition = new LegacyEnemy
                {
                    type = "Chest Mimic", color = "#FFFFFF", hp = 48, damage = 21, speed = 68,
                    attackRange = 46, reward = 38, levelRequirement = 1, levelTier = 1,
                    abilities = Array.Empty<string>()
                }
            });
            Build();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote) || UnityEngine.Input.GetKeyDown(KeyCode.F10)) Toggle();
        }

        private void Toggle()
        {
            if (root == null) return;
            var open = !root.activeSelf;
            root.SetActive(open);
            game.SetDeveloperConsoleOpen(open);
            if (open) Refresh();
        }

        private void Close()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            game.SetDeveloperConsoleOpen(false);
        }

        private void Build()
        {
            root = Panel("Developer Console", transform, new Color(.004f, .006f, .008f, .88f));
            root.transform.SetAsLastSibling();
            var card = Panel("Console Surface", root.transform, Color.white);
            DarkFantasySkin.Apply(card.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(.45f, .53f, .58f));
            SetRect(card.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(1540, 850), Vector2.zero);

            var header = Panel("Console Header", card.transform, Color.white);
            DarkFantasySkin.Apply(header.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.31f, .39f, .43f));
            SetRect(header.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(1490, 88), new Vector2(0, 364));
            Text(header.transform, "DARKFALL · РЕЖИМ РАЗРАБОТЧИКА", 28, new Vector2(-213, 14), new Vector2(1000, 42),
                new Color(.82f, .9f, .93f), TextAnchor.MiddleLeft, true);
            Text(header.transform, "` / F10 — открыть или закрыть · изменения действуют только в текущем забеге", 14,
                new Vector2(-213, -18), new Vector2(1000, 24), DarkFantasySkin.MutedText, TextAnchor.MiddleLeft);
            Button(header.transform, "ЗАКРЫТЬ", new Vector2(632, 0), new Vector2(176, 46), Close);

            BuildProgressColumn(card.transform);
            BuildSpawnColumn(card.transform);
            BuildStatsColumn(card.transform);

            status = Text(card.transform, "ГОТОВО", 14, new Vector2(0, -386), new Vector2(1390, 32),
                new Color(.57f, .72f, .67f), TextAnchor.MiddleLeft);
            root.SetActive(false);
        }

        private void BuildProgressColumn(Transform parent)
        {
            var panel = Section(parent, "ПРОГРЕСС И ЭКОНОМИКА", new Vector2(-510, -20), new Vector2(430, 650));
            Button(panel, "СЛЕДУЮЩАЯ ГЛУБИНА", new Vector2(0, 240), new Vector2(356, 54), () =>
            {
                game.DeveloperAdvanceLevel(); Refresh(); SetStatus("Создана следующая глубина");
            });
            Button(panel, "ОТКРЫТЬ МАГАЗИН", new Vector2(0, 174), new Vector2(356, 54), () =>
            {
                Close(); game.DeveloperOpenShop();
            });
            Button(panel, "+100 ЗОЛОТА", new Vector2(-92, 94), new Vector2(170, 52), () => AddGold(100));
            Button(panel, "+1000 ЗОЛОТА", new Vector2(92, 94), new Vector2(170, 52), () => AddGold(1000));
            Button(panel, "ПОЛНОЕ ЛЕЧЕНИЕ", new Vector2(0, 18), new Vector2(356, 52), () =>
            {
                if (game.Player == null) return; game.Player.Heal(game.Player.MaxHealth); Refresh(); SetStatus("Здоровье восстановлено");
            });
            var god = Button(panel, "БЕССМЕРТИЕ", new Vector2(0, -48), new Vector2(356, 58), ToggleGodMode,
                new Color(.42f, .16f, .12f));
            godModeLabel = god.GetComponentInChildren<Text>();
            Text(panel, "Быстрые проверки", 15, new Vector2(0, -128), new Vector2(356, 26),
                new Color(.72f, .65f, .54f), TextAnchor.MiddleLeft, true);
            Button(panel, "УБИТЬ ВСЕХ ВРАГОВ", new Vector2(0, -174), new Vector2(356, 46), KillAllEnemies);
            Button(panel, "ОТКРЫТЬ ПОРТАЛ", new Vector2(0, -230), new Vector2(356, 46), () =>
            {
                ExitPortal.Active?.Empower(); SetStatus("Портал активирован");
            });
        }

        private void BuildSpawnColumn(Transform parent)
        {
            var panel = Section(parent, "ПРЕДМЕТЫ И СПАВН", new Vector2(0, -20), new Vector2(520, 650));
            Text(panel, "ЗЕЛЬЕ ИЛИ СВИТОК", 14, new Vector2(0, 252), new Vector2(440, 24),
                DarkFantasySkin.MutedText, TextAnchor.MiddleLeft, true);
            itemDropdown = ChoiceDropdown(panel, new Vector2(0, 202), new Vector2(440, 50));
            itemDropdown.onValueChanged.AddListener(value => itemIndex = value);
            Button(panel, "ДОБАВИТЬ В РЮКЗАК", new Vector2(0, 142), new Vector2(440, 50), AddSelectedItem,
                new Color(.18f, .32f, .22f));

            Divider(panel, 92, 440);
            Text(panel, "ПРОТИВНИК РЯДОМ С ГЕРОЕМ", 14, new Vector2(0, 60), new Vector2(440, 24),
                DarkFantasySkin.MutedText, TextAnchor.MiddleLeft, true);
            enemyDropdown = ChoiceDropdown(panel, new Vector2(0, 10), new Vector2(440, 50));
            enemyDropdown.onValueChanged.AddListener(value => enemyIndex = value);
            Button(panel, "СОЗДАТЬ ПРОТИВНИКА", new Vector2(0, -50), new Vector2(440, 50), SpawnSelectedEnemy,
                new Color(.38f, .16f, .15f));
            Text(panel, "Противник ищет ближайшую свободную точку в радиусе 1.5–4.5 клетки.", 14,
                new Vector2(0, -114), new Vector2(440, 54), DarkFantasySkin.MutedText, TextAnchor.UpperLeft);
            Text(panel, "Нажмите на поле и выберите нужный тип из прокручиваемого списка.", 14,
                new Vector2(0, -198), new Vector2(440, 44), DarkFantasySkin.MutedText, TextAnchor.MiddleLeft);
        }

        private void BuildStatsColumn(Transform parent)
        {
            var panel = Section(parent, "ПОКАЗАТЕЛИ ГЕРОЯ", new Vector2(535, -20), new Vector2(450, 650));
            var labels = new[] { "МАКС. HP", "УРОН", "ЗАЩИТА", "СКОРОСТЬ", "КРИТ, %", "РАДИУС АТАКИ" };
            for (var i = 0; i < labels.Length; i++)
            {
                var y = 250 - i * 74;
                Text(panel, labels[i], 14, new Vector2(-96, y), new Vector2(170, 28),
                    DarkFantasySkin.MutedText, TextAnchor.MiddleLeft, true);
                statInputs[i] = Input(panel, new Vector2(92, y), new Vector2(150, 42));
            }
            Button(panel, "ПРИМЕНИТЬ ПОКАЗАТЕЛИ", new Vector2(0, -220), new Vector2(370, 54), ApplyStats,
                new Color(.18f, .30f, .36f));
            Button(panel, "СЧИТАТЬ ТЕКУЩИЕ", new Vector2(0, -282), new Vector2(370, 44), Refresh);
        }

        private Transform Section(Transform parent, string title, Vector2 position, Vector2 size)
        {
            var section = Panel(title, parent, Color.white);
            DarkFantasySkin.Apply(section.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetRect(section.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position);
            Text(section.transform, title, 17, new Vector2(0, size.y * .5f - 32), new Vector2(size.x - 58, 30),
                new Color(.82f, .7f, .48f), TextAnchor.MiddleLeft, true);
            return section.transform;
        }

        private Text Selector(Transform parent, Vector2 position, Action previous, Action next)
        {
            Button(parent, "<", position + Vector2.left * 202, new Vector2(42, 46), () => previous());
            var label = Text(parent, string.Empty, 15, position, new Vector2(342, 46), DarkFantasySkin.Text, TextAnchor.MiddleCenter, true);
            var surface = label.gameObject.AddComponent<Outline>();
            surface.effectColor = new Color(.36f, .29f, .18f, .55f);
            surface.effectDistance = new Vector2(1, -1);
            Button(parent, ">", position + Vector2.right * 202, new Vector2(42, 46), () => next());
            return label;
        }

        private Dropdown ChoiceDropdown(Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position);
            DarkFantasySkin.Apply(root.GetComponent<Image>(), DarkFantasySkin.Button, new Color(.16f, .23f, .27f));

            var caption = Text(root.transform, "Выберите…", 15, new Vector2(-10, 0), size - new Vector2(58, 8),
                DarkFantasySkin.Text, TextAnchor.MiddleLeft, true);
            Text(root.transform, "▼", 15, new Vector2(size.x * .5f - 26, 0), new Vector2(30, size.y - 8),
                new Color(.85f, .68f, .38f), TextAnchor.MiddleCenter, true);

            var template = Panel("Template", root.transform, Color.white);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, -4);
            templateRect.sizeDelta = new Vector2(0, 260);
            DarkFantasySkin.Apply(template.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(.38f, .48f, .52f));

            var scroll = template.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            var viewport = Panel("Viewport", template.transform, new Color(.01f, .015f, .018f, .98f));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4, 4);
            viewportRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 42);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content, false);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, .5f);
            itemRect.anchorMax = new Vector2(1, .5f);
            itemRect.sizeDelta = new Vector2(0, 42);
            var itemBackground = Panel("Item Background", item.transform, new Color(.05f, .065f, .07f, .98f));
            var itemBackgroundRect = itemBackground.GetComponent<RectTransform>();
            itemBackgroundRect.anchorMin = Vector2.zero;
            itemBackgroundRect.anchorMax = Vector2.one;
            itemBackgroundRect.offsetMin = new Vector2(2, 1);
            itemBackgroundRect.offsetMax = new Vector2(-2, -1);
            var checkmark = Panel("Item Checkmark", itemBackground.transform, new Color(.86f, .55f, .18f, 1f));
            SetRect(checkmark.GetComponent<RectTransform>(), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(4, 30), new Vector2(5, 0));
            var itemLabel = Text(item.transform, "Option", 14, new Vector2(12, 0), new Vector2(size.x - 38, 38),
                DarkFantasySkin.Text, TextAnchor.MiddleLeft);
            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBackground.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();

            scroll.viewport = viewportRect;
            scroll.content = content;
            template.SetActive(false);

            var dropdown = root.GetComponent<Dropdown>();
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData>();
            return dropdown;
        }

        private void Refresh()
        {
            if (game == null) return;
            if (consumables.Count > 0)
            {
                itemIndex = Wrap(itemIndex, consumables.Count);
                var item = consumables[itemIndex];
                var type = item.baseId.StartsWith("scroll_") || item.baseId == "mystery_scroll" ? "СВИТОК" : "ЗЕЛЬЕ";
                if (itemDropdown.options.Count != consumables.Count)
                {
                    itemDropdown.ClearOptions();
                    var labels = new List<string>();
                    foreach (var choice in consumables)
                    {
                        var choiceType = choice.baseId.StartsWith("scroll_") || choice.baseId == "mystery_scroll" ? "СВИТОК" : "ЗЕЛЬЕ";
                        labels.Add($"{choiceType} · {choice.name}");
                    }
                    itemDropdown.AddOptions(labels);
                }
                itemDropdown.SetValueWithoutNotify(itemIndex);
                itemDropdown.RefreshShownValue();
            }
            if (enemies.Count > 0)
            {
                enemyIndex = Wrap(enemyIndex, enemies.Count);
                if (enemyDropdown.options.Count != enemies.Count)
                {
                    enemyDropdown.ClearOptions();
                    var labels = new List<string>();
                    foreach (var choice in enemies) labels.Add(choice.Label);
                    enemyDropdown.AddOptions(labels);
                }
                enemyDropdown.SetValueWithoutNotify(enemyIndex);
                enemyDropdown.RefreshShownValue();
            }
            var player = game.Player;
            if (player == null) return;
            godModeLabel.text = game.DeveloperGodMode ? "БЕССМЕРТИЕ: ВКЛ" : "БЕССМЕРТИЕ: ВЫКЛ";
            godModeLabel.color = game.DeveloperGodMode ? new Color(.55f, 1f, .62f) : Color.white;
            var values = new[]
            {
                player.MaxHealth, player.Damage, player.Defense, player.BaseMoveSpeed,
                player.CriticalChance * 100f, player.AttackRange
            };
            for (var i = 0; i < statInputs.Length; i++) statInputs[i].text = values[i].ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void ApplyStats()
        {
            if (game.Player == null) return;
            var values = new float[statInputs.Length];
            for (var i = 0; i < values.Length; i++)
                if (!float.TryParse(statInputs[i].text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                {
                    SetStatus("Некорректное число: " + statInputs[i].text, true);
                    return;
                }
            game.Player.ApplyDeveloperStats(values[0], values[1], values[2], values[3], values[4], values[5]);
            Refresh();
            SetStatus("Показатели героя применены");
        }

        private void ToggleGodMode()
        {
            if (game.Player == null) return;
            game.SetDeveloperGodMode(!game.DeveloperGodMode);
            Refresh();
            SetStatus(game.DeveloperGodMode ? "Бессмертие включено" : "Бессмертие выключено");
        }

        private void AddGold(int amount) { game.AddGold(amount); SetStatus($"Добавлено {amount} золота"); }
        private void AddSelectedItem()
        {
            if (consumables.Count == 0) return;
            var item = consumables[Wrap(itemIndex, consumables.Count)];
            SetStatus(game.DeveloperAddItem(item.baseId) ? "Предмет добавлен: " + item.name : "Рюкзак заполнен", true);
        }
        private void SpawnSelectedEnemy()
        {
            if (enemies.Count == 0) return;
            var choice = enemies[Wrap(enemyIndex, enemies.Count)];
            SetStatus(game.DeveloperSpawnEnemy(choice.Definition, choice.Boss) ? "Создан: " + choice.Label : "Не удалось создать", true);
        }
        private void KillAllEnemies()
        {
            foreach (var enemy in EnemyController.Snapshot()) if (enemy != null) enemy.TakeDamage(9999999);
            SetStatus("Все активные противники уничтожены");
        }
        private static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;
        private void SetStatus(string message, bool warning = false)
        {
            if (status == null) return;
            status.text = message;
            status.color = warning ? new Color(1f, .58f, .4f) : new Color(.57f, .86f, .67f);
        }

        private GameObject Panel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Text Text(Transform parent, string value, int size, Vector2 position, Vector2 dimensions,
            Color color, TextAnchor alignment, bool bold = false)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = bold ? boldFont : font;
            text.fontSize = size; text.text = value; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            SetRect(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), dimensions, position);
            return text;
        }

        private GameObject Button(Transform parent, string label, Vector2 position, Vector2 size, Action action, Color? tint = null)
        {
            var root = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position);
            DarkFantasySkin.Apply(root.GetComponent<Image>(), DarkFantasySkin.Button, tint);
            var button = root.GetComponent<Button>();
            if (action != null) button.onClick.AddListener(() => action());
            root.AddComponent<UIHoverFeedback>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.06f, .92f); colors.pressedColor = new Color(.7f, .62f, .5f);
            button.colors = colors;
            Text(root.transform, label, Mathf.Clamp(Mathf.RoundToInt(size.y * .31f), 14, 19), Vector2.zero,
                size - new Vector2(18, 10), Color.white, TextAnchor.MiddleCenter, true);
            return root;
        }

        private InputField Input(Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject("Value", typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), size, position);
            DarkFantasySkin.Apply(root.GetComponent<Image>(), DarkFantasySkin.Slot);
            var value = Text(root.transform, string.Empty, 16, Vector2.zero, size - new Vector2(22, 8),
                DarkFantasySkin.Text, TextAnchor.MiddleRight, true);
            value.supportRichText = false;
            var field = root.GetComponent<InputField>();
            field.textComponent = value;
            field.contentType = InputField.ContentType.DecimalNumber;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        private GameObject Divider(Transform parent, float y, float width)
        {
            var divider = Panel("Divider", parent, new Color(.56f, .46f, .29f, .66f));
            SetRect(divider.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(width, 1), new Vector2(0, y));
            return divider;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.sizeDelta = size; rect.anchoredPosition = position;
        }
    }
}
#endif
