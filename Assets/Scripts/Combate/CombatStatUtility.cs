using UnityEngine;

/// Utilidad de stats efectivos para la prioridad de turno.
public static class CombatStatUtility
{
    public static int GetEffectiveSpeed(PokemonInstance p)
    {
        if (p == null) return 1;
        float stageMul = StatStageService.GetMultiplier(p, BattleStat.Speed);

        float statusMul = 1f;
        if (PokemonStatusService.GetStatus(p) == PrimaryStatus.Paralysis)
            statusMul = 0.5f; // penalización simple

        int raw = Mathf.Max(1, p.stats.Speed);
        return Mathf.Max(1, Mathf.FloorToInt(raw * stageMul * statusMul));
    }
}
