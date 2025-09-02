using UnityEngine;

public enum HealingEffectType
{
    // HP
    HealFixed,       // cura 'amount' PS
    HealPercent,     // cura 'percent'% de PS máx
    HealToFull,      // cura al 100% (no revive)

    // Revivir
    ReviveToHalf,    // revive al 50% PS máx
    ReviveToFull,    // revive al 100% PS máx

    // Estados (curas específicas)
    CurePoison,
    CureParalysis,
    CureSleep,
    CureBurn,
    CureFreeze,
    CureAllStatus,

    // PP (restauración de puntos de poder)
    RestorePPSingleFixed,   // restaura 'ppAmount' a un movimiento (moveIndex)
    RestorePPSingleFull,    // restaura al máximo un movimiento (moveIndex)
    RestorePPAllFixed,      // restaura 'ppAmount' a todos los movimientos
    RestorePPAllFull        // restaura al máximo todos los movimientos
}

[CreateAssetMenu(fileName = "New HealingItem", menuName = "Pokémon/Items/Healing Item")]
public class HealingItemData : ItemData
{
    [Header("Efecto principal")]
    public HealingEffectType effect = HealingEffectType.HealFixed;

    [Header("Parámetros HP")]
    [Min(0)] public int amount = 20;         // usado en HealFixed
    [Range(1, 100)] public int percent = 50; // usado en HealPercent

    [Header("Parámetros PP")]
    [Min(1)] public int ppAmount = 10;       // usado en RestorePP*Fixed

    [Header("Estados alterados")]
    [Tooltip("Si además de curar PS debe curar estados principales (p.ej. Restaurar Todo).")]
    public bool curePrimaryStatus = false;

    [Header("Restricciones de uso")]
    public bool usableInBattle = true;
    public bool usableOutsideBattle = true;

    // Utilidad: categorías lógicas para ordenación fina
    public bool IsRevive() =>
        effect == HealingEffectType.ReviveToHalf || effect == HealingEffectType.ReviveToFull;

    public bool IsPPRestore() =>
        effect == HealingEffectType.RestorePPSingleFixed || effect == HealingEffectType.RestorePPSingleFull ||
        effect == HealingEffectType.RestorePPAllFixed || effect == HealingEffectType.RestorePPAllFull;

    public bool IsStatusOnly() =>
        effect == HealingEffectType.CurePoison || effect == HealingEffectType.CureParalysis ||
        effect == HealingEffectType.CureSleep || effect == HealingEffectType.CureBurn ||
        effect == HealingEffectType.CureFreeze || effect == HealingEffectType.CureAllStatus;
}
