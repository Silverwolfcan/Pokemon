using System;
using System.Collections.Generic;

public static class TypeEffectiveness
{
    private static readonly Dictionary<(PokemonType atk, PokemonType def), float> map = new();

    static TypeEffectiveness()
    {
        void Set(PokemonType a, PokemonType d, float v) => map[(a, d)] = v;

        // Normal
        Set(PokemonType.Normal, PokemonType.Rock, 0.5f);
        Set(PokemonType.Normal, PokemonType.Steel, 0.5f);
        Set(PokemonType.Normal, PokemonType.Ghost, 0f);

        // Fire
        Set(PokemonType.Fire, PokemonType.Fire, 0.5f);
        Set(PokemonType.Fire, PokemonType.Water, 0.5f);
        Set(PokemonType.Fire, PokemonType.Grass, 2f);
        Set(PokemonType.Fire, PokemonType.Ice, 2f);
        Set(PokemonType.Fire, PokemonType.Bug, 2f);
        Set(PokemonType.Fire, PokemonType.Rock, 0.5f);
        Set(PokemonType.Fire, PokemonType.Dragon, 0.5f);
        Set(PokemonType.Fire, PokemonType.Steel, 2f);

        // Water
        Set(PokemonType.Water, PokemonType.Fire, 2f);
        Set(PokemonType.Water, PokemonType.Water, 0.5f);
        Set(PokemonType.Water, PokemonType.Grass, 0.5f);
        Set(PokemonType.Water, PokemonType.Ground, 2f);
        Set(PokemonType.Water, PokemonType.Rock, 2f);
        Set(PokemonType.Water, PokemonType.Dragon, 0.5f);

        // Electric
        Set(PokemonType.Electric, PokemonType.Water, 2f);
        Set(PokemonType.Electric, PokemonType.Electric, 0.5f);
        Set(PokemonType.Electric, PokemonType.Grass, 0.5f);
        Set(PokemonType.Electric, PokemonType.Ground, 0f);
        Set(PokemonType.Electric, PokemonType.Flying, 2f);
        Set(PokemonType.Electric, PokemonType.Dragon, 0.5f);

        // Ice
        Set(PokemonType.Ice, PokemonType.Grass, 2f);
        Set(PokemonType.Ice, PokemonType.Ground, 2f);
        Set(PokemonType.Ice, PokemonType.Flying, 2f);
        Set(PokemonType.Ice, PokemonType.Dragon, 2f);
        Set(PokemonType.Ice, PokemonType.Fire, 0.5f);
        Set(PokemonType.Ice, PokemonType.Water, 0.5f);
        Set(PokemonType.Ice, PokemonType.Ice, 0.5f);
        Set(PokemonType.Ice, PokemonType.Steel, 0.5f);

        // Fighting
        Set(PokemonType.Fighting, PokemonType.Normal, 2f);
        Set(PokemonType.Fighting, PokemonType.Ice, 2f);
        Set(PokemonType.Fighting, PokemonType.Rock, 2f);
        Set(PokemonType.Fighting, PokemonType.Dark, 2f);
        Set(PokemonType.Fighting, PokemonType.Steel, 2f);
        Set(PokemonType.Fighting, PokemonType.Poison, 0.5f);
        Set(PokemonType.Fighting, PokemonType.Flying, 0.5f);
        Set(PokemonType.Fighting, PokemonType.Psychic, 0.5f);
        Set(PokemonType.Fighting, PokemonType.Bug, 0.5f);
        Set(PokemonType.Fighting, PokemonType.Fairy, 0.5f);
        Set(PokemonType.Fighting, PokemonType.Ghost, 0f);

        // Poison
        Set(PokemonType.Poison, PokemonType.Grass, 2f);
        Set(PokemonType.Poison, PokemonType.Poison, 0.5f);
        Set(PokemonType.Poison, PokemonType.Ground, 0.5f);
        Set(PokemonType.Poison, PokemonType.Rock, 0.5f);
        Set(PokemonType.Poison, PokemonType.Ghost, 0.5f);
        Set(PokemonType.Poison, PokemonType.Fairy, 2f);
        Set(PokemonType.Poison, PokemonType.Steel, 0f);

        // Ground
        Set(PokemonType.Ground, PokemonType.Fire, 2f);
        Set(PokemonType.Ground, PokemonType.Electric, 2f);
        Set(PokemonType.Ground, PokemonType.Grass, 0.5f);
        Set(PokemonType.Ground, PokemonType.Poison, 2f);
        Set(PokemonType.Ground, PokemonType.Flying, 0f);
        Set(PokemonType.Ground, PokemonType.Bug, 0.5f);
        Set(PokemonType.Ground, PokemonType.Rock, 2f);
        Set(PokemonType.Ground, PokemonType.Steel, 2f);

        // Flying
        Set(PokemonType.Flying, PokemonType.Grass, 2f);
        Set(PokemonType.Flying, PokemonType.Fighting, 2f);
        Set(PokemonType.Flying, PokemonType.Bug, 2f);
        Set(PokemonType.Flying, PokemonType.Electric, 0.5f);
        Set(PokemonType.Flying, PokemonType.Rock, 0.5f);
        Set(PokemonType.Flying, PokemonType.Steel, 0.5f);

        // Psychic
        Set(PokemonType.Psychic, PokemonType.Fighting, 2f);
        Set(PokemonType.Psychic, PokemonType.Poison, 2f);
        Set(PokemonType.Psychic, PokemonType.Psychic, 0.5f);
        Set(PokemonType.Psychic, PokemonType.Steel, 0.5f);
        Set(PokemonType.Psychic, PokemonType.Dark, 0f);

        // Bug
        Set(PokemonType.Bug, PokemonType.Grass, 2f);
        Set(PokemonType.Bug, PokemonType.Psychic, 2f);
        Set(PokemonType.Bug, PokemonType.Dark, 2f);
        Set(PokemonType.Bug, PokemonType.Fire, 0.5f);
        Set(PokemonType.Bug, PokemonType.Fighting, 0.5f);
        Set(PokemonType.Bug, PokemonType.Poison, 0.5f);
        Set(PokemonType.Bug, PokemonType.Flying, 0.5f);
        Set(PokemonType.Bug, PokemonType.Ghost, 0.5f);
        Set(PokemonType.Bug, PokemonType.Steel, 0.5f);
        Set(PokemonType.Bug, PokemonType.Fairy, 0.5f);

        // Rock
        Set(PokemonType.Rock, PokemonType.Fire, 2f);
        Set(PokemonType.Rock, PokemonType.Ice, 2f);
        Set(PokemonType.Rock, PokemonType.Flying, 2f);
        Set(PokemonType.Rock, PokemonType.Bug, 2f);
        Set(PokemonType.Rock, PokemonType.Fighting, 0.5f);
        Set(PokemonType.Rock, PokemonType.Ground, 0.5f);
        Set(PokemonType.Rock, PokemonType.Steel, 0.5f);

        // Ghost
        Set(PokemonType.Ghost, PokemonType.Psychic, 2f);
        Set(PokemonType.Ghost, PokemonType.Ghost, 2f);
        Set(PokemonType.Ghost, PokemonType.Dark, 0.5f);
        Set(PokemonType.Ghost, PokemonType.Normal, 0f);

        // Dragon
        Set(PokemonType.Dragon, PokemonType.Dragon, 2f);
        Set(PokemonType.Dragon, PokemonType.Steel, 0.5f);
        Set(PokemonType.Dragon, PokemonType.Fairy, 0f);

        // Dark
        Set(PokemonType.Dark, PokemonType.Psychic, 2f);
        Set(PokemonType.Dark, PokemonType.Ghost, 2f);
        Set(PokemonType.Dark, PokemonType.Fighting, 0.5f);
        Set(PokemonType.Dark, PokemonType.Dark, 0.5f);
        Set(PokemonType.Dark, PokemonType.Fairy, 0.5f);

        // Steel
        Set(PokemonType.Steel, PokemonType.Rock, 2f);
        Set(PokemonType.Steel, PokemonType.Ice, 2f);
        Set(PokemonType.Steel, PokemonType.Fairy, 2f);
        Set(PokemonType.Steel, PokemonType.Fire, 0.5f);
        Set(PokemonType.Steel, PokemonType.Water, 0.5f);
        Set(PokemonType.Steel, PokemonType.Electric, 0.5f);
        Set(PokemonType.Steel, PokemonType.Steel, 0.5f);

        // Fairy
        Set(PokemonType.Fairy, PokemonType.Fighting, 2f);
        Set(PokemonType.Fairy, PokemonType.Dragon, 2f);
        Set(PokemonType.Fairy, PokemonType.Dark, 2f);
        Set(PokemonType.Fairy, PokemonType.Fire, 0.5f);
        Set(PokemonType.Fairy, PokemonType.Poison, 0.5f);
        Set(PokemonType.Fairy, PokemonType.Steel, 0.5f);
    }

    public static float GetMultiplier(PokemonType attack, PokemonType defend)
    {
        if (defend == PokemonType.None) return 1f;
        return map.TryGetValue((attack, defend), out var v) ? v : 1f;
    }

    public static float GetMultiplier(PokemonType attack, PokemonType def1, PokemonType def2)
    {
        float m1 = GetMultiplier(attack, def1);
        if (def2 == PokemonType.None || def2 == def1) return m1;
        return m1 * GetMultiplier(attack, def2);
    }

    // Overload por comodidad si partes de ElementType del movimiento.
    public static float GetMultiplier(ElementType moveType, PokemonType def1, PokemonType def2)
    {
        var atk = ToPokemonType(moveType);
        return GetMultiplier(atk, def1, def2);
    }

    private static PokemonType ToPokemonType(ElementType e)
    {
        // Seguro por nombre. Si tus enums comparten orden, puedes castear.
        return Enum.TryParse(e.ToString(), out PokemonType p) ? p : PokemonType.None;
    }
}
