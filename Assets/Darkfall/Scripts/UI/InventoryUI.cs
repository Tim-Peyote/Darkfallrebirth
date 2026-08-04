using System;
using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Darkfall.UI
{
    public enum InventoryDragArea { Backpack, Equipment, QuickSlot, Chest }

    public sealed class InventoryUI : MonoBehaviour
    {
        private static readonly string[] EquipmentNames =
            { "ГОЛОВА", "ОРУЖИЕ I", "БРОНЯ", "ОРУЖИЕ II", "АМУЛЕТ I", "ПЕРЧАТКИ", "АМУЛЕТ II", "ПОЯС", "БОТИНКИ" };
        private static readonly string[] CharacterStatLabels =
            { "ЗДОРОВЬЕ", "УРОН", "ЗАЩИТА", "ШАНС КРИТА", "СОПР. ОГНЮ", "СОПР. ЛЬДУ" };

        public static InventoryUI Instance { get; private set; }
        private GameManager game;
        private Font font;
        private Font boldFont;
        private Font headingFont;
        private GameObject root;
        private RectTransform backpackGrid;
        private RectTransform equipmentGrid;
        private RectTransform quickGrid;
        private RectTransform chestGrid;
        private GameObject chestPanel;
        private GameObject detailsPanel;
        private Text details;
        private Image detailsIcon;
        private Image equipmentHero;
        private GameObject characterSummaryPanel;
        private Text characterSummaryHeader;
        private readonly Text[] characterStatValues = new Text[6];
        private Image characterHealthFill;
        private Text gold;
        private TreasureChest chest;
        private int selectedIndex = -1;
        private int pendingDelete = -1;
        private GameObject deleteConfirmation;
        private GameObject contextLayer;
        private GameObject contextCard;
        private bool visible;
        private sealed class ContextCommand
        {
            public string label;
            public Action action;
            public ContextCommand(string value, Action callback) { label = value; action = callback; }
        }
        public bool IsOpen => visible;

        public void Initialize(GameManager manager, Font uiFont)
        {
            Instance = this;
            game = manager;
            font = uiFont;
            boldFont = Resources.Load<Font>("Fonts/PTSans-Bold");
            if (boldFont == null) boldFont = font;
            headingFont = boldFont;
            Build();
            root.SetActive(false);
        }

        private void Update()
        {
            if (game.Player != null && GameInput.InventoryPressed && (visible || !game.IsBlockingModal)) Toggle();
        }

        public void Toggle()
        {
            SetVisible(!visible);
            if (!visible) chest = null;
        }

        public void Close()
        {
            if (!visible) return;
            chest = null;
            SetVisible(false);
        }

        public void OpenChest(TreasureChest target)
        {
            chest = target;
            SetVisible(true);
        }

        private void SetVisible(bool value)
        {
            visible = value;
            root.SetActive(value);
            if (!value && contextLayer != null) contextLayer.SetActive(false);
            game.PauseForModal(value);
            if (value) Refresh();
        }

        public void Refresh()
        {
            if (!visible) return;
            RebuildBackpack();
            RebuildEquipment();
            RebuildQuickSlots();
            chestPanel.SetActive(chest != null);
            if (chest != null)
            {
                RebuildChest();
                characterSummaryPanel.SetActive(false);
                SetArea(detailsPanel.GetComponent<RectTransform>(), new Vector2(.02f, .02f), new Vector2(.98f, .47f), Vector2.zero, Vector2.zero);
                detailsIcon.rectTransform.sizeDelta = new Vector2(88, 88);
                detailsIcon.rectTransform.anchoredPosition = new Vector2(0, -55);
                SetArea(details.rectTransform, new Vector2(.06f, .05f), new Vector2(.94f, .65f), Vector2.zero, Vector2.zero);
            }
            else
            {
                characterSummaryPanel.SetActive(true);
                SetArea(detailsPanel.GetComponent<RectTransform>(), new Vector2(.02f, .02f), new Vector2(.98f, .70f), Vector2.zero, Vector2.zero);
                detailsIcon.rectTransform.sizeDelta = new Vector2(112, 112);
                detailsIcon.rectTransform.anchoredPosition = new Vector2(0, -72);
                SetArea(details.rectTransform, new Vector2(.06f, .05f), new Vector2(.94f, .70f), Vector2.zero, Vector2.zero);
            }
            if (equipmentHero != null) equipmentHero.sprite = DirectionalSpriteAtlas.HeroPortrait(game.Player.Hero.heroClass);
            RefreshCharacterSummary();
            gold.text = $"ЗОЛОТО  {game.Gold}";
            if (selectedIndex >= 0)
            {
                if (selectedIndex < game.Inventory.Slots.Length && game.Inventory.Slots[selectedIndex] != null)
                    ShowDetails(game.Inventory.Slots[selectedIndex]);
                else
                {
                    selectedIndex = -1;
                    ShowEmptyDetails();
                }
            }
        }

        private void RefreshCharacterSummary()
        {
            if (characterSummaryHeader == null || game?.Player == null) return;

            var player = game.Player;
            characterSummaryHeader.text = $"{player.Hero.displayName.ToUpperInvariant()}  ·  ИТОГОВЫЕ ПАРАМЕТРЫ";
            if (characterHealthFill != null)
                characterHealthFill.fillAmount = player.MaxHealth <= 0 ? 0 : player.Health / player.MaxHealth;

            var values = new[]
            {
                $"{player.Health:0} / {player.MaxHealth:0}",
                $"{player.Damage:0.#}",
                $"{player.Defense:0.#}",
                $"{player.CriticalChance * 100f:0}%",
                $"{player.FireResistance:0}%",
                $"{player.IceResistance:0}%"
            };
            var accents = new[] { "#D9654C", "#E8E3D6", "#BFC7CB", "#D7BC82", "#DD7A32", "#79A9C5" };

            for (var i = 0; i < characterStatValues.Length; i++)
            {
                if (characterStatValues[i] == null) continue;
                characterStatValues[i].text =
                    $"<size=10><color=#999990>{CharacterStatLabels[i]}</color></size>\n" +
                    $"<size=18><color={accents[i]}><b>{values[i]}</b></color></size>";
            }
        }

        private void RebuildBackpack()
        {
            Clear(backpackGrid);
            for (var i = 0; i < game.Inventory.Slots.Length; i++)
            {
                var index = i;
                var item = game.Inventory.Slots[i];
                var slot = CreateSlot(backpackGrid, item, item == null ? "" : Glyph(item.kind), item?.quantity ?? 0);
                if (index == selectedIndex)
                {
                    var outline = slot.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.effectColor = new Color(.95f, .65f, .22f, 1f);
                        outline.effectDistance = new Vector2(2f, -2f);
                    }
                }
                var interaction = slot.AddComponent<InventorySlotInteraction>();
                interaction.Configure(InventoryDragArea.Backpack, index, item,
                    () => SelectBackpack(index),
                    () => ActivateBackpack(index),
                    position => ShowContextMenu(InventoryDragArea.Backpack, index, item, position),
                    HandleDrop);
            }
        }

        private void RebuildEquipment()
        {
            Clear(equipmentGrid);
            var positions = new[]
            {
                new Vector2(0, 238), new Vector2(-185, 112), new Vector2(185, 112),
                new Vector2(-185, -8), new Vector2(185, -8), new Vector2(-185, -128),
                new Vector2(185, -128), new Vector2(-88, -258), new Vector2(88, -258)
            };
            for (var i = 0; i < game.Inventory.Equipment.Length; i++)
            {
                var index = i;
                var item = game.Inventory.Equipment[i];
                var slot = CreateSlot(equipmentGrid, item, item == null ? EquipmentNames[i] : Glyph(item.kind), 0, 86);
                SetRect(slot.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(78, 78), positions[i] * .88f);
                slot.AddComponent<InventorySlotInteraction>().Configure(InventoryDragArea.Equipment, index, item,
                    () =>
                    {
                        if (game.Inventory.Equipment[index] == null) details.text = EquipmentNames[index] + "\nПустой слот экипировки";
                        else ShowDetails(game.Inventory.Equipment[index]);
                    },
                    () => { game.Inventory.Unequip(index); Refresh(); },
                    position => ShowContextMenu(InventoryDragArea.Equipment, index, item, position), HandleDrop);
            }
        }

        private void RebuildQuickSlots()
        {
            Clear(quickGrid);
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var baseId = game.Inventory.QuickSlots[i];
                var definition = string.IsNullOrEmpty(baseId) ? null : LegacyCatalog.Item(baseId);
                var count = string.IsNullOrEmpty(baseId) ? 0 : game.Inventory.Count(baseId);
                var item = FindBackpackItem(baseId);
                var label = definition == null ? "ПУСТО" : "";
                var slot = CreateSlot(quickGrid, item, label, count, 54);
                AddSlotKey(slot.transform, (i + 1).ToString());
                slot.AddComponent<InventorySlotInteraction>().Configure(InventoryDragArea.QuickSlot, index, item,
                    () => { if (item != null) ShowDetails(item); },
                    () => { game.Inventory.UseQuickSlot(index, game.Player); Refresh(); },
                    position => ShowContextMenu(InventoryDragArea.QuickSlot, index, item, position), HandleDrop);
            }
        }

        private void RebuildChest()
        {
            Clear(chestGrid);
            for (var i = 0; i < chest.Items.Length; i++)
            {
                var index = i;
                var item = chest.Items[i];
                var slot = CreateSlot(chestGrid, item, item == null ? "" : Glyph(item.kind), item?.quantity ?? 0);
                slot.AddComponent<InventorySlotInteraction>().Configure(InventoryDragArea.Chest, index, item,
                    () => { if (item != null) ShowDetails(item); },
                    () => { if (item != null) chest.Take(index); Refresh(); },
                    position => ShowContextMenu(InventoryDragArea.Chest, index, item, position), HandleDrop);
            }
        }

        private void SelectBackpack(int index)
        {
            selectedIndex = index;
            var item = game.Inventory.Slots[index];
            if (item != null) ShowDetails(item);
        }

        private void ActivateBackpack(int index)
        {
            selectedIndex = index;
            game.Inventory.UseOrEquip(index, game.Player);
            Refresh();
        }

        private void AssignSelected(int quickIndex)
        {
            if (game.Inventory.AssignQuickSlot(selectedIndex, quickIndex)) Refresh();
        }

        private void HandleDrop(InventoryDragArea sourceArea, int sourceIndex, InventoryDragArea targetArea, int targetIndex)
        {
            var changed = false;
            if (sourceArea == InventoryDragArea.Backpack && targetArea == InventoryDragArea.Backpack)
            {
                game.Inventory.SwapBackpack(sourceIndex, targetIndex);
                selectedIndex = targetIndex;
                changed = true;
            }
            else if (sourceArea == InventoryDragArea.Backpack && targetArea == InventoryDragArea.Equipment)
                changed = game.Inventory.MoveBackpackToEquipment(sourceIndex, targetIndex, game.Player);
            else if (sourceArea == InventoryDragArea.Equipment && targetArea == InventoryDragArea.Backpack)
            {
                changed = game.Inventory.MoveEquipmentToBackpack(sourceIndex, targetIndex);
                if (changed) selectedIndex = targetIndex;
            }
            else if (sourceArea == InventoryDragArea.Equipment && targetArea == InventoryDragArea.Equipment)
                changed = game.Inventory.SwapEquipment(sourceIndex, targetIndex);
            else if (sourceArea == InventoryDragArea.Backpack && targetArea == InventoryDragArea.QuickSlot)
                changed = game.Inventory.AssignQuickSlot(sourceIndex, targetIndex);
            else if (sourceArea == InventoryDragArea.QuickSlot && targetArea == InventoryDragArea.QuickSlot)
            {
                game.Inventory.SwapQuickSlots(sourceIndex, targetIndex);
                changed = true;
            }
            else if (sourceArea == InventoryDragArea.Chest && targetArea == InventoryDragArea.Backpack && chest != null)
            {
                changed = chest.TakeTo(sourceIndex, targetIndex);
                if (changed) selectedIndex = targetIndex;
            }

            if (!changed)
            {
                GameManager.Instance.ShowMessage("Сюда нельзя поместить этот предмет");
                return;
            }
            Refresh();
        }

        private void ShowContextMenu(InventoryDragArea area, int index, ItemInstance item, Vector2 screenPosition)
        {
            if (contextLayer == null || contextCard == null) return;
            var commands = new List<ContextCommand>();
            switch (area)
            {
                case InventoryDragArea.Backpack when item != null:
                    commands.Add(new ContextCommand(item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll
                        ? "ИСПОЛЬЗОВАТЬ" : "НАДЕТЬ", () => ActivateBackpack(index)));
                    if (item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll)
                    {
                        for (var quick = 0; quick < 3; quick++)
                        {
                            var target = quick;
                            commands.Add(new ContextCommand($"В БЫСТРЫЙ СЛОТ {quick + 1}", () =>
                            {
                                game.Inventory.AssignQuickSlot(index, target);
                                Refresh();
                            }));
                        }
                    }
                    commands.Add(new ContextCommand("ВЫБРОСИТЬ…", () => AskDelete(index)));
                    break;
                case InventoryDragArea.Equipment when item != null:
                    commands.Add(new ContextCommand("СНЯТЬ", () => { game.Inventory.Unequip(index); Refresh(); }));
                    break;
                case InventoryDragArea.QuickSlot:
                    if (item != null) commands.Add(new ContextCommand("ИСПОЛЬЗОВАТЬ", () =>
                    {
                        game.Inventory.UseQuickSlot(index, game.Player);
                        Refresh();
                    }));
                    commands.Add(new ContextCommand("ОЧИСТИТЬ СЛОТ", () => { game.Inventory.ClearQuickSlot(index); Refresh(); }));
                    break;
                case InventoryDragArea.Chest when item != null:
                    commands.Add(new ContextCommand("ЗАБРАТЬ", () => { chest?.Take(index); Refresh(); }));
                    break;
            }
            if (commands.Count == 0) return;

            Clear(contextCard.transform);
            var height = 16 + commands.Count * 46;
            SetRect(contextCard.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(250, height), Vector2.zero);
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var button = MakeButton(contextCard.transform, command.label, () =>
                {
                    contextLayer.SetActive(false);
                    command.action();
                });
                SetRect(button.GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(232, 40), new Vector2(0, -28 - i * 46));
            }

            var rootRect = (RectTransform)root.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPosition, null, out var local);
            var bounds = rootRect.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + 135, bounds.xMax - 135);
            local.y = Mathf.Clamp(local.y, bounds.yMin + height * .5f + 10, bounds.yMax - height * .5f - 10);
            contextCard.GetComponent<RectTransform>().anchoredPosition = local;
            contextLayer.SetActive(true);
            contextLayer.transform.SetAsLastSibling();
        }

        private void AddSlotKey(Transform parent, string value)
        {
            var badge = Panel("Key Badge", parent, new Color(.08f, .07f, .055f, .96f));
            SetRect(badge.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, 22), new Vector2(14, -13));
            var text = AddText(badge.transform, value, 13, TextAnchor.MiddleCenter);
            text.font = boldFont;
            badge.transform.SetAsLastSibling();
        }

        private void AskDelete(int index)
        {
            if (index < 0 || index >= game.Inventory.Slots.Length || game.Inventory.Slots[index] == null) return;
            pendingDelete = index;
            deleteConfirmation.SetActive(true);
        }

        private void ConfirmDelete()
        {
            game.Inventory.DeleteBackpack(pendingDelete);
            pendingDelete = -1;
            selectedIndex = -1;
            deleteConfirmation.SetActive(false);
            details.text = "Предмет выброшен.";
            details.color = Color.white;
            Refresh();
        }

        private void ShowDetails(ItemInstance item)
        {
            if (item == null) return;
            var stats = "";
            AddStat(ref stats, "Урон", item.damage);
            AddStat(ref stats, "Защита", item.defense);
            AddStat(ref stats, "Здоровье", item.maxHp);
            AddStat(ref stats, "Скорость", item.moveSpeed, "%");
            AddStat(ref stats, "Скорость атаки", item.attackSpeed, "%");
            AddStat(ref stats, "Крит", item.crit, "%");
            AddStat(ref stats, "Радиус атаки", item.attackRadius);
            AddStat(ref stats, "Огонь", item.fire);
            AddStat(ref stats, "Лёд", item.ice);
            var affixes = item.affixes == null || item.affixes.Length == 0 ? "" : "\n" + string.Join("\n", item.affixes);
            var rarityColor = ColorUtility.ToHtmlStringRGB(item.Color);
            details.text = $"<color=#{rarityColor}><b>{item.name}</b></color>\n" +
                           $"Уровень {item.itemLevel}  •  {RarityName(item.rarity)}  •  {KindName(item.kind)}\n\n{item.description}" +
                           (string.IsNullOrEmpty(item.requiredClass) ? "" : $"\nКласс: {item.requiredClass}") +
                           (string.IsNullOrEmpty(stats) ? "" : "\n\n<color=#D7BC82>ХАРАКТЕРИСТИКИ</color>\n" + stats.TrimEnd()) + affixes +
                           (item.quantity > 1 ? $"\nКоличество: {item.quantity}" : "");
            details.color = new Color(.88f, .86f, .81f);
            if (detailsIcon != null)
            {
                detailsIcon.sprite = RuntimeItemIcons.Get(item);
                detailsIcon.color = Color.white;
                detailsIcon.gameObject.SetActive(true);
            }
        }

        private void ShowEmptyDetails()
        {
            details.text = "Выберите предмет.\n\nЛКМ — осмотреть\nДвойной клик — использовать\nПКМ — действия\nПеретаскивание — переместить";
            details.color = new Color(.62f, .60f, .56f);
            if (detailsIcon != null) detailsIcon.gameObject.SetActive(false);
        }

        private static void AddStat(ref string text, string name, float value, string suffix = "")
        {
            if (Mathf.Abs(value) > .01f) text += $"+{value:0.#} {name}{suffix}\n";
        }

        private ItemInstance FindBackpackItem(string baseId)
        {
            if (string.IsNullOrEmpty(baseId)) return null;
            foreach (var item in game.Inventory.Slots) if (item != null && item.baseId == baseId) return item;
            return null;
        }

        private GameObject CreateSlot(Transform parent, ItemInstance item, string label, int quantity, float slotSize = 74)
        {
            var slot = Panel("Slot", parent, new Color(.035f, .045f, .075f, .98f));
            DarkFantasySkin.Apply(slot.GetComponent<Image>(), DarkFantasySkin.Slot,
                item == null ? new Color(.55f, .58f, .64f, .8f) : item.Color);
            var layout = slot.AddComponent<LayoutElement>();
            layout.preferredWidth = slotSize;
            layout.preferredHeight = slotSize;
            slot.AddComponent<Button>();
            if (item != null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slot.transform, false);
                var iconSize = slotSize - 18;
                SetRect(iconObject.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(iconSize, iconSize), new Vector2(0, 3));
                var image = iconObject.GetComponent<Image>();
                image.sprite = RuntimeItemIcons.Get(item);
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            var text = item == null ? label : quantity > 1 ? $"×{quantity}" : "";
            var labelText = AddText(slot.transform, text, item == null ? (slotSize >= 90 ? 13 : 12) : 16,
                item == null ? TextAnchor.MiddleCenter : TextAnchor.LowerRight);
            labelText.color = item == null ? new Color(.55f, .58f, .64f) : Color.white;
            return slot;
        }

        private void Build()
        {
            root = Panel("Inventory Overlay", transform, new Color(.004f, .006f, .008f, .92f));
            root.AddComponent<SafeAreaFitter>();
            var window = Panel("Inventory Window", root.transform, Color.white);
            DarkFantasySkin.Apply(window.GetComponent<Image>(), DarkFantasySkin.Panel);
            SetArea(window.GetComponent<RectTransform>(), new Vector2(.025f, .035f), new Vector2(.975f, .965f), Vector2.zero, Vector2.zero);

            var header = Panel("Inventory Header", window.transform, Color.white);
            DarkFantasySkin.Apply(header.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(header.GetComponent<RectTransform>(), new Vector2(.012f, .895f), new Vector2(.988f, .985f), Vector2.zero, Vector2.zero);
            header.GetComponent<Image>().raycastTarget = false;
            var headerLine = Panel("Header Accent", header.transform, DarkFantasySkin.Gold);
            SetRect(headerLine.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(460, 2), new Vector2(0, 1));
            headerLine.GetComponent<Image>().raycastTarget = false;

            AddTextAt(window.transform, "ИНВЕНТАРЬ", 30, new Vector2(0, -43), new Vector2(680, 46), TextAnchor.MiddleCenter,
                new Vector2(.5f, 1), new Vector2(.5f, 1));
            gold = AddTextAt(window.transform, "", 17, new Vector2(-34, -43), new Vector2(280, 36), TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1));

            var equipmentPanel = Panel("Equipped Gear", window.transform, Color.white);
            DarkFantasySkin.Apply(equipmentPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(equipmentPanel.GetComponent<RectTransform>(), new Vector2(.405f, .18f), new Vector2(.69f, .87f), Vector2.zero, Vector2.zero);
            AddTextAt(equipmentPanel.transform, "ЭКИПИРОВКА", 19, new Vector2(0, -27), new Vector2(320, 36), TextAnchor.MiddleCenter,
                new Vector2(.5f,1), new Vector2(.5f,1));
            equipmentHero = new GameObject("Hero Silhouette", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            equipmentHero.transform.SetParent(equipmentPanel.transform, false);
            equipmentHero.preserveAspect = true;
            equipmentHero.color = new Color(.94f, .91f, .84f, .92f);
            equipmentHero.raycastTarget = false;
            SetRect(equipmentHero.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(235, 345), new Vector2(0, -12));
            equipmentGrid = new GameObject("Equipment Slots", typeof(RectTransform)).GetComponent<RectTransform>();
            equipmentGrid.SetParent(equipmentPanel.transform, false);
            Stretch(equipmentGrid);

            var quickPanel = Panel("Quick Assignment", window.transform, Color.white);
            DarkFantasySkin.Apply(quickPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(quickPanel.GetComponent<RectTransform>(), new Vector2(.405f, .07f), new Vector2(.69f, .16f), Vector2.zero, Vector2.zero);
            AddTextAt(quickPanel.transform, "БЫСТРЫЙ ДОСТУП", 15, new Vector2(-135, 0), new Vector2(180, 30), TextAnchor.MiddleLeft);
            quickGrid = MakeGrid(quickPanel.transform, new Vector2(100, 0), new Vector2(220, 58), 3, 54, 10);

            var backpackPanel = Panel("Backpack", window.transform, Color.white);
            DarkFantasySkin.Apply(backpackPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(backpackPanel.GetComponent<RectTransform>(), new Vector2(.02f, .18f), new Vector2(.39f, .87f), Vector2.zero, Vector2.zero);
            AddTextAt(backpackPanel.transform, "РЮКЗАК  ·  42 ЯЧЕЙКИ", 19, new Vector2(0, -27), new Vector2(460, 36), TextAnchor.MiddleCenter,
                new Vector2(.5f,1), new Vector2(.5f,1));
            backpackGrid = MakeGrid(backpackPanel.transform, new Vector2(0, -12), new Vector2(570, 560), 6, 74, 10);

            var actionPanel = Panel("Inventory Actions", window.transform, Color.white);
            DarkFantasySkin.Apply(actionPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(actionPanel.GetComponent<RectTransform>(), new Vector2(.02f, .07f), new Vector2(.39f, .16f), Vector2.zero, Vector2.zero);
            var sort = MakeButton(actionPanel.transform, "СОРТИРОВАТЬ", () => { game.Inventory.SortBackpack(); Refresh(); });
            SetRect(sort.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(155, 46), new Vector2(-188, 0));
            var activate = MakeButton(actionPanel.transform, "НАДЕТЬ / ИСПОЛЬЗОВАТЬ", () => ActivateBackpack(selectedIndex));
            SetRect(activate.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(230, 46), new Vector2(5, 0));
            var delete = MakeButton(actionPanel.transform, "ВЫБРОСИТЬ", () => AskDelete(selectedIndex));
            SetRect(delete.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(145, 46), new Vector2(205, 0));

            var rightPanel = Panel("Inspection Column", window.transform, Color.clear);
            SetArea(rightPanel.GetComponent<RectTransform>(), new Vector2(.705f, .18f), new Vector2(.98f, .87f), Vector2.zero, Vector2.zero);
            rightPanel.GetComponent<Image>().raycastTarget = false;
            characterSummaryPanel = Panel("Character Summary", rightPanel.transform, Color.white);
            DarkFantasySkin.Apply(characterSummaryPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(characterSummaryPanel.GetComponent<RectTransform>(), new Vector2(.02f, .72f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            characterSummaryHeader = AddTextAt(characterSummaryPanel.transform, "", 15, new Vector2(0, -18), new Vector2(390, 28),
                TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1));
            characterSummaryHeader.font = boldFont;
            for (var i = 0; i < CharacterStatLabels.Length; i++)
            {
                var tile = Panel("Stat " + CharacterStatLabels[i], characterSummaryPanel.transform, Color.white);
                DarkFantasySkin.Apply(tile.GetComponent<Image>(), DarkFantasySkin.Slot);
                var column = i % 3;
                var row = i / 3;
                SetRect(tile.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f),
                    new Vector2(116, 54), new Vector2(-126 + column * 126, 18 - row * 62));
                characterStatValues[i] = AddText(tile.transform, CharacterStatLabels[i], 15, TextAnchor.MiddleCenter);
                characterStatValues[i].font = boldFont;
                if (i == 0)
                {
                    var track = Panel("Health Track", tile.transform, new Color(.035f, .03f, .028f, .95f));
                    SetRect(track.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(96, 4), new Vector2(0, 5));
                    track.GetComponent<Image>().raycastTarget = false;
                    characterHealthFill = Panel("Health Fill", track.transform, new Color(.82f, .18f, .12f, 1f)).GetComponent<Image>();
                    characterHealthFill.type = Image.Type.Filled;
                    characterHealthFill.fillMethod = Image.FillMethod.Horizontal;
                    characterHealthFill.raycastTarget = false;
                }
            }
            chestPanel = Panel("Chest", rightPanel.transform, Color.white);
            DarkFantasySkin.Apply(chestPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(chestPanel.GetComponent<RectTransform>(), new Vector2(.02f, .5f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            AddTextAt(chestPanel.transform, "СУНДУК", 22, new Vector2(0, -27), new Vector2(300, 40), TextAnchor.MiddleCenter,
                new Vector2(.5f,1), new Vector2(.5f,1));
            chestGrid = MakeGrid(chestPanel.transform, new Vector2(0, -20), new Vector2(330, 250), 4, 70, 7);

            detailsPanel = Panel("Item Details", rightPanel.transform, Color.white);
            DarkFantasySkin.Apply(detailsPanel.GetComponent<Image>(), DarkFantasySkin.Tooltip);
            SetArea(detailsPanel.GetComponent<RectTransform>(), new Vector2(.02f, .02f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            detailsIcon = new GameObject("Selected Item", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            detailsIcon.transform.SetParent(detailsPanel.transform, false);
            detailsIcon.preserveAspect = true;
            detailsIcon.raycastTarget = false;
            detailsIcon.gameObject.SetActive(false);
            SetRect(detailsIcon.rectTransform, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(132, 132), new Vector2(0, -88));
            details = AddText(detailsPanel.transform, "Выберите предмет.\n\nЛКМ — осмотреть\nДвойной клик — использовать\nПКМ — действия\nПеретаскивание — переместить", 18,
                TextAnchor.UpperLeft);
            SetArea(details.rectTransform, new Vector2(.06f, .05f), new Vector2(.94f, .74f), Vector2.zero, Vector2.zero);
            var close = MakeButton(window.transform, "ЗАКРЫТЬ  [I / TAB]", Toggle);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1,0), new Vector2(1,0), new Vector2(270, 50), new Vector2(-165, 78));

            contextLayer = Panel("Context Layer", root.transform, new Color(0, 0, 0, .01f));
            contextLayer.AddComponent<Button>().onClick.AddListener(() => contextLayer.SetActive(false));
            contextCard = Panel("Context Menu", contextLayer.transform, new Color(.02f, .02f, .019f, .995f));
            DarkFantasySkin.Apply(contextCard.GetComponent<Image>(), DarkFantasySkin.Tooltip, new Color(.88f, .58f, .2f));
            contextCard.AddComponent<CanvasGroup>().blocksRaycasts = true;
            contextLayer.SetActive(false);

            deleteConfirmation = Panel("Delete Confirmation", root.transform, new Color(0, 0, 0, .78f));
            var confirmCard = Panel("Confirm Card", deleteConfirmation.transform, new Color(.08f, .025f, .035f, .99f));
            DarkFantasySkin.Apply(confirmCard.GetComponent<Image>(), DarkFantasySkin.Panel, new Color(1f, .45f, .45f));
            SetRect(confirmCard.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(600, 300), Vector2.zero);
            AddTextAt(confirmCard.transform, "ВЫБРОСИТЬ ПРЕДМЕТ НАВСЕГДА?", 26, new Vector2(0, 75), new Vector2(520, 70), TextAnchor.MiddleCenter);
            var yes = MakeButton(confirmCard.transform, "ВЫБРОСИТЬ", ConfirmDelete);
            SetRect(yes.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(220, 62), new Vector2(-125, -60));
            var no = MakeButton(confirmCard.transform, "ОТМЕНА", () => deleteConfirmation.SetActive(false));
            SetRect(no.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(220, 62), new Vector2(125, -60));
            deleteConfirmation.SetActive(false);
        }

        private RectTransform MakeGrid(Transform parent, Vector2 pos, Vector2 size, int columns, float cell, float spacing = 8)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(.5f,.5f), new Vector2(.5f,.5f), size, pos);
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.UpperCenter;
            return go.GetComponent<RectTransform>();
        }

        private GameObject MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = Panel(label, parent, new Color(.1f, .25f, .36f, 1));
            DarkFantasySkin.Apply(go.GetComponent<Image>(), DarkFantasySkin.Button);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(action);
            go.AddComponent<UIHoverFeedback>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.1f, .94f, 1f);
            colors.pressedColor = new Color(.7f, .59f, .43f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            button.colors = colors;
            var labelText = AddText(go.transform, label, 17, TextAnchor.MiddleCenter);
            labelText.font = boldFont;
            labelText.fontStyle = FontStyle.Normal;
            return go;
        }

        private GameObject Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text AddText(Transform parent, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var text = go.GetComponent<Text>();
            text.font = size >= 24 ? headingFont : font;
            text.text = value;
            text.fontSize = size;
            text.color = DarkFantasySkin.Text;
            text.alignment = anchor;
            text.fontStyle = size >= 24 ? FontStyle.Bold : FontStyle.Normal;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            text.raycastTarget = false;
            return text;
        }

        private Text AddTextAt(Transform parent, string value, int size, Vector2 pos, Vector2 dimensions, TextAnchor anchor)
        {
            var text = AddText(parent, value, size, anchor);
            SetRect(text.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), dimensions, pos);
            return text;
        }

        private Text AddTextAt(Transform parent, string value, int size, Vector2 pos, Vector2 dimensions,
            TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = AddText(parent, value, size, anchor);
            SetRect(text.rectTransform, anchorMin, anchorMax, dimensions, pos);
            return text;
        }

        private static void Clear(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        private static string Glyph(ItemKind kind) => kind switch
        {
            ItemKind.Weapon => "⚔", ItemKind.Shield => "◈", ItemKind.Armor => "◆",
            ItemKind.Head => "⌂", ItemKind.Gloves => "✋", ItemKind.Belt => "═",
            ItemKind.Boots => "∩", ItemKind.Accessory => "✦", ItemKind.Scroll => "▧",
            ItemKind.Potion => "♥", ItemKind.Gold => "●", _ => "?"
        };

        private static string RarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Rare => "Редкий", ItemRarity.Epic => "Эпический",
            ItemRarity.Legendary => "Легендарный", _ => "Обычный"
        };

        private static string KindName(ItemKind kind) => kind switch
        {
            ItemKind.Weapon => "Оружие", ItemKind.Shield => "Щит", ItemKind.Armor => "Броня",
            ItemKind.Head => "Головной убор", ItemKind.Gloves => "Перчатки", ItemKind.Belt => "Пояс",
            ItemKind.Boots => "Ботинки", ItemKind.Accessory => "Украшение", ItemKind.Scroll => "Свиток",
            ItemKind.Potion => "Зелье", ItemKind.Gold => "Золото", _ => kind.ToString()
        };

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 size, Vector2 pos)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        private static void SetArea(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    public sealed class InventorySlotInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static InventorySlotInteraction activeDrag;
        private InventoryDragArea area;
        private int index;
        private ItemInstance item;
        private Action click;
        private Action doubleClick;
        private Action<Vector2> contextClick;
        private Action<InventoryDragArea, int, InventoryDragArea, int> drop;
        private CanvasGroup canvasGroup;
        private GameObject dragGhost;
        private Outline outline;
        private Color defaultOutline;
        private bool dragging;

        public void Configure(InventoryDragArea slotArea, int slotIndex, ItemInstance slotItem,
            Action onClick, Action onDoubleClick, Action<Vector2> onContextClick,
            Action<InventoryDragArea, int, InventoryDragArea, int> onDrop)
        {
            area = slotArea;
            index = slotIndex;
            item = slotItem;
            click = onClick;
            doubleClick = onDoubleClick;
            contextClick = onContextClick;
            drop = onDrop;
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            outline = GetComponent<Outline>();
            if (outline != null) defaultOutline = outline.effectColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragging) return;
            if (eventData.button == PointerEventData.InputButton.Right) { contextClick?.Invoke(eventData.position); return; }
            if (eventData.clickCount >= 2) doubleClick?.Invoke();
            else click?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (item == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            dragging = true;
            activeDrag = this;
            canvasGroup.alpha = .45f;
            canvasGroup.blocksRaycasts = false;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            dragGhost = new GameObject("Dragged Item", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            dragGhost.transform.SetParent(canvas.transform, false);
            var image = dragGhost.GetComponent<Image>();
            image.sprite = RuntimeItemIcons.Get(item);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var group = dragGhost.GetComponent<CanvasGroup>();
            group.alpha = .92f;
            group.blocksRaycasts = false;
            var rect = dragGhost.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72, 72);
            rect.position = eventData.position;
            dragGhost.transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragGhost != null) dragGhost.GetComponent<RectTransform>().position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
            if (dragGhost != null) Destroy(dragGhost);
            dragGhost = null;
            activeDrag = null;
            // Suppress the click Unity can emit immediately after ending a drag.
            StartCoroutine(ResetDragging());
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (activeDrag == null || activeDrag == this) return;
            drop?.Invoke(activeDrag.area, activeDrag.index, area, index);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (activeDrag == null || outline == null) return;
            outline.effectColor = new Color(.95f, .62f, .2f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (outline == null) return;
            outline.effectColor = defaultOutline;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private System.Collections.IEnumerator ResetDragging()
        {
            yield return null;
            dragging = false;
        }
    }
}
