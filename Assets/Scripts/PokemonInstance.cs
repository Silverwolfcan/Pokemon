using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum Nature
{
    Hardy, Lonely, Brave, Adamant, Naughty,
    Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive,
    Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky
}

[Serializable] public struct PokemonStats { public int MaxHP, Attack, Defense, SpAttack, SpDefense, Speed; }

[Serializable]
public struct IVs
{
    [Range(0, 31)] public int HP, Attack, Defense, SpAttack, SpDefense, Speed;
    public static IVs RandomIVs(System.Random rng = null)
    {
        rng ??= new System.Random();
        return new IVs
        {
            HP = rng.Next(0, 32),
            Attack = rng.Next(0, 32),
            Defense = rng.Next(0, 32),
            SpAttack = rng.Next(0, 32),
            SpDefense = rng.Next(0, 32),
            Speed = rng.Next(0, 32)
        };
    }
}

[Serializable]
public struct EVs
{
    [Range(0, 252)] public int HP, Attack, Defense, SpAttack, SpDefense, Speed;
    public int Total => HP + Attack + Defense + SpAttack + SpDefense + Speed;
    public void ClampTotals()
    {
        HP = Mathf.Clamp(HP, 0, 252); Attack = Mathf.Clamp(Attack, 0, 252); Defense = Mathf.Clamp(Defense, 0, 252);
        SpAttack = Mathf.Clamp(SpAttack, 0, 252); SpDefense = Mathf.Clamp(SpDefense, 0, 252); Speed = Mathf.Clamp(Speed, 0, 252);
        int overflow = Mathf.Max(0, Total - 510);
        if (overflow > 0)
        {
            Reduce(ref Speed, ref overflow); Reduce(ref SpDefense, ref overflow); Reduce(ref SpAttack, ref overflow);
            Reduce(ref Defense, ref overflow); Reduce(ref Attack, ref overflow); Reduce(ref HP, ref overflow);
        }
        static void Reduce(ref int s, ref int o) { if (o <= 0) return; int r = Mathf.Min(s, o); s -= r; o -= r; }
    }
}

[Serializable]
public class MoveInstance
{
    public MoveData data;
    public int currentPP;
    public int maxPP;
    public MoveInstance() { }
    public MoveInstance(MoveData move) { data = move; maxPP = move != null ? move.pp : 0; currentPP = maxPP; }
    public void RestoreAllPP() => currentPP = maxPP;
    public bool TryConsumePP(int amount = 1) { if (currentPP < amount) return false; currentPP -= amount; return true; }
}

[Serializable]
public class PokemonInstance : ISerializationCallbackReceiver
{
    [Header("Identity")]
    [SerializeField] private string uniqueID;
    public string UniqueID => uniqueID;

    [Tooltip("Datos base de la especie")] public PokemonData species;

    [Header("Main")]
    public int level = 1;
    public Gender gender = Gender.Unknown;
    public bool isShiny = false;
    public Nature nature = Nature.Hardy;

    [Header("Stats")]
    public IVs ivs = IVs.RandomIVs();
    public EVs evs;
    public PokemonStats stats;
    public int currentHP;

    [Header("Progression")] public int currentExp;

    [Header("Abilities & Moves")]
    public AbilityData ability;

    [SerializeField] private List<MoveInstance> moves = new List<MoveInstance>(4);
    public IReadOnlyList<MoveInstance> Moves => moves;

    [Header("Origin / Misc")]
    public string originalTrainerName;
    public int originalTrainerID;
    public string caughtLocation;
    public int metLevel;
    public string pokeballName;

    public PokemonInstance() { uniqueID = Guid.NewGuid().ToString(); }

    public PokemonInstance(PokemonData species, int level, Gender gender = Gender.Unknown, bool isShiny = false, Nature? forcedNature = null, AbilityData forcedAbility = null)
        : this()
    {
        this.species = species;
        this.level = Mathf.Clamp(level, 1, 100);
        this.isShiny = isShiny;
        nature = forcedNature ?? RollNature();
        ability = forcedAbility != null ? forcedAbility : PickAbilityFromSpecies();
        this.gender = (gender == Gender.Unknown) ? ResolveGenderFromSpecies(species) : gender;

        ivs = IVs.RandomIVs();
        evs = new EVs();
        RecalculateStats();
        currentHP = stats.MaxHP;
        InitializeMovesFromLearnset();
        CompactMoves(); // asegura huecos al final
    }

