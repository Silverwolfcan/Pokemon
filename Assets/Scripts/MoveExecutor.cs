using System;
using UnityEngine;

public static class MoveExecutor
{
    // ---------- Accuracy ----------
    // accuracy: 0..100
    public static bool CheckAccuracy(PokemonInstance atk, PokemonInstance def, int accuracy)
    {
        accuracy = Mathf.Clamp(accuracy, 1, 100);
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll <= accuracy;
    }

    // ---------- Daño ----------
    public static int ComputeDamage(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        if (atk == null || def == null || move == null || move.power <= 0)
            return 0;

        // Si es de estado, no hace daño directo (lo trataremos con efectos aparte)
        if (move.attackCategory == AttackType.Status)
            return 0;

        int level = Mathf.Max(1, atk.level);

        bool isPhysical = (move.attackCategory == AttackType.Physical);

        // OJO: ajusta estos nombres a tu struct/prop de stats reales
        int A = isPhysical ? atk.stats.Attack : atk.stats.SpAttack;
        int D = isPhysical ? def.stats.Defense : def.stats.SpDefense;

        // Fórmula base aproximada a la clásica: (((2L/5+2)*P*A/D)/50)+2
        float baseDmg = (((2f * level / 5f) + 2f) * move.power * A / Mathf.Max(1, D)) / 50f + 2f;

        // STAB
        float stab = HasSTAB(atk, move) ? 1.5f : 1f;

        // CRIT simple (1/24)
        float crit = (UnityEngine.Random.Range(0, 24) == 0) ? 1.5f : 1f;

        // Efectividad (TODO: tabla de tipos)
        float eff = 1f;

        float dmg = baseDmg * stab * crit * eff;
        int finalDmg = Mathf.Max(1, Mathf.RoundToInt(dmg));
        return finalDmg;
    }

    // ---------- Helpers ----------
    private static bool HasSTAB(PokemonInstance attacker, MoveData move)
    {
        if (attacker?.species == null || move == null) return false;

        PokemonType moveAsPkmType = ConvertElementToPokemonType(move.type);

        return attacker.species.primaryType == moveAsPkmType
            || attacker.species.secondaryType == moveAsPkmType;
    }

    private static PokemonType ConvertElementToPokemonType(ElementType e)
    {
        // Los enums tienen el mismo orden que en tus definiciones (salvo "None" al final de PokemonType).
        // Con un cast por índice es suficiente y eficiente.
        return (PokemonType)(int)e;

        // Alternativa segura por nombre:
        // return Enum.TryParse<PokemonType>(e.ToString(), out var p) ? p : PokemonType.None;
    }
}
