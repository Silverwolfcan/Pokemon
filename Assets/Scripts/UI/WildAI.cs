using UnityEngine;
using System.Collections.Generic;

public static class WildAI
{
    /// <summary>
    /// Devuelve el índice del movimiento a usar.
    /// Preferencia: movimientos que hacen daño (power>0 y no Status), ponderados por power*accuracy y STAB.
    /// Si no hay movimientos de daño con PP>0, elige un movimiento de estado con PP>0 al azar.
    /// Fallback: 0 si no encuentra nada.
    /// </summary>
    public static int ChooseMoveIndex(PokemonInstance wild)
    {
        if (wild == null || wild.Moves == null || wild.Moves.Count == 0)
            return 0;

        var usableDamage = new List<(int idx, float score)>();
        var usableStatus = new List<int>();

        for (int i = 0; i < wild.Moves.Count; i++)
        {
            var mi = wild.Moves[i];
            if (mi == null || mi.data == null) continue;
            if (mi.currentPP <= 0) continue;

            bool isStatus = (mi.data.attackCategory == AttackType.Status) || mi.data.power <= 0;

            if (!isStatus)
            {
                // Heurística: power * (accuracy/100) * STAB
                float acc = Mathf.Clamp(mi.data.accuracy, 1, 100) / 100f;
                float stab = HasSTAB(wild, mi.data) ? 1.5f : 1f;
                float score = Mathf.Max(1f, mi.data.power) * acc * stab;

                usableDamage.Add((i, score));
            }
            else
            {
                usableStatus.Add(i);
            }
        }

        // 1) Si hay movimientos de daño, elige por ruleta ponderada
        if (usableDamage.Count > 0)
            return WeightedPick(usableDamage);

        // 2) Si solo hay status, elige uno aleatorio para no quedarse parado
        if (usableStatus.Count > 0)
            return usableStatus[Random.Range(0, usableStatus.Count)];

        // 3) Fallback
        return 0;
    }

    // ----------------- Helpers -----------------

    private static int WeightedPick(List<(int idx, float score)> items)
    {
        float total = 0f;
        for (int i = 0; i < items.Count; i++)
            total += Mathf.Max(0.001f, items[i].score);

        float roll = Random.value * total;
        float accum = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            accum += Mathf.Max(0.001f, items[i].score);
            if (roll <= accum) return items[i].idx;
        }
        return items[items.Count - 1].idx; // seguro
    }

    // Mismo mapeo que usa MoveExecutor (ElementType -> PokemonType por índice).
    private static bool HasSTAB(PokemonInstance attacker, MoveData move)
    {
        if (attacker?.species == null || move == null) return false;
        PokemonType moveAsType = (PokemonType)(int)move.type;
        return attacker.species.primaryType == moveAsType || attacker.species.secondaryType == moveAsType;
    }
}
