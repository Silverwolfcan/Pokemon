using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PokemonInstance
{
    public PokemonData baseData;
    public int level;
    public int currentExp;

    public PokemonStats stats;
    public int currentHP;

    public Gender gender;
    public bool isShiny;

    // Habilidad y ataques
    public AbilityData ability;
    public List<AttackData> learnedAttacks = new List<AttackData>();

    // IVs y EVs
    public int ivHP = 31, ivAttack = 31, ivDefense = 31, ivSpAttack = 31, ivSpDefense = 31, ivSpeed = 31;
    public int evHP = 0, evAttack = 0, evDefense = 0, evSpAttack = 0, evSpDefense = 0, evSpeed = 0;

    //Identificador Unico para cada pokemon
    public string uniqueID;

    public PokemonInstance(PokemonData data, int level, Gender gender = Gender.Unknown, bool isShiny = false)
    {
        this.uniqueID = System.Guid.NewGuid().ToString();
        this.baseData = data;
        this.level = level;
        this.gender = (data.genderType == Gender.Unknown) ? Gender.Unknown : GenerateGender();
        this.isShiny = isShiny;
        this.currentExp = 0;

        stats = PokemonStatsCalculator.CalculateAllStats(data, level, ivHP, evHP, ivAttack, evAttack, ivDefense, evDefense, ivSpAttack, evSpAttack, ivSpDefense, evSpDefense, ivSpeed, evSpeed);
        currentHP = stats.HP;

        // Habilidad aleatoria (si hay varias)
        if (data.possibleAbilities != null && data.possibleAbilities.Length > 0)
            ability = data.possibleAbilities[Random.Range(0, data.possibleAbilities.Length)];

        // Aprender ataques iniciales (hasta nivel actual, si en el futuro tienes ataques por nivel)
        learnedAttacks = new List<AttackData>();
        foreach (var entry in data.learnableAttacks)
        {
            if (entry.level <= level)
            {
                learnedAttacks.Add(entry.attackData);
                if (learnedAttacks.Count >= 4) break;
            }
        }


    }

    private Gender GenerateGender()
    {
        return Random.value < baseData.maleRatio ? Gender.Male : Gender.Female;
    }

    public void GainExperience(int amount)
    {
        currentExp += amount;

        while (level < 100 && currentExp >= GetRequiredExpForLevel(level + 1))
        {
            currentExp -= GetRequiredExpForLevel(level + 1);
            LevelUp();
        }
    }

    private void LevelUp()
    {
        if (level >= 100) return;

        int previousHP = stats.HP;
        level++;

        stats = PokemonStatsCalculator.CalculateAllStats(baseData, level, ivHP, evHP, ivAttack, evAttack, ivDefense, evDefense, ivSpAttack, evSpAttack, ivSpDefense, evSpDefense, ivSpeed, evSpeed);

        int hpGained = stats.HP - previousHP;
        currentHP += hpGained;

        // Aprender nuevos ataques si hay disponibles en el nuevo nivel
        foreach (var entry in baseData.learnableAttacks)
        {
            if (entry.level == level && !learnedAttacks.Contains(entry.attackData))
            {
                if (learnedAttacks.Count >= 4)
                    learnedAttacks.RemoveAt(0); // Reemplazar el ataque más antiguo
                learnedAttacks.Add(entry.attackData);
            }
        }


        Debug.Log($"📈 {baseData.pokemonName} subió al nivel {level}!");
    }

    public int GetRequiredExpForLevel(int level)
    {
        switch (baseData.growthRate)
        {
            case GrowthRateGroup.Fast:
                return Mathf.FloorToInt(4 * Mathf.Pow(level, 3) / 5f);
            case GrowthRateGroup.MediumFast:
                return Mathf.FloorToInt(Mathf.Pow(level, 3));
            case GrowthRateGroup.MediumSlow:
                return Mathf.FloorToInt(1.2f * Mathf.Pow(level, 3) - 15 * Mathf.Pow(level, 2) + 100 * level - 140);
            case GrowthRateGroup.Slow:
                return Mathf.FloorToInt(5 * Mathf.Pow(level, 3) / 4f);
            case GrowthRateGroup.Fluctuating:
                if (level <= 15)
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * ((Mathf.Floor((level + 1) / 3f) + 24f) / 50f));
                else if (level <= 36)
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * ((level + 14f) / 50f));
                else
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * ((Mathf.Floor(level / 2f) + 32f) / 50f));
            case GrowthRateGroup.Erratic:
                if (level <= 50)
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * (100f - level) / 50f);
                else if (level <= 68)
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * (150f - level) / 100f);
                else if (level <= 98)
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * ((1911f - 10f * level) / 3f) / 500f);
                else
                    return Mathf.FloorToInt(Mathf.Pow(level, 3) * (160f - level) / 100f);
            default:
                return Mathf.FloorToInt(Mathf.Pow(level, 3)); // Por defecto MediumFast
        }
    }
}
