using UnityEngine;

public enum PokemonType
{
    Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground,
    Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy, None
}

public enum GrowthRateGroup
{
    Fast,
    MediumFast,
    MediumSlow,
    Slow,
    Fluctuating,
    Erratic
}


public enum Gender
{
    Male,
    Female,
    Unknown // A�adido para representar Pok�mon sin g�nero (como muchos legendarios)
}


public enum PokemonBehaviorType
{
    Friendly,
    Aggressive,
    Idle
}


[System.Serializable]
public class LearnableAttackEntry
{
    public AttackData attackData;
    public int level;
}

[CreateAssetMenu(menuName = "Pok�mon/Pokemon Complete Data")]
public class PokemonData : ScriptableObject
{
    [Header("Identidad")]
    public string pokemonName;            // Nombre interno del Pok�mon
    public string displayName;            // Nombre que se muestra al jugador
    [TextArea(2, 5)]
    public string description;
    public int nationalDexNumber;         // N�mero en la Pok�dex
    public Sprite pokemonSprite;          // Sprite para la UI
    public GameObject prefab;             // Prefab 3D del Pok�mon

    [Header("Tipos")]
    public PokemonType primaryType;
    public PokemonType secondaryType;

    [Header("Captura y Comportamiento")]
    [Range(0f, 1f)] public float catchRate = 0.5f;
    public PokemonBehaviorType behaviorType;

    [Header("Estad�sticas base")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpAttack;
    public int baseSpDefense;
    public int baseSpeed;

    [Header("Crecimiento")]
    public int baseExperienceYield;
    public GrowthRateGroup growthRate;

    [Header("Sexo")]
    public Gender genderType = Gender.Male;

    // Solo se usa si genderType es Male o Female
    [Range(0f, 1f)]
    public float maleRatio = 0.5f;

    [Header("Habilidades")]
    public AbilityData[] possibleAbilities;

    [Header("Listado de Ataques Aprendibles por Nivel")]
    public LearnableAttackEntry[] learnableAttacks;




}
