// Servicios/ExperienceService.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ExperienceService
{
    public struct Gain { public PokemonInstance mon; public int expGained; public int levelsGained; }

    // Gen III+ simplificado: floor( b * L / 7 ) * trainerMod
    public static int CalcExpYield(PokemonInstance defeated, bool isTrainerBattle = false)
    {
        if (defeated == null || defeated.species == null) return 0;
        int b = Mathf.Max(1, defeated.species.baseExperienceYield);
        int L = Mathf.Max(1, defeated.level);
        float baseTotal = Mathf.Floor(b * (L / 7f));
        if (isTrainerBattle) baseTotal *= 1.5f;
        return Mathf.Max(1, Mathf.FloorToInt(baseTotal));
    }

    public static List<Gain> DistributeAndApply(
        IList<PokemonInstance> party,
        HashSet<PokemonInstance> participants,
        PokemonInstance activePlayer,
        PokemonInstance defeatedEnemy,
        bool isTrainerBattle = false)
    {
        var res = new List<Gain>();
        if (defeatedEnemy == null) return res;

        int total = CalcExpYield(defeatedEnemy, isTrainerBattle);

        var targets = new List<PokemonInstance>();
        if (participants != null) foreach (var p in participants) if (p != null && p.currentHP > 0) targets.Add(p);
        if (targets.Count == 0 && activePlayer != null && activePlayer.currentHP > 0) targets.Add(activePlayer);

        int share = Mathf.Max(1, Mathf.FloorToInt(total / Mathf.Max(1, targets.Count)));

        foreach (var mon in targets)
        {
            if (mon == null || mon.level >= 100) continue;
            int beforeLevel = mon.level;
            mon.AddExp(share);
            int lvUps = Mathf.Max(0, mon.level - beforeLevel);
            res.Add(new Gain { mon = mon, expGained = share, levelsGained = lvUps });
        }
        return res;
    }
}
