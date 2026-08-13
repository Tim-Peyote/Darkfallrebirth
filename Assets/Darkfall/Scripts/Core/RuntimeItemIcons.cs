using System.Collections.Generic;
using Darkfall.Gameplay;
using UnityEngine;

namespace Darkfall.Core
{
    public static class RuntimeItemIcons
    {
        private const int Size = 64;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(ItemInstance item)
        {
            if (item == null) return null;
            var key = item.baseId + ":" + item.rarity;
            if (Cache.TryGetValue(key, out var sprite)) return sprite;
            sprite = ItemSpriteAtlas.Get(item.baseId);
            if (sprite != null)
            {
                Cache[key] = sprite;
                return sprite;
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Icon " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[Size * Size];
            var primary = ItemColor(item.baseId);
            switch (item.kind)
            {
                case ItemKind.Weapon: DrawWeapon(pixels, item.baseId, primary); break;
                case ItemKind.Shield: DrawShield(pixels, primary); break;
                case ItemKind.Armor:
                case ItemKind.Head:
                case ItemKind.Gloves:
                case ItemKind.Belt:
                case ItemKind.Boots: DrawArmor(pixels, item.kind, primary); break;
                case ItemKind.Accessory:
                case ItemKind.Amulet:
                case ItemKind.Ring: DrawAccessory(pixels, item.baseId, primary); break;
                case ItemKind.Focus: DrawScroll(pixels, item.baseId, primary); break;
                case ItemKind.Potion: DrawPotion(pixels, primary); break;
                case ItemKind.Scroll: DrawScroll(pixels, item.baseId, primary); break;
                case ItemKind.Gold: DrawPouch(pixels); break;
            }
            OutlineGlow(pixels, item.Color);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size, 0, SpriteMeshType.Tight);
            Cache[key] = sprite;
            return sprite;
        }

        private static void DrawWeapon(Color32[] p, string id, Color32 color)
        {
            var wood = C("#704429");
            if (id == "axe")
            {
                Line(p, 20, 10, 43, 52, wood, 5);
                FillRect(p, 30, 36, 50, 51, C("#C5CBD2"));
                Line(p, 30, 37, 50, 49, C("#F2F4F5"), 2);
            }
            else if (id == "staff" || id == "wand")
            {
                Line(p, 17, 8, 43, 54, wood, id == "staff" ? 5 : 3);
                Circle(p, 17, 11, id == "staff" ? 8 : 6, color);
                Circle(p, 15, 13, 3, C("#FFFFFF"));
            }
            else if (id == "crossbow")
            {
                Line(p, 13, 22, 51, 42, wood, 5);
                Line(p, 13, 42, 51, 22, wood, 4);
                Line(p, 9, 32, 55, 32, C("#D4D8DD"), 2);
                Line(p, 31, 15, 31, 52, C("#8A5B37"), 4);
            }
            else
            {
                var shortBlade = id == "dagger";
                Line(p, shortBlade ? 22 : 12, shortBlade ? 15 : 8, 47, 49, C("#E9EDF0"), shortBlade ? 5 : 7);
                Line(p, shortBlade ? 23 : 13, shortBlade ? 16 : 9, 44, 45, color, 2);
                Line(p, 38, 43, 51, 31, C("#B68A48"), 5);
                Line(p, 42, 49, 53, 38, wood, 6);
            }
        }

        private static void DrawShield(Color32[] p, Color32 color)
        {
            FillRect(p, 14, 12, 50, 39, C("#B6BEC6"));
            for (var y = 18; y < 53; y++)
            {
                var half = Mathf.Max(2, 19 - (y - 18) / 2);
                FillRect(p, 32 - half, y, 32 + half, y, color);
            }
            Line(p, 32, 17, 32, 48, C("#D7B45B"), 3);
            Line(p, 18, 29, 46, 29, C("#D7B45B"), 3);
        }

        private static void DrawArmor(Color32[] p, ItemKind kind, Color32 color)
        {
            if (kind == ItemKind.Head)
            {
                Circle(p, 32, 31, 18, color);
                FillRect(p, 13, 31, 51, 48, color);
                FillRect(p, 22, 31, 42, 47, C("#10131B"));
                Line(p, 14, 48, 50, 48, C("#C0C6CC"), 3);
            }
            else if (kind == ItemKind.Gloves)
            {
                FillRect(p, 19, 25, 33, 51, color);
                for (var i = 0; i < 4; i++) FillRect(p, 17 + i * 5, 13 + i % 2 * 2, 20 + i * 5, 30, color);
                Line(p, 19, 40, 34, 40, C("#D1B06B"), 2);
            }
            else if (kind == ItemKind.Belt)
            {
                FillRect(p, 8, 25, 56, 39, color);
                FillRect(p, 26, 22, 40, 42, C("#D8B24E"));
                FillRect(p, 30, 26, 36, 38, C("#171921"));
            }
            else if (kind == ItemKind.Boots)
            {
                FillRect(p, 17, 11, 34, 43, color);
                FillRect(p, 17, 40, 48, 53, color);
                Line(p, 20, 22, 34, 22, C("#B38A55"), 2);
                Line(p, 20, 31, 34, 31, C("#B38A55"), 2);
            }
            else
            {
                for (var y = 13; y < 54; y++)
                FillRect(p, 13 + Mathf.Abs(32 - y) / 5, y, 51 - Mathf.Abs(32 - y) / 5, y, color);
                FillRect(p, 25, 10, 39, 19, C("#11141D"));
                Line(p, 32, 20, 32, 50, C("#C0C6CC"), 2);
            }
        }

