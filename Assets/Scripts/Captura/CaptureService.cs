using UnityEngine;

/// Lógica de captura (MVP). Ofrece wrappers compatibles con Ball.cs:
/// - ComputeCaptureChance(target, ball)
/// - RollCapture(target, ball, criticalChance)
/// - ComputeShakeCount(success, chance)
public static class CaptureService
{
    /// Núcleo: calcula éxito, chance usada y nº de sacudidas.
    public static bool TryCatch(
        PokemonInstance target,
        PokeballData ball,
        PrimaryStatus status,
        out float chance,
        out int shakes)
    {
        chance = 0f;
        shakes = 1;

        if (target == null || target.species == null || target.stats.MaxHP <= 0)
            return false;

        float catchRate = Mathf.Clamp01(target.species.catchRate);               // [0..1]
        float ballMul = ball ? Mathf.Max(0f, ball.catchMultiplier) : 1f;

        float statusMul = 1f;
        switch (status)
        {
            case PrimaryStatus.Sleep:
            case PrimaryStatus.Freeze: statusMul = 2.0f; break;
            case PrimaryStatus.Paralysis:
            case PrimaryStatus.Burn:
            case PrimaryStatus.Poison: statusMul = 1.5f; break;
        }

        float hpFrac = Mathf.Clamp01((float)target.currentHP / Mathf.Max(1, target.stats.MaxHP));
        float hpMul = Mathf.Lerp(2.0f, 0.5f, hpFrac);

        chance = Mathf.Clamp01(catchRate * ballMul * statusMul * hpMul);

        const float defaultCritical = 0.10f;
        bool success = (Random.value < defaultCritical) || (Random.value <= chance);

        shakes = success ? 3 : Random.Range(1, 3);

        // Evento con shakes reales
        try { GameEventBus.RaiseCaptureShakes(shakes, chance, success); } catch { }

        return success;
    }

    // ---------------- Wrappers compatibles con Ball.cs ----------------

    public static float ComputeCaptureChance(PokemonInstance target, PokeballData ball)
    {
        var status = PokemonStatusService.GetStatus(target);
        ComputeChanceInternal(target, ball, status, out float chance);
        return chance;
    }

    /// Hace la tirada con prob. crítica externa y emite el evento con shakes reales.
    public static bool RollCapture(PokemonInstance target, PokeballData ball, float criticalChance)
    {
        var status = PokemonStatusService.GetStatus(target);
        float chance = ComputeChanceInternal(target, ball, status, out _);
        bool success = (Random.value < Mathf.Clamp01(criticalChance)) || (Random.value <= chance);

        int shakes = success ? 3 : ComputeShakeCount(false, chance);
        try { GameEventBus.RaiseCaptureShakes(shakes, chance, success); } catch { }

        return success;
    }

    public static int ComputeShakeCount(bool success, float chance)
    {
        if (success) return 3;
        float pTwo = Mathf.Clamp01(chance * 1.5f);
        return (Random.value < pTwo) ? 2 : 1;
    }

    // ---------------- Internos ----------------

    private static float ComputeChanceInternal(PokemonInstance target, PokeballData ball, PrimaryStatus status, out float chance)
    {
        chance = 0f;
        if (target == null || target.species == null || target.stats.MaxHP <= 0) return 0f;

        float catchRate = Mathf.Clamp01(target.species.catchRate);
        float ballMul = ball ? Mathf.Max(0f, ball.catchMultiplier) : 1f;

        float statusMul = 1f;
        switch (status)
        {
            case PrimaryStatus.Sleep:
            case PrimaryStatus.Freeze: statusMul = 2.0f; break;
            case PrimaryStatus.Paralysis:
            case PrimaryStatus.Burn:
            case PrimaryStatus.Poison: statusMul = 1.5f; break;
        }

        float hpFrac = Mathf.Clamp01((float)target.currentHP / Mathf.Max(1, target.stats.MaxHP));
        float hpMul = Mathf.Lerp(2.0f, 0.5f, hpFrac);

        chance = Mathf.Clamp01(catchRate * ballMul * statusMul * hpMul);
        return chance;
    }
}
