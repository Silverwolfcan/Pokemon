using UnityEngine;

/// Enums de apoyo usados por MoveData para modificar “stages”.
public enum BattleStat
{
    Attack,
    Defense,
    SpAttack,     // coincide con tu PokemonStats.SpAttack
    SpDefense,    // coincide con tu PokemonStats.SpDefense
    Speed,
    Accuracy,
    Evasion
}

public enum StageTarget
{
    Self,
    Opponent
}
