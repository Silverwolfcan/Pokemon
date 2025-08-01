using UnityEngine;

[System.Serializable]
public class PokemonStats
{
    public int HP;
    public int Attack;
    public int Defense;
    public int SpAttack;
    public int SpDefense;
    public int Speed;
}

public static class PokemonStatsCalculator
{
    public static PokemonStats CalculateAllStats(PokemonData data, int level,
        int ivHP = 31, int evHP = 0,
        int ivAtk = 31, int evAtk = 0,
        int ivDef = 31, int evDef = 0,
        int ivSpA = 31, int evSpA = 0,
        int ivSpD = 31, int evSpD = 0,
        int ivSpe = 31, int evSpe = 0)
    {
        PokemonStats stats = new PokemonStats();

        stats.HP = CalculateHPStat(data.baseHP, ivHP, evHP, level);
        stats.Attack = CalculateOtherStat(data.baseAttack, ivAtk, evAtk, level);
        stats.Defense = CalculateOtherStat(data.baseDefense, ivDef, evDef, level);
        stats.SpAttack = CalculateOtherStat(data.baseSpAttack, ivSpA, evSpA, level);
        stats.SpDefense = CalculateOtherStat(data.baseSpDefense, ivSpD, evSpD, level);
        stats.Speed = CalculateOtherStat(data.baseSpeed, ivSpe, evSpe, level);

        return stats;
    }

    private static int CalculateHPStat(int baseStat, int iv, int ev, int level)
    {
        return Mathf.FloorToInt((((2 * baseStat + iv + Mathf.FloorToInt(ev / 4f)) * level) / 100f) + level + 10);
    }

    private static int CalculateOtherStat(int baseStat, int iv, int ev, int level)
    {
        return Mathf.FloorToInt((((2 * baseStat + iv + Mathf.FloorToInt(ev / 4f)) * level) / 100f) + 5);
    }
}
