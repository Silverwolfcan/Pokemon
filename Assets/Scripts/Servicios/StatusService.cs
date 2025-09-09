// Servicios/StatusService.cs
using UnityEngine;

/// Estados primarios + confusión: utilidades y configuración mínima.
public static class StatusService
{
    // --- Tipos públicos (compartidos) ---
    public enum PrimaryStatus { None = 0, Burn = 1, Poison = 2, Sleep = 3, Paralysis = 4, Freeze = 5 }
    public enum StatusBlockReason { None = 0, Sleep = 1, Freeze = 2, Paralysis = 3 }
    public enum StatTarget { PhysicalAttack, Speed }

    // --- Parámetros por defecto (estables) ---
    private const float BURN_DOT = 1f / 16f;     // fracción de MaxHP por turno
    private const float POISON_DOT = 1f / 8f;    // fracción de MaxHP por turno
    private const float PARALYSIS_BLOCK = 0.25f; // 25% de no actuar
    private const float PARALYSIS_SPEED_MUL = 0.5f;
    private const float BURN_ATK_MUL = 0.5f;
    private static readonly Vector2Int SLEEP_TURNS = new Vector2Int(1, 3);
    private const float THAW_PER_TURN = 0.20f;
    private static readonly Vector2Int CONFUSION_TURNS = new Vector2Int(1, 4);
    private const int CONFUSION_SELF_POWER = 40;
    private const int VAR_MIN = 85, VAR_MAX = 100;

    // --- API de azar/coeficientes ---
    public static int RollSleepTurns() => Random.Range(SLEEP_TURNS.x, SLEEP_TURNS.y + 1);
    public static int RollConfusionTurns() => Random.Range(CONFUSION_TURNS.x, CONFUSION_TURNS.y + 1);
    public static bool RollParalysisBlock() => Random.value < PARALYSIS_BLOCK;
    public static bool RollThawThisTurn() => Random.value < THAW_PER_TURN;
    public static int RollVariancePercent() => Random.Range(Mathf.Min(VAR_MIN, VAR_MAX), Mathf.Max(VAR_MIN, VAR_MAX) + 1);

    // --- DOT y deltas persistentes ---
    public static float GetDotFraction(PrimaryStatus status)
    {
        switch (status)
        {
            case PrimaryStatus.Burn: return BURN_DOT;
            case PrimaryStatus.Poison: return POISON_DOT;
            default: return 0f;
        }
    }

    // Multiplicadores "virtuales" por estado para integrarse en cálculos sin tocar StageService.
    public static float GetAttackMulByStatus(PrimaryStatus status) => status == PrimaryStatus.Burn ? BURN_ATK_MUL : 1f;
    public static float GetSpeedMulByStatus(PrimaryStatus status) => status == PrimaryStatus.Paralysis ? PARALYSIS_SPEED_MUL : 1f;

    // Alternativa por etapas si prefieres sumar un delta de etapa "virtual" (-2 ≈ x0.5).
    public static int GetPersistentStageDelta(PrimaryStatus status, StatTarget target)
    {
        if (status == PrimaryStatus.Burn && target == StatTarget.PhysicalAttack) return -2;
        if (status == PrimaryStatus.Paralysis && target == StatTarget.Speed) return -2;
        return 0;
    }

    // --- Confusión ---
    public static int GetConfusionSelfPower() => CONFUSION_SELF_POWER;
}

/*
Asignaciones en el Inspector:
- Ninguna. Es un servicio estático.
- Si deseas parámetros editables más adelante, migra a un ScriptableObject, pero no es necesario para integrar.
*/
