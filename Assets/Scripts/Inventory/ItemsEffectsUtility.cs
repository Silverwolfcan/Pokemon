using UnityEngine;
using System.Reflection;

public enum ItemUseResult { Applied, NotApplicable, InvalidTarget }

/// Lógica de aplicación genérica para HealingItemData. Reutilizable dentro y fuera de combate.
public static class ItemEffectsUtility
{
    public static ItemUseResult ApplyHealingItem(PokemonInstance target, HealingItemData data, int moveIndex = -1)
    {
        if (target == null || data == null) return ItemUseResult.InvalidTarget;

        switch (data.effect)
        {
            case HealingEffectType.HealFixed:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                return Heal(target, data.amount);

            case HealingEffectType.HealPercent:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                int delta = Mathf.CeilToInt(target.stats.MaxHP * (data.percent / 100f));
                return Heal(target, delta);

            case HealingEffectType.HealToFull:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                var resHF = HealToFull(target);
                if (resHF == ItemUseResult.Applied && data.curePrimaryStatus) TryCureAllStatuses(target);
                return resHF;

            case HealingEffectType.ReviveToHalf:
                if (target.currentHP > 0) return ItemUseResult.NotApplicable;
                target.currentHP = Mathf.Max(1, Mathf.CeilToInt(target.stats.MaxHP * 0.5f));
                TryCureAllStatuses(target);
                return ItemUseResult.Applied;

            case HealingEffectType.ReviveToFull:
                if (target.currentHP > 0) return ItemUseResult.NotApplicable;
                target.currentHP = target.stats.MaxHP;
                TryCureAllStatuses(target);
                return ItemUseResult.Applied;

            case HealingEffectType.CurePoison: return TryCureSpecific(target, StatusService.PrimaryStatus.Poison);
            case HealingEffectType.CureParalysis: return TryCureSpecific(target, StatusService.PrimaryStatus.Paralysis);
            case HealingEffectType.CureSleep: return TryCureSpecific(target, StatusService.PrimaryStatus.Sleep);
            case HealingEffectType.CureBurn: return TryCureSpecific(target, StatusService.PrimaryStatus.Burn);
            case HealingEffectType.CureFreeze: return TryCureSpecific(target, StatusService.PrimaryStatus.Freeze);
            case HealingEffectType.CureAllStatus: return TryCureAllStatuses(target) ? ItemUseResult.Applied : ItemUseResult.NotApplicable;

            case HealingEffectType.RestorePPSingleFixed: return RestorePPSingle(target, data.ppAmount, moveIndex);
            case HealingEffectType.RestorePPSingleFull: return RestorePPSingle(target, int.MaxValue, moveIndex);
            case HealingEffectType.RestorePPAllFixed: return RestorePPAll(target, data.ppAmount);
            case HealingEffectType.RestorePPAllFull: return RestorePPAll(target, int.MaxValue);
        }
        return ItemUseResult.InvalidTarget;
    }

    static ItemUseResult Heal(PokemonInstance p, int amount)
    {
        int before = p.currentHP;
        int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, p.stats.MaxHP);
        if (after == before) return ItemUseResult.NotApplicable;
        p.currentHP = after;
        return ItemUseResult.Applied;
    }

    static ItemUseResult HealToFull(PokemonInstance p)
    {
        if (p.currentHP >= p.stats.MaxHP) return ItemUseResult.NotApplicable;
        p.currentHP = p.stats.MaxHP;
        return ItemUseResult.Applied;
    }

    static ItemUseResult RestorePPSingle(PokemonInstance p, int amount, int moveIndex)
    {
        if (p.Moves == null || p.Moves.Count == 0) return ItemUseResult.NotApplicable;
        if (moveIndex < 0 || moveIndex >= p.Moves.Count) return ItemUseResult.InvalidTarget;

        var mv = p.Moves[moveIndex];
        if (mv == null || mv.data == null) return ItemUseResult.InvalidTarget;

        int before = mv.currentPP;
        int max = mv.maxPP;
        int add = amount >= int.MaxValue ? (max - before) : Mathf.Min(amount, max - before);
        if (add <= 0) return ItemUseResult.NotApplicable;

        mv.currentPP += add;
        return ItemUseResult.Applied;
    }

    static ItemUseResult RestorePPAll(PokemonInstance p, int amountEach)
    {
        if (p.Moves == null || p.Moves.Count == 0) return ItemUseResult.NotApplicable;
        int applied = 0;
        for (int i = 0; i < p.Moves.Count; i++)
        {
            var mv = p.Moves[i];
            if (mv == null || mv.data == null) continue;

            int before = mv.currentPP;
            int max = mv.maxPP;
            int add = amountEach >= int.MaxValue ? (max - before) : Mathf.Min(amountEach, max - before);
            if (add > 0) { mv.currentPP += add; applied++; }
        }
        return applied > 0 ? ItemUseResult.Applied : ItemUseResult.NotApplicable;
    }

    // --- Estados ---
    static bool TryCureAllStatuses(PokemonInstance target)
    {
        var sc = FindContainerFor(target);
        if (sc == null) return false;

        bool changed = false;

        var clear = sc.GetType().GetMethod("ClearPrimary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (clear != null)
        {
            var before = GetPrimary(sc);
            clear.Invoke(sc, null);
            var after = GetPrimary(sc);
            changed |= before != after;
        }
        else changed |= TryApplyPrimary(sc, StatusService.PrimaryStatus.None);

        var clearConf = sc.GetType().GetMethod("ClearConfusion", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (clearConf != null) { clearConf.Invoke(sc, null); changed = true; }

        return changed;
    }

    static ItemUseResult TryCureSpecific(PokemonInstance target, StatusService.PrimaryStatus s)
    {
        var sc = FindContainerFor(target);
        if (sc == null) return ItemUseResult.NotApplicable;
        if (GetPrimary(sc) != s) return ItemUseResult.NotApplicable;

        if (TryApplyPrimary(sc, StatusService.PrimaryStatus.None)) return ItemUseResult.Applied;

        var clear = sc.GetType().GetMethod("ClearPrimary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (clear != null) { clear.Invoke(sc, null); return ItemUseResult.Applied; }
        return ItemUseResult.NotApplicable;
    }

    static StatusService.PrimaryStatus GetPrimary(object sc)
    {
        var pi = sc.GetType().GetProperty("Primary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pi != null) { try { return (StatusService.PrimaryStatus)pi.GetValue(sc); } catch { } }
        return StatusService.PrimaryStatus.None;
    }

    static bool TryApplyPrimary(object sc, StatusService.PrimaryStatus s)
    {
        var mi = sc.GetType().GetMethod("TryApplyPrimary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null) { try { return (bool)mi.Invoke(sc, new object[] { s }); } catch { } }
        return false;
    }

    static object FindContainerFor(PokemonInstance model)
    {
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i]; if (!mb) continue;
            var t = mb.GetType(); if (t.Name != "StatusContainer") continue;

            var p = t.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
            PokemonInstance m = null;
            if (p != null) m = p.GetValue(mb) as PokemonInstance;
            if (m == null)
            {
                var f = t.GetField("_pokemon", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) m = f.GetValue(mb) as PokemonInstance;
            }
            if (ReferenceEquals(m, model)) return mb;
        }
        return null;
    }
}