    private static Gender ResolveGenderFromSpecies(PokemonData s)
    {
        if (s == null) return Gender.Unknown;
        switch (s.genderRule)
        {
            case GenderRule.AlwaysMale: return Gender.Male;
            case GenderRule.AlwaysFemale: return Gender.Female;
            case GenderRule.Genderless: return Gender.Unknown;
            case GenderRule.Ratio:
            default:
                return UnityEngine.Random.value < Mathf.Clamp01(s.maleRatio) ? Gender.Male : Gender.Female;
        }
    }

    public void RecalculateStats()
    {
        stats = new PokemonStats
        {
            MaxHP = CalcHP(species.baseHP, ivs.HP, evs.HP, level),
            Attack = CalcOther(species.baseAttack, ivs.Attack, evs.Attack, level, GetNatureMod(StatName.Attack)),
            Defense = CalcOther(species.baseDefense, ivs.Defense, evs.Defense, level, GetNatureMod(StatName.Defense)),
            SpAttack = CalcOther(species.baseSpAttack, ivs.SpAttack, evs.SpAttack, level, GetNatureMod(StatName.SpAttack)),
            SpDefense = CalcOther(species.baseSpDefense, ivs.SpDefense, evs.SpDefense, level, GetNatureMod(StatName.SpDefense)),
            Speed = CalcOther(species.baseSpeed, ivs.Speed, evs.Speed, level, GetNatureMod(StatName.Speed))
        };
        currentHP = Mathf.Clamp(currentHP, 0, stats.MaxHP);
    }

    public void HealAll() { currentHP = stats.MaxHP; foreach (var m in moves) m?.RestoreAllPP(); }

    public void AddExp(int amount)
    {
        if (amount <= 0) return;
        currentExp += amount;
        while (currentExp >= ExpToNextLevel() && level < 100)
        {
            currentExp -= ExpToNextLevel();
            level++;
            RecalculateStats();
            currentHP = stats.MaxHP;
            AutoLearnNewMovesOnLevelUp();
            CompactMoves();
        }
    }

    public void ApplyEVGain(int hp = 0, int atk = 0, int def = 0, int spa = 0, int spd = 0, int spe = 0)
    {
        evs.HP += hp; evs.Attack += atk; evs.Defense += def; evs.SpAttack += spa; evs.SpDefense += spd; evs.Speed += spe;
        evs.ClampTotals(); RecalculateStats();
    }

    public bool LearnMove(MoveData moveData, int replaceIndex = -1)
    {
        if (moveData == null) return false;
        for (int i = 0; i < moves.Count; i++) if (moves[i] != null && moves[i].data == moveData) return false;
        for (int i = 0; i < 4; i++) { if (i >= moves.Count) moves.Add(null); if (moves[i] == null) { moves[i] = new MoveInstance(moveData); CompactMoves(); return true; } }
        if (replaceIndex >= 0 && replaceIndex < 4) { moves[replaceIndex] = new MoveInstance(moveData); CompactMoves(); return true; }
        return false;
    }

    public void ForgetMoveAt(int index) { if (index < 0 || index >= 4) return; while (moves.Count < 4) moves.Add(null); moves[index] = null; CompactMoves(); }
    public void SetMoveAt(int index, MoveInstance move) { if (index < 0 || index >= 4) return; while (moves.Count < 4) moves.Add(null); moves[index] = move; CompactMoves(); }

    public bool SwapMoves(int a, int b)
    {
        while (moves.Count < 4) moves.Add(null);
        if (a < 0 || a >= 4 || b < 0 || b >= 4 || a == b) return false;
        var tmp = moves[a];
        moves[a] = moves[b];
        moves[b] = tmp;
        CompactMoves();
        return true;
    }

    public bool MoveMove(int from, int to)
    {
        while (moves.Count < 4) moves.Add(null);
        if (from < 0 || from >= 4 || to < 0 || to >= 4 || from == to) return false;
        var item = moves[from];
        if (item == null) return false;
        moves.RemoveAt(from);
        moves.Insert(to, item);
        while (moves.Count > 4) moves.RemoveAt(moves.Count - 1);
        CompactMoves();
        return true;
    }

