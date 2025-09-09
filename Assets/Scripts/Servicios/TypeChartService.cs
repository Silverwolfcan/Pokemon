using UnityEngine;

public static class TypeChartService
{
    // Orden de PokemonType:
    // Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground,
    // Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy, None
    // Fila = ATAQUE, Columna = DEFENSA
    private static readonly float[,] chart = new float[,]
    {
        /* Normal  */ { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.5f, 0f,  1f, 1f, 0.5f, 1f, 1f },
        /* Fire    */ { 1f, 0.5f,0.5f,2f,  1f, 2f, 1f, 1f, 1f, 1f, 1f, 2f, 0.5f, 1f, 0.5f,1f, 2f,  1f, 1f },
        /* Water   */ { 1f, 2f,  0.5f,0.5f,1f, 1f, 1f, 1f, 2f, 1f, 1f, 1f, 2f,   1f, 0.5f,1f, 1f,  1f, 1f },
        /* Grass   */ { 1f, 0.5f,2f,  0.5f,1f, 1f, 1f, 0.5f,2f, 0.5f,1f, 0.5f,2f, 1f, 0.5f,1f, 0.5f,1f, 1f },
        /* Electric*/ { 1f, 1f,  2f,  0.5f,0.5f,1f, 1f, 1f, 0f,  2f, 1f, 1f, 1f,  1f, 0.5f,1f, 1f,  1f, 1f },
        /* Ice     */ { 1f, 0.5f,0.5f,2f,  1f, 0.5f,1f, 1f, 2f, 2f, 1f, 1f, 1f,  1f, 2f,  1f, 0.5f,1f, 1f },
        /* Fighting*/ { 2f, 1f,  1f,  1f,  1f, 2f, 1f, 0.5f,1f, 0.5f,0.5f,0.5f,2f, 0f,  1f, 2f, 2f,  0.5f,1f },
        /* Poison  */ { 1f, 1f,  1f,  2f,  1f, 1f, 1f, 0.5f,0.5f,1f, 1f, 1f, 0.5f,0.5f,1f, 1f, 0f,  2f, 1f },
        /* Ground  */ { 1f, 2f,  1f,  0.5f,2f, 1f, 1f, 2f,  1f, 0f,  1f, 0.5f,2f, 1f, 1f,  1f, 2f,  1f, 1f },
        /* Flying  */ { 1f, 1f,  1f,  2f,  0.5f,1f, 2f, 1f,  1f, 1f, 1f, 2f, 0.5f,1f, 1f,  1f, 0.5f,1f, 1f },
        /* Psychic */ { 1f, 1f,  1f,  1f,  1f, 1f, 2f, 2f,  1f, 1f, 0.5f,1f, 1f,  1f, 1f,  0f,  0.5f,1f, 1f },
        /* Bug     */ { 1f, 0.5f,1f,  2f,  1f, 1f, 0.5f,0.5f,1f, 0.5f,2f, 1f, 1f,  0.5f,1f, 2f,  0.5f,0.5f,1f },
        /* Rock    */ { 1f, 2f,  1f,  1f,  1f, 2f, 0.5f,1f,  0.5f,2f, 1f, 2f, 1f,  1f,  1f, 1f,  0.5f,1f, 1f },
        /* Ghost   */ { 0f,  1f,  1f,  1f,  1f, 1f, 1f, 1f,  1f, 1f, 2f,  1f, 1f,  2f,  1f, 0.5f,1f,  1f, 1f },
        /* Dragon  */ { 1f, 1f,  1f,  1f,  1f, 1f, 1f, 1f,  1f, 1f, 1f, 1f, 1f,  1f,  2f, 1f,  0.5f,0f,  1f },
        /* Dark    */ { 1f, 1f,  1f,  1f,  1f, 1f, 0.5f,1f,  1f, 1f, 2f,  1f, 1f,  2f,  1f, 0.5f,1f,  0.5f,1f },
        /* Steel   */ { 1f, 0.5f,0.5f,1f,  0.5f,2f, 1f, 1f,  1f, 1f, 1f, 1f, 2f,  1f,  1f, 1f,  0.5f,2f, 1f },
        /* Fairy   */ { 1f, 0.5f,1f,  1f,  1f, 1f, 2f, 0.5f,1f, 1f, 1f, 1f, 1f,  1f,  2f, 2f,  0.5f,1f, 1f },
        /* None    */ { 1f, 1f,  1f,  1f,  1f, 1f, 1f, 1f,  1f, 1f, 1f, 1f, 1f,  1f,  1f, 1f,  1f,  1f, 1f },
    };

    public static float GetMultiplier(PokemonType attackType, PokemonType defenseType)
    {
        int a = Mathf.Clamp((int)attackType, 0, chart.GetLength(0) - 1);
        int d = Mathf.Clamp((int)defenseType, 0, chart.GetLength(1) - 1);
        return chart[a, d];
    }

    public static float GetCombinedMultiplier(PokemonType attackType, PokemonType defTypeA, PokemonType defTypeB)
    {
        float m1 = GetMultiplier(attackType, defTypeA);
        if (defTypeB == PokemonType.None || defTypeB == defTypeA) return m1;
        float m2 = GetMultiplier(attackType, defTypeB);
        return m1 * m2;
    }

    public static bool HasSTAB(PokemonInstance attacker, PokemonType moveType)
    {
        if (attacker == null || attacker.species == null) return false;
        return attacker.species.primaryType == moveType || attacker.species.secondaryType == moveType;
    }
}
