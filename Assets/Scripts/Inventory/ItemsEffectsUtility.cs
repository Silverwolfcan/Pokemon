using UnityEngine;

public enum ItemUseResult { Applied, NotApplicable, InvalidTarget }

/// Lógica de aplicación genérica para HealingItemData.
/// No depende de UI; úsala desde tus menús dentro/fuera de combate.
public static class ItemEffectsUtility
{
    /// <summary>
    /// Aplica un HealingItem sobre un Pokémon. Para efectos que requieren un movimiento concreto (Éter), pasa moveIndex.
    /// Retorna Applied si algo cambió; NotApplicable si no hacía falta; InvalidTarget si los parámetros no cuadran.
    /// </summary>
    public static ItemUseResult ApplyHealingItem(PokemonInstance target, HealingItemData data, int moveIndex = -1)
    {
        if (target == null || data == null) return ItemUseResult.InvalidTarget;

        switch (data.effect)
        {
            // --- HP ---
            case HealingEffectType.HealFixed:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                return Heal(target, data.amount);

            case HealingEffectType.HealPercent:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                int delta = Mathf.CeilToInt(target.stats.MaxHP * (data.percent / 100f));
                return Heal(target, delta);

            case HealingEffectType.HealToFull:
                if (target.currentHP <= 0) return ItemUseResult.NotApplicable;
                return HealToFull(target, data.curePrimaryStatus);

            // --- Revivir ---
            case HealingEffectType.ReviveToHalf:
                if (target.currentHP > 0) return ItemUseResult.NotApplicable;
                target.currentHP = Mathf.Max(1, Mathf.CeilToInt(target.stats.MaxHP * 0.5f));
                // TODO: limpiar estado KO si lo modelas aparte
                return ItemUseResult.Applied;

            case HealingEffectType.ReviveToFull:
                if (target.currentHP > 0) return ItemUseResult.NotApplicable;
                target.currentHP = target.stats.MaxHP;
                // TODO: limpiar estado KO si lo modelas aparte
                return ItemUseResult.Applied;

            // --- Estados (pendiente de integrar con tu sistema de estados) ---
            case HealingEffectType.CurePoison:
            case HealingEffectType.CureParalysis:
            case HealingEffectType.CureSleep:
            case HealingEffectType.CureBurn:
            case HealingEffectType.CureFreeze:
            case HealingEffectType.CureAllStatus:
                {
                    // Aún no hay sistema de estados en PokemonInstance; dejamos el gancho y no consumimos por defecto.
                    Debug.LogWarning("[ItemEffectsUtility] CureStatus llamado pero no hay sistema de estados aún. Implementar cuando existan los estados.");
                    return ItemUseResult.NotApplicable;
                }

            // --- PP ---
            case HealingEffectType.RestorePPSingleFixed:
                return RestorePPSingle(target, data.ppAmount, moveIndex);

            case HealingEffectType.RestorePPSingleFull:
                return RestorePPSingle(target, int.MaxValue, moveIndex);

            case HealingEffectType.RestorePPAllFixed:
                return RestorePPAll(target, data.ppAmount);

            case HealingEffectType.RestorePPAllFull:
                return RestorePPAll(target, int.MaxValue);
        }

        return ItemUseResult.InvalidTarget;
    }

    private static ItemUseResult Heal(PokemonInstance p, int amount)
    {
        int before = p.currentHP;
        int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, p.stats.MaxHP);
        if (after == before) return ItemUseResult.NotApplicable;
        p.currentHP = after;
        return ItemUseResult.Applied;
    }

    private static ItemUseResult HealToFull(PokemonInstance p, bool cureStatus)
    {
        if (p.currentHP >= p.stats.MaxHP) return ItemUseResult.NotApplicable;
        p.currentHP = p.stats.MaxHP;
        // if (cureStatus) TODO: limpiar estados cuando existan
        return ItemUseResult.Applied;
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
        return ItemUseResult.Applied;
    }

    private static ItemUseResult RestorePPAll(PokemonInstance p, int amountEach)
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
            if (add > 0)
            {
                mv.currentPP += add;
                applied++;
            }
        }
        return applied > 0 ? ItemUseResult.Applied : ItemUseResult.NotApplicable;
    }
}
