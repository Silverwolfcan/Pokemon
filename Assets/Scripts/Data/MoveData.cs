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
    [Range(-6, 6)] public int attack;
    [Range(-6, 6)] public int defense;
    [Range(-6, 6)] public int spAttack;
    [Range(-6, 6)] public int spDefense;
    [Range(-6, 6)] public int speed;
    [Range(-6, 6)] public int accuracy;
    [Range(-6, 6)] public int evasion;

    public bool IsZero =>
        attack == 0 && defense == 0 && spAttack == 0 && spDefense == 0 &&
        speed == 0 && accuracy == 0 && evasion == 0;
}

[CreateAssetMenu(fileName = "New Move", menuName = "Pokemon/Moves")]
public class MoveData : ScriptableObject
{
    [Header("Identidad")]
    public string moveName;
    [TextArea] public string description;

    [Header("Datos base")]
    public ElementType type = ElementType.Normal;
    public AttackType attackCategory = AttackType.Physical;
    [Min(0)] public int power = 0;        // 0 para movimientos de estado
    [Range(0, 100)] public int accuracy = 100; // 0 = siempre acierta (tratado en MoveExecutor)
    [Min(1)] public int pp = 10;
    [Tooltip("Prioridad del movimiento. +1 actúa antes que 0.")] public int priority = 0;
    [Tooltip("Hace contacto físico (para habilidades/objetos).")] public bool makesContact = false;

    [Header("Efectos secundarios / Estado / Buffs")]
    public bool hasSecondaryEffect = false;
    [Range(0, 100)] public int secondaryChance = 100;
    public MoveTarget effectTarget = MoveTarget.Opponent;

    [Tooltip("Estado alterado a aplicar si procede.")]
    public StatusAilment statusToApply = StatusAilment.None;

    [Tooltip("Modificadores de etapas. Positivo = buff, negativo = debuff.")]
    public StageDelta stageDelta;

    [Header("Descripción opcional de efectos")]
    [TextArea] public string secondaryEffectDescription;
}
