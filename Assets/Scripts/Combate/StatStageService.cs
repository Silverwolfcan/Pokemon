using UnityEngine;
using System;
using System.Collections.Generic;

public static class StatStageService
{
    private static readonly Dictionary<string, Dictionary<BattleStat, int>> _map = new();

    private static Dictionary<BattleStat, int> GetBucket(PokemonInstance p)
    {
        if (p == null) return null;
        var id = p.UniqueID;
        if (!_map.TryGetValue(id, out var dict))
        {
            dict = new Dictionary<BattleStat, int>();
            foreach (BattleStat s in Enum.GetValues(typeof(BattleStat)))
                dict[s] = 0;
            _map[id] = dict;
        }
        return dict;
    }

    public static void ResetAllFor(PokemonInstance p)
    {
        if (p == null) return;
        _map.Remove(p.UniqueID);
    }

    public static void ResetAll() => _map.Clear();

    // Compatibilidad con código existente
    public static void Clear(PokemonInstance p) => ResetAllFor(p);

    public static void Add(PokemonInstance p, BattleStat stat, int delta)
    {
        var b = GetBucket(p);
        if (b == null) return;
        b[stat] = Mathf.Clamp(b[stat] + delta, -6, 6);
    }

    public static void AddByName(PokemonInstance p, string statName, int delta)
    {
        if (!Enum.TryParse(statName, out BattleStat s)) return;
        Add(p, s, delta);
    }

    public static float GetMultiplier(PokemonInstance p, BattleStat stat)
    {
        var b = GetBucket(p);
        if (b == null) return 1f;
        int st = b[stat];
        if (st >= 0) return (2f + st) / 2f;
        return 2f / (2f - st);
    }

    public static float GetStageMultiplier(PokemonInstance p, string statName)
    {
        if (!Enum.TryParse(statName, out BattleStat s)) return 1f;
        return GetMultiplier(p, s);
    }
}
