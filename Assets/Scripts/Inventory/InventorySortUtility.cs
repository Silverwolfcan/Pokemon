using System;
using System.Reflection;
using UnityEngine;

public static class InventorySortUtility
{
    // ---- Entrada: ItemData -> clave (grupo, calidad, nombre) ----
    // Grupo bajo = aparece antes en la lista de su categoría.
    public static (int group, int quality, string name) GetSortKey(ItemData item)
    {
        if (item == null) return (int.MaxValue, int.MaxValue, "");

        // --- Botiquín (Healing) ---
        if (item.category == ItemCategory.Healing)
        {
            // Compatibilidad: dos vías
            // (A) Nuevo HealingItemData con 'effect', 'amount', 'percent', 'ppAmount', 'curePrimaryStatus'...
            if (TryGetMember(item, "effect", out object effectObj))
            {
                // enumeración HealingEffectType si existe
                string eff = effectObj.ToString();

                // Subgrupos: 0-PS, 1-Revive, 2-PP, 3-Estados
                if (eff.Contains("HealFixed"))
                {
                    int amount = GetInt(item, "amount", GetInt(item, "restoreHP", 20));
                    int quality = Mathf.Clamp(amount, 0, 9999);
                    if (GetBool(item, "curePrimaryStatus", false)) quality += 5;
                    return (0, quality, item.itemName);
                }
                if (eff.Contains("HealPercent"))
                {
                    int percent = GetInt(item, "percent", 50);
                    int quality = 300 + Mathf.Clamp(percent, 1, 100);
                    if (GetBool(item, "curePrimaryStatus", false)) quality += 5;
                    return (0, quality, item.itemName);
                }
                if (eff.Contains("HealToFull"))
                {
                    int quality = 1000 + (GetBool(item, "curePrimaryStatus", false) ? 100 : 0);
                    return (0, quality, item.itemName);
                }
                if (eff.Contains("ReviveToHalf")) return (1, 50, item.itemName);
                if (eff.Contains("ReviveToFull")) return (1, 100, item.itemName);

                if (eff.Contains("RestorePPSingleFixed"))
                {
                    int pp = GetInt(item, "ppAmount", 10);
                    return (2, Mathf.Clamp(pp, 1, 9999), item.itemName);
                }
                if (eff.Contains("RestorePPSingleFull")) return (2, 1000, item.itemName);
                if (eff.Contains("RestorePPAllFixed")) return (2, 1100, item.itemName);
                if (eff.Contains("RestorePPAllFull")) return (2, 1200, item.itemName);

                if (eff.Contains("CureAllStatus")) return (3, 100, item.itemName);
                if (eff.Contains("CurePoison") || eff.Contains("CureParalysis") ||
                    eff.Contains("CureSleep") || eff.Contains("CureBurn") ||
                    eff.Contains("CureFreeze")) return (3, 10, item.itemName);
            }

            // (B) Fallback: SO antiguo con 'restoreHP' y 'revive'
            int restore = GetInt(item, "restoreHP", 0);
            bool revive = GetBool(item, "revive", false);
            if (revive) return (1, 50, item.itemName); // Revivir (simple)
            if (restore > 0) return (0, Mathf.Clamp(restore, 0, 9999), item.itemName); // Pociones por cantidad
            return (0, int.MaxValue - 1, item.itemName);
        }

        // --- Poké Balls ---
        if (item.category == ItemCategory.Pokeball)
        {
            if (IsMasterBall(item)) return (0, 10000, item.itemName);

            if (TryGetBallMultiplier(item, out float mult) && mult > 0f)
            {
                int q = (int)Mathf.Round(mult * 100f); // 1.0 -> 100, 1.5 -> 150, 2.0 -> 200...
                return (0, q, item.itemName);
            }

            int nameQ = GuessBallQualityByName(item.itemName);
            return (0, nameQ, item.itemName);
        }

        // --- Resto de categorías: orden alfabético dentro del grupo ---
        return (10, 0, item.itemName ?? "");
    }

