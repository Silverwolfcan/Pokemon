using UnityEngine;

/// Regla de orden de acción por turno:
/// 1) Prioridad del movimiento (mayor primero).
/// 2) Si igual: Velocidad efectiva (incluye modificadores ya aplicados en CombatStatUtility).
/// 3) Si igual: coin flip.
public static class TurnPriorityResolver
{
    public static bool PlayerActsFirst(
        PokemonInstance player, int playerMovePriority,
        PokemonInstance enemy, int enemyMovePriority)
    {
        // 1) Prioridad del movimiento
        if (playerMovePriority != enemyMovePriority)
            return playerMovePriority > enemyMovePriority;

        // 2) Velocidad efectiva
        int spP = CombatStatUtility.GetEffectiveSpeed(player);
        int spE = CombatStatUtility.GetEffectiveSpeed(enemy);
        if (spP != spE) return spP > spE;

        // 3) Coin flip
        return Random.value >= 0.5f;
    }
}
