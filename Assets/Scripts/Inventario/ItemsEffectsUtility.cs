using UnityEngine;

public enum ItemUseResult { Applied, NotApplicable, InvalidTarget }

/// Lógica de aplicación para HealingItemData.
/// No depende de UI. Úsala desde BagUseController u otros menús.
public static class ItemEffectsUtility
{
    /// Aplica un HealingItem sobre un Pokémon. Para Éter/Elixir usar moveIndex.
    public static ItemUseResult ApplyHealingItem(PokemonInstance target, HealingItemData data, int moveIndex = -1)
    {
        if (target == null || data == null) return ItemUseResult.InvalidTarget;

        bool fainted = target.currentHP <= 0;
        bool isRevive = data.IsRevive();
        if (fainted && !isRevive) return ItemUseResult.InvalidTarget;
        if (!fainted && isRevive) return ItemUseResult.NotApplicable;

        switch (data.effect)
        {
            // --------- HP ---------
            case HealingEffectType.HealFixed:
                return Heal(target, data.amount);

            case HealingEffectType.HealPercent:
                return HealPercent(target, data.percent);

            case HealingEffectType.HealToFull:
                return HealToFull(target, data.curePrimaryStatus);

            // --------- Revivir ---------
            case HealingEffectType.ReviveToHalf:
                return Revive(target, 0.5f);

            case HealingEffectType.ReviveToFull:
                return Revive(target, 1f);

            // --------- Estados ---------
            case HealingEffectType.CurePoison:
                return CureSpecific(target, PrimaryStatus.Poison);

            case HealingEffectType.CureParalysis:
                return CureSpecific(target, PrimaryStatus.Paralysis);

            case HealingEffectType.CureSleep:
                return CureSpecific(target, PrimaryStatus.Sleep);

            case HealingEffectType.CureBurn:
                return CureSpecific(target, PrimaryStatus.Burn);

            case HealingEffectType.CureFreeze:
                return CureSpecific(target, PrimaryStatus.Freeze);

            case HealingEffectType.CureAllStatus:
                return CureAny(target);

            // --------- PP (un movimiento) ---------
            case HealingEffectType.RestorePPSingleFixed:
                return RestorePPSingle(target, data.ppAmount, moveIndex);

            case HealingEffectType.RestorePPSingleFull:
                return RestorePPSingle(target, int.MaxValue, moveIndex);

            // --------- PP (todos) ---------
            case HealingEffectType.RestorePPAllFixed:
                return RestorePPAll(target, data.ppAmount);

            case HealingEffectType.RestorePPAllFull:
                return RestorePPAll(target, int.MaxValue);

            default:
                return ItemUseResult.InvalidTarget;
        }
    }

    // ---------------- internals ----------------
    private static ItemUseResult Heal(PokemonInstance p, int amount)
    {
        if (p.currentHP <= 0) return ItemUseResult.InvalidTarget;
        int before = p.currentHP;
        int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, p.stats.MaxHP);
        if (after == before) return ItemUseResult.NotApplicable;
        p.currentHP = after;
        GameEventBus.RaisePokemonChanged(p);
        return ItemUseResult.Applied;
    }

    private static ItemUseResult HealPercent(PokemonInstance p, int percent)
    {
        if (p.currentHP <= 0) return ItemUseResult.InvalidTarget;
        int max = Mathf.Max(1, p.stats.MaxHP);
        int add = Mathf.Max(1, Mathf.FloorToInt(max * Mathf.Clamp01(percent / 100f)));
        return Heal(p, add);
    }

    private static ItemUseResult HealToFull(PokemonInstance p, bool cureStatus)
    {
        if (p.currentHP <= 0) return ItemUseResult.InvalidTarget;

        bool hpFull = p.currentHP >= p.stats.MaxHP;
        bool noStatus = PokemonStatusService.GetStatus(p) == PrimaryStatus.None;

        if (hpFull && (!cureStatus || noStatus))
            return ItemUseResult.NotApplicable;

        p.currentHP = p.stats.MaxHP;
        GameEventBus.RaisePokemonChanged(p);
        if (cureStatus) PokemonStatusService.Cure(p);
        return ItemUseResult.Applied;
    }

    private static ItemUseResult Revive(PokemonInstance p, float frac)
    {
        if (p.currentHP > 0) return ItemUseResult.NotApplicable;
        int max = Mathf.Max(1, p.stats.MaxHP);
        p.currentHP = Mathf.Max(1, Mathf.FloorToInt(max * Mathf.Clamp01(frac)));
        PokemonStatusService.Cure(p);
        GameEventBus.RaisePokemonChanged(p);
        return ItemUseResult.Applied;
    }

    private static ItemUseResult CureSpecific(PokemonInstance p, PrimaryStatus s)
    {
        if (PokemonStatusService.GetStatus(p) != s) return ItemUseResult.NotApplicable;
        return PokemonStatusService.Cure(p) ? ItemUseResult.Applied : ItemUseResult.NotApplicable;
    }

    private static ItemUseResult CureAny(PokemonInstance p)
    {
        if (PokemonStatusService.GetStatus(p) == PrimaryStatus.None) return ItemUseResult.NotApplicable;
        return PokemonStatusService.Cure(p) ? ItemUseResult.Applied : ItemUseResult.NotApplicable;
    }

    private static ItemUseResult RestorePPSingle(PokemonInstance p, int amount, int moveIndex)
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
        GameEventBus.RaisePokemonChanged(p);
        return ItemUseResult.Applied;
    }

    private static ItemUseResult RestorePPAll(PokemonInstance p, int amountEach)
    {
        if (p.Moves == null || p.Moves.Count == 0) return ItemUseResult.NotApplicable;

        int applied = 0;
        foreach (var mv in p.Moves)
        {
            if (mv == null || mv.data == null) continue;
            int before = mv.currentPP;
            int max = mv.maxPP;
            int add = amountEach >= int.MaxValue ? (max - before) : Mathf.Min(amountEach, max - before);
            if (add > 0)
            {
                mv.currentPP += add;
                applied++;
            }
        }
        if (applied > 0) GameEventBus.RaisePokemonChanged(p);
        return applied > 0 ? ItemUseResult.Applied : ItemUseResult.NotApplicable;
    }
}
