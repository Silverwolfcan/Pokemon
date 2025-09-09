using UnityEngine;

public enum AttackType { Physical, Special, Status }
public enum ElementType
{
    Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground,
    Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy
}
public enum MoveTarget { Opponent, Self }
public enum StatusAilment { None, Poison, Burn, Paralysis, Sleep, Freeze }

[System.Serializable]
public struct StageDelta
{
    [Range(-6, 6)] public int attack, defense, spAttack, spDefense, speed, accuracy, evasion;
    public bool IsZero =>
        attack == 0 && defense == 0 && spAttack == 0 && spDefense == 0 &&
        speed == 0 && accuracy == 0 && evasion == 0;
}

[CreateAssetMenu(menuName = "PokemonLike/Move Data")]
public class MoveData : ScriptableObject
{
    [Header("Datos base")]
    public string moveName;
    public ElementType type;
    [Min(0)] public int power = 0;
    [Range(0, 100)] public int accuracy = 100;
    public AttackType attackCategory = AttackType.Physical;
    [Min(0)] public int pp = 20;                   // ← restaurado para compatibilidad

    [Header("Secundarios: estado/etapas")]
    public bool hasSecondaryEffect = false;
    [Range(0, 100)] public int secondaryChance = 100;
    public MoveTarget effectTarget = MoveTarget.Opponent;
    public StatusAilment statusToApply = StatusAilment.None;
    public StageDelta stageDelta;

    [Header("Flinch")]
    public bool causesFlinch = false;
    [Range(0, 100)] public int flinchChance = 0;

    [Header("Drenaje")]
    public bool hasDrain = false;
    [Range(0, 100)] public int drainPercentOfDamage = 0;

    [Header("Retroceso")]
    public bool hasRecoil = false;
    [Range(0, 100)] public int recoilPercentOfDamage = 0;

    [Header("Multigolpe")]
    public bool isMultiHit = false;
    public Vector2Int hitsRange = new Vector2Int(2, 5);

    [Header("Descripción")]
    [TextArea] public string secondaryEffectDescription;
}
