using UnityEngine;

public enum AttackType { Physical, Special, Status }
public enum ElementType { Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy }

[System.Serializable]
public struct StageChange
{
    public StageTarget target;
    public BattleStat stat;
    [Range(-6, 6)] public int delta;
    [Range(0, 100)] public int chance;
}

[CreateAssetMenu(fileName = "New Move", menuName = "Pokemon/Moves")]
public class MoveData : ScriptableObject
{
    [Header("Básico")]
    public string moveName;
    [TextArea] public string description;
    public ElementType type;
    public AttackType attackCategory;
    public int power = 0; // sólo Physical/Special
    [Range(1, 100)] public int accuracy = 100;
    public int pp = 10;

    [Header("Meta")]
    public int priority = 0; // reservado para uso futuro en orden de turno

    [Header("Secundario (legacy)")]
    public bool hasSecondaryEffect;
    [TextArea] public string secondaryEffectDescription;

    [Header("Aplicación de ESTADO primario")]
    public bool appliesPrimaryStatus = false;
    public PrimaryStatus statusToApply = PrimaryStatus.None;
    [Range(0, 100)] public int statusChance = 100;
    public bool onlyIfDamageDealt = true;

    [Header("Modificador de STATS (stages) — Compatibilidad")]
    public bool modifiesStages = false;
    public StageTarget stageTarget = StageTarget.Opponent;
    public BattleStat stageStat = BattleStat.Attack;
    [Range(-6, 6)] public int stageDelta = -1;
    [Range(0, 100)] public int stageChance = 100;

    [Header("Modificador de STATS (stages) — Múltiples")]
    public StageChange[] stageChanges;

    [Header("Daño especial")]
    public bool isFixedDamage = false;
    public int fixedDamage = 0;

    [Header("Multi-golpe")]
    public bool isMultiHit = false;
    [Min(1)] public int multiHitMin = 2;
    [Min(1)] public int multiHitMax = 5;

    [Header("Robo de vida / retroceso")]
    [Range(0f, 1f)] public float drainPercent = 0f;   // 0.5 = drena 50% del daño total
    [Range(0f, 1f)] public float recoilPercent = 0f;  // 0.33 = sufre 33% del daño hecho

    [Header("Volátiles")]
    public bool causesFlinch = false;
    [Range(0, 100)] public int flinchChance = 0;
}
