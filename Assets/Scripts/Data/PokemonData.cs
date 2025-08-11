using UnityEngine;

public enum PokemonType
{
    Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground,
    Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy, None
}

public enum GrowthRateGroup
{
    Fast, MediumFast, MediumSlow, Slow, Fluctuating, Erratic
}

public enum Gender
{
    Male, Female, Unknown // Unknown = sin género
}

public enum PokemonBehaviorType
{
    Friendly, Aggressive, Idle
}

[System.Serializable]
public class LearnableAttackEntry
{
    public MoveData attackData;
    public int level;
}

public enum GenderRule
{
    Ratio,        // usa maleRatio 0..1
    AlwaysMale,
    AlwaysFemale,
    Genderless    // = Unknown
}

[CreateAssetMenu(menuName = "Pokémon/Pokemon Complete Data")]
public class PokemonData : ScriptableObject
{
    [Header("Identidad")]
    public string pokemonName;
    public string displayName;
    [TextArea(2, 5)] public string description;
    public int nationalDexNumber;
    public Sprite pokemonSprite;
    public GameObject prefab;

    [Header("Tipos")]
    public PokemonType primaryType;
    public PokemonType secondaryType;

    [Header("Captura y Comportamiento")]
    [Range(0f, 1f)] public float catchRate = 0.5f;
    public PokemonBehaviorType behaviorType;

    [Header("Estadísticas base")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpAttack;
    public int baseSpDefense;
    public int baseSpeed;

    [Header("Crecimiento")]
    public int baseExperienceYield;
    public GrowthRateGroup growthRate;

    [Header("Sexo (reglas de especie)")]
    public GenderRule genderRule = GenderRule.Ratio;
    [Range(0f, 1f)] public float maleRatio = 0.5f; // solo aplica si genderRule == Ratio

    [Header("Habilidades")]
    public AbilityData[] possibleAbilities;

    [Header("Listado de Ataques Aprendibles por Nivel")]
    public LearnableAttackEntry[] learnableAttacks;
}