        private static void DrawAccessory(Color32[] p, string id, Color32 color)
        {
            if (id == "ring")
            {
                Ring(p, 32, 34, 18, 7, C("#E8C65B"));
                Circle(p, 32, 15, 7, color);
                Circle(p, 30, 13, 2, C("#FFFFFF"));
            }
            else
            {
                Ring(p, 32, 25, 20, 2, C("#D9B754"));
                Circle(p, 32, 43, 11, C("#E8C65B"));
                Circle(p, 32, 43, 6, color);
            }
        }

        private static void DrawPotion(Color32[] p, Color32 liquid)
        {
            FillRect(p, 25, 8, 39, 19, C("#B88A55"));
            FillRect(p, 27, 17, 37, 26, C("#D9E2E8"));
            Circle(p, 32, 39, 17, C("#C9D8E0"));
            Circle(p, 32, 42, 13, liquid);
            FillRect(p, 20, 29, 44, 40, liquid);
            Circle(p, 27, 33, 4, C("#FFFFFF"));
        }

        private static void DrawScroll(Color32[] p, string id, Color32 rune)
        {
            FillRect(p, 14, 13, 50, 51, C("#D8BC79"));
            Circle(p, 16, 15, 6, C("#F0D693"));
            Circle(p, 48, 49, 6, C("#9C7543"));
            Line(p, 23, 23, 42, 23, C("#795735"), 2);
            var hash = Mathf.Abs(id.GetHashCode());
            if (hash % 3 == 0)
            {
                Line(p, 32, 27, 22, 43, rune, 3);
                Line(p, 32, 27, 42, 43, rune, 3);
                Line(p, 25, 38, 39, 38, rune, 3);
            }
            else if (hash % 3 == 1)
            {
                Ring(p, 32, 36, 10, 3, rune);
                Line(p, 32, 25, 32, 47, rune, 2);
            }
            else
            {
                Line(p, 22, 28, 42, 44, rune, 3);
                Line(p, 42, 28, 22, 44, rune, 3);
            }
        }

        private static void DrawPouch(Color32[] p)
        {
            Circle(p, 32, 38, 18, C("#9A572D"));
            FillRect(p, 22, 13, 42, 27, C("#70401F"));
            Line(p, 20, 25, 44, 25, C("#E3B54A"), 3);
            Circle(p, 32, 39, 7, C("#F0C34F"));
        }

        private static Color32 ItemColor(string id)
        {
            if (id.Contains("fire") || id.Contains("rage") || id.Contains("chaos")) return C("#E64B32");
            if (id.Contains("ice") || id.Contains("speed") || id.Contains("barrier")) return C("#3C91D8");
            if (id.Contains("lightning") || id.Contains("invulnerability") || id.Contains("purification")) return C("#F0CC3F");
            if (id.Contains("poison") || id.Contains("regen")) return C("#3FAE62");
            if (id.Contains("mystery") || id.Contains("curse") || id.Contains("vampir")) return C("#8B4CB8");
            if (id.Contains("stone") || id.Contains("defense") || id == "plate" || id == "helmet") return C("#8D969E");
            if (id == "potion") return C("#D9434D");
            if (id == "robe" || id == "hood" || id == "staff" || id == "wand") return C("#7442A4");
            if (id == "leather" || id == "belt" || id == "boots" || id == "scroll_werewolf") return C("#A65B32");
            return C("#527B9E");
        }

        private static void OutlineGlow(Color32[] p, Color glow)
        {
            var source = (Color32[])p.Clone();
            var edge = (Color32)new Color(glow.r, glow.g, glow.b, .65f);
            for (var y = 1; y < Size - 1; y++)
            for (var x = 1; x < Size - 1; x++)
            {
                var index = y * Size + x;
                if (source[index].a != 0) continue;
                for (var oy = -1; oy <= 1; oy++)
                for (var ox = -1; ox <= 1; ox++)
                    if (source[(y + oy) * Size + x + ox].a != 0) p[index] = edge;
            }
        }

        private static void FillRect(Color32[] p, int x0, int y0, int x1, int y1, Color32 color)
        {
            for (var y = Mathf.Max(0, y0); y <= Mathf.Min(Size - 1, y1); y++)
            for (var x = Mathf.Max(0, x0); x <= Mathf.Min(Size - 1, x1); x++)
                p[y * Size + x] = color;
        }

        private static void Circle(Color32[] p, int cx, int cy, int radius, Color32 color)
        {
            var square = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            for (var x = cx - radius; x <= cx + radius; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= square && x >= 0 && y >= 0 && x < Size && y < Size)
                    p[y * Size + x] = color;
        }

        private static void Ring(Color32[] p, int cx, int cy, int radius, int thickness, Color32 color)
        {
            var outer = radius * radius;
            var inner = (radius - thickness) * (radius - thickness);
            for (var y = cy - radius; y <= cy + radius; y++)
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var distance = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (distance <= outer && distance >= inner && x >= 0 && y >= 0 && x < Size && y < Size) p[y * Size + x] = color;
            }
        }

        private static void Line(Color32[] p, int x0, int y0, int x1, int y1, Color32 color, int width)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (var i = 0; i <= steps; i++)
            {
                var t = steps == 0 ? 0 : i / (float)steps;
                Circle(p, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), width / 2, color);
            }
        }

        private static Color32 C(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var value) ? value : Color.white;
        }
    }
}