    // Comparadores de conveniencia
    public static int CompareItems(ItemData a, ItemData b)
    {
        var ka = GetSortKey(a);
        var kb = GetSortKey(b);
        int g = ka.group.CompareTo(kb.group);
        if (g != 0) return g;
        int q = ka.quality.CompareTo(kb.quality);
        if (q != 0) return q;
        return string.Compare(ka.name, kb.name, StringComparison.Ordinal);
    }

    public static int CompareEntries(ItemEntry a, ItemEntry b)
    {
        if (a?.item == null && b?.item == null) return 0;
        if (a?.item == null) return 1;
        if (b?.item == null) return -1;
        return CompareItems(a.item, b.item);
    }

    // ---------------- Balls helpers ----------------
    private static bool IsMasterBall(ItemData item)
    {
        if (GetBool(item, "isMasterBall", false)) return true;
        var n = item.itemName?.ToLowerInvariant() ?? "";
        return n.Contains("master");
    }

    private static bool TryGetBallMultiplier(ItemData item, out float value)
    {
        string[] names =
        {
            "catchMultiplier", "captureMultiplier",
            "catchRateModifier", "captureRateModifier",
            "rate", "multiplier", "bonus"
        };
        return TryGetFloat(item, names, out value);
    }

    private static int GuessBallQualityByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 9999;
        var n = name.ToLowerInvariant();

        if (n.Contains("poke")) return 100;
        if (n.Contains("great") || n.Contains("super")) return 150;
        if (n.Contains("ultra")) return 200;
        if (n.Contains("premier") || n.Contains("honor")) return 120;

        if (n.Contains("quick") || n.Contains("veloz")) return 160;
        if (n.Contains("timer") || n.Contains("turno")) return 170;
        if (n.Contains("dusk") || n.Contains("ocaso")) return 180;
        if (n.Contains("repeat") || n.Contains("acopio")) return 170;
        if (n.Contains("net") || n.Contains("bicho")) return 150;
        if (n.Contains("dive") || n.Contains("buceo")) return 150;
        if (n.Contains("nest") || n.Contains("nido")) return 140;
        if (n.Contains("luxury") || n.Contains("lujo")) return 140;
        if (n.Contains("friend") || n.Contains("amigo")) return 140;
        if (n.Contains("heal") || n.Contains("sana")) return 140;
        if (n.Contains("level") || n.Contains("nivel")) return 160;
        if (n.Contains("lure") || n.Contains("cebo")) return 160;
        if (n.Contains("fast") || n.Contains("rapida") || n.Contains("rápida")) return 160;
        if (n.Contains("heavy") || n.Contains("peso") || n.Contains("pesada")) return 170;

        return 9000;
    }

    // ---------------- Reflexión utilitaria (tolerante) ----------------
    private static bool GetBool(object obj, string name, bool def)
    {
        if (TryGetMember(obj, name, out object v))
        {
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return def;
    }

    private static int GetInt(object obj, string name, int def)
    {
        if (TryGetMember(obj, name, out object v))
        {
            if (v is int i) return i;
            if (v is float f) return Mathf.RoundToInt(f);
            if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return def;
    }

    private static bool TryGetFloat(object obj, string[] names, out float value)
    {
        value = 0f;
        foreach (var n in names)
        {
            if (TryGetMember(obj, n, out object v))
            {
                if (v is float f) { value = f; return true; }
                if (v is double d) { value = (float)d; return true; }
                if (v is int i) { value = i; return true; }
                if (v is long l) { value = l; return true; }
                if (float.TryParse(v.ToString(), out var parsed)) { value = parsed; return true; }
            }
        }
        return false;
    }

    private static bool TryGetMember(object obj, string name, out object value)
    {
        value = null;
        if (obj == null) return false;
        var t = obj.GetType();
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var f = t.GetField(name, F);
        if (f != null) { value = f.GetValue(obj); return true; }

        var p = t.GetProperty(name, F);
        if (p != null) { value = p.GetValue(obj, null); return true; }

        // case-insensitive
        foreach (var fi in t.GetFields(F))
            if (string.Equals(fi.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = fi.GetValue(obj); return true; }
        foreach (var pi in t.GetProperties(F))
            if (string.Equals(pi.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = pi.GetValue(obj, null); return true; }

        return false;
    }
}