    // === NUEVO: compacta movimientos (no-nulos al principio, nulls al final) ===
    // === NUEVO: compacta movimientos (no-nulos al principio, nulls al final).
    //     Considera "vacío" también cualquier MoveInstance con data == null y lo normaliza a null.
    public bool CompactMoves()
    {
        bool changed = false;
        if (moves == null) moves = new List<MoveInstance>(4);

        // normaliza: cualquier MoveInstance sin data => null
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i] != null && moves[i].data == null)
            {
                moves[i] = null;
                changed = true;
            }
        }

        // asegurar tamaño 4
        while (moves.Count < 4) { moves.Add(null); changed = true; }
        if (moves.Count > 4) { moves.RemoveRange(4, moves.Count - 4); changed = true; }

        // compactar: mover no-nulos (con data) al principio
        var compact = new List<MoveInstance>(4);
        for (int i = 0; i < 4; i++)
            if (moves[i] != null && moves[i].data != null)
                compact.Add(moves[i]);

        for (int i = 0; i < 4; i++)
        {
            var v = (i < compact.Count) ? compact[i] : null;
            if (!ReferenceEquals(v, moves[i])) changed = true;
            moves[i] = v;
        }
        return changed;
    }


    public PokemonInstance DeepCopy(bool newID = false)
    {
        var clone = JsonUtility.FromJson<PokemonInstance>(JsonUtility.ToJson(this));
        if (newID) clone.uniqueID = Guid.NewGuid().ToString();
        return clone;
    }

    private void InitializeMovesFromLearnset()
    {
        if (species == null || species.learnableAttacks == null) return;
        foreach (var entry in species.learnableAttacks)
            if (entry.level <= level) LearnMove(entry.attackData);
    }

    private void AutoLearnNewMovesOnLevelUp()
    {
        if (species == null || species.learnableAttacks == null) return;
        foreach (var entry in species.learnableAttacks)
            if (entry.level == level) LearnMove(entry.attackData);
    }

    private Nature RollNature() { Array values = Enum.GetValues(typeof(Nature)); return (Nature)values.GetValue(UnityEngine.Random.Range(0, values.Length)); }
    private AbilityData PickAbilityFromSpecies()
    {
        if (species == null || species.possibleAbilities == null || species.possibleAbilities.Length == 0) return null;
        int idx = UnityEngine.Random.Range(0, species.possibleAbilities.Length);
        return species.possibleAbilities[idx];
    }

    private int ExpToNextLevel()
    {
        int nextLevel = Mathf.Min(level + 1, 100);
        return GrowthExpToLevel(species.growthRate, nextLevel) - GrowthExpToLevel(species.growthRate, level);
    }

    private static int GrowthExpToLevel(GrowthRateGroup group, int lvl)
    {
        lvl = Mathf.Clamp(lvl, 1, 100);
        switch (group)
        {
            case GrowthRateGroup.Fast: return Mathf.FloorToInt(0.8f * lvl * lvl * lvl);
            case GrowthRateGroup.MediumFast: return lvl * lvl * lvl;
            case GrowthRateGroup.MediumSlow: return Mathf.FloorToInt(1.2f * lvl * lvl * lvl - 15 * lvl * lvl + 100 * lvl - 140);
            case GrowthRateGroup.Slow: return Mathf.FloorToInt(1.25f * lvl * lvl * lvl);
            case GrowthRateGroup.Erratic:
                return Mathf.FloorToInt((lvl <= 50) ? (lvl * lvl * lvl * (100 - lvl)) / 50f
                                                     : (lvl <= 68) ? (lvl * lvl * lvl * (150 - lvl)) / 100f
                                                     : (lvl <= 98) ? (lvl * lvl * lvl * ((1911 - 10 * lvl) / 3f)) / 500f
                                                               : (lvl * lvl * lvl * (160 - lvl)) / 100f);
            case GrowthRateGroup.Fluctuating:
                return Mathf.FloorToInt((lvl <= 15) ? (lvl * lvl * lvl * ((float)(24 + ((lvl + 1) / 3)))) / 50f
                                                     : (lvl <= 36) ? (lvl * lvl * lvl * ((float)(lvl + 14))) / 50f
                                                               : (lvl * lvl * lvl * ((float)(lvl + 14))) / 50f);
            default: return lvl * lvl * lvl;
        }
    }

    private enum StatName { Attack, Defense, SpAttack, SpDefense, Speed }
    private float GetNatureMod(StatName stat)
    {
        switch (nature)
        {
            case Nature.Lonely: return stat == StatName.Attack ? 1.1f : stat == StatName.Defense ? 0.9f : 1f;
            case Nature.Brave: return stat == StatName.Attack ? 1.1f : stat == StatName.Speed ? 0.9f : 1f;
            case Nature.Adamant: return stat == StatName.Attack ? 1.1f : stat == StatName.SpAttack ? 0.9f : 1f;
            case Nature.Naughty: return stat == StatName.Attack ? 1.1f : stat == StatName.SpDefense ? 0.9f : 1f;

            case Nature.Bold: return stat == StatName.Defense ? 1.1f : stat == StatName.Attack ? 0.9f : 1f;
            case Nature.Relaxed: return stat == StatName.Defense ? 1.1f : stat == StatName.Speed ? 0.9f : 1f;
            case Nature.Impish: return stat == StatName.Defense ? 1.1f : stat == StatName.SpAttack ? 0.9f : 1f;
            case Nature.Lax: return stat == StatName.Defense ? 1.1f : stat == StatName.SpDefense ? 0.9f : 1f;

            case Nature.Timid: return stat == StatName.Speed ? 1.1f : stat == StatName.Attack ? 0.9f : 1f;
            case Nature.Hasty: return stat == StatName.Speed ? 1.1f : stat == StatName.Defense ? 0.9f : 1f;
            case Nature.Jolly: return stat == StatName.Speed ? 1.1f : stat == StatName.SpAttack ? 0.9f : 1f;
            case Nature.Naive: return stat == StatName.Speed ? 1.1f : stat == StatName.SpDefense ? 0.9f : 1f;

            case Nature.Modest: return stat == StatName.SpAttack ? 1.1f : stat == StatName.Attack ? 0.9f : 1f;
            case Nature.Mild: return stat == StatName.SpAttack ? 1.1f : stat == StatName.Defense ? 0.9f : 1f;
            case Nature.Quiet: return stat == StatName.SpAttack ? 1.1f : stat == StatName.Speed ? 0.9f : 1f;
            case Nature.Rash: return stat == StatName.SpAttack ? 1.1f : stat == StatName.SpDefense ? 0.9f : 1f;

            case Nature.Calm: return stat == StatName.SpDefense ? 1.1f : stat == StatName.Attack ? 0.9f : 1f;
            case Nature.Gentle: return stat == StatName.SpDefense ? 1.1f : stat == StatName.Defense ? 0.9f : 1f;
            case Nature.Sassy: return stat == StatName.SpDefense ? 1.1f : stat == StatName.Speed ? 0.9f : 1f;
            case Nature.Careful: return stat == StatName.SpDefense ? 1.1f : stat == StatName.SpAttack ? 0.9f : 1f;

            default: return 1f;
        }
    }

    private static int CalcHP(int baseStat, int iv, int ev, int level)
    { int term = (2 * baseStat + iv + (ev / 4)); return Mathf.FloorToInt((term * level) / 100f) + level + 10; }
    private static int CalcOther(int baseStat, int iv, int ev, int level, float natureMod)
    { int term = (2 * baseStat + iv + (ev / 4)); int baseVal = Mathf.FloorToInt((term * level) / 100f) + 5; return Mathf.FloorToInt(baseVal * natureMod); }

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (string.IsNullOrEmpty(uniqueID)) uniqueID = Guid.NewGuid().ToString();
        if (moves == null) moves = new List<MoveInstance>(4);
        while (moves.Count < 4) moves.Add(null);
        CompactMoves();
        evs.ClampTotals();
        if (species != null) { RecalculateStats(); currentHP = Mathf.Clamp(currentHP, 0, stats.MaxHP); }
    }
}
