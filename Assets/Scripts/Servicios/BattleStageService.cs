using System.Collections.Generic;
using UnityEngine;

/// Servicio de etapas de combate. No toca datos persistentes.
/// Guarda etapas por instancia de Pokémon y provee multiplicadores.
public static class BattleStageService
{
    public enum StatKind { Attack, Defense, SpAttack, SpDefense, Speed, Accuracy, Evasion }

    private struct StageState
    {
        public int atk, def, spa, spd, spe, acc, eva;
        public void Reset() { atk = def = spa = spd = spe = acc = eva = 0; }
    }

    private static readonly Dictionary<PokemonInstance, StageState> map = new Dictionary<PokemonInstance, StageState>();

    // ---- API pública ----
    public static void ResetFor(PokemonInstance p)
    {
        if (p == null) return;
        map.Remove(p);
    }

    public static void ResetForAll(params PokemonInstance[] arr)
    {
        if (arr == null) return;
        foreach (var p in arr) ResetFor(p);
    }

    public static void ApplyDelta(PokemonInstance p, StageDelta delta)
    {
        if (p == null) return;
        var s = Get(p);
        s.atk = ClampStage(s.atk + delta.attack);
        s.def = ClampStage(s.def + delta.defense);
        s.spa = ClampStage(s.spa + delta.spAttack);
        s.spd = ClampStage(s.spd + delta.spDefense);
        s.spe = ClampStage(s.spe + delta.speed);
        s.acc = ClampStage(s.acc + delta.accuracy);
        s.eva = ClampStage(s.eva + delta.evasion);
        map[p] = s;
    }

    public static float GetMultiplier(PokemonInstance p, StatKind kind)
    {
        var s = Get(p);
        switch (kind)
        {
            case StatKind.Attack: return StageToMul_AtkDefSpe(s.atk);
            case StatKind.Defense: return StageToMul_AtkDefSpe(s.def);
            case StatKind.SpAttack: return StageToMul_AtkDefSpe(s.spa);
            case StatKind.SpDefense: return StageToMul_AtkDefSpe(s.spd);
            case StatKind.Speed: return StageToMul_AtkDefSpe(s.spe);
            case StatKind.Accuracy: return StageToMul_AccEva(s.acc);
            case StatKind.Evasion: return StageToMul_AccEva(s.eva);
            default: return 1f;
        }
    }

    public static (float accMul, float evaMul) GetAccEva(PokemonInstance p)
    {
        var s = Get(p);
        return (StageToMul_AccEva(s.acc), StageToMul_AccEva(s.eva));
    }

    public static int GetModifiedStat(PokemonInstance p, int baseStat, StatKind kind)
    {
        float mul = GetMultiplier(p, kind);
        return Mathf.Max(1, Mathf.FloorToInt(baseStat * mul));
    }

    public static int GetStageValue(PokemonInstance p, StatKind kind)
    {
        var s = Get(p);
        switch (kind)
        {
            case StatKind.Attack: return s.atk;
            case StatKind.Defense: return s.def;
            case StatKind.SpAttack: return s.spa;
            case StatKind.SpDefense: return s.spd;
            case StatKind.Speed: return s.spe;
            case StatKind.Accuracy: return s.acc;
            case StatKind.Evasion: return s.eva;
            default: return 0;
        }
    }

    // ---- Internos ----
    private static StageState Get(PokemonInstance p)
    {
        StageState s;
        if (!map.TryGetValue(p, out s)) { s = new StageState(); s.Reset(); map[p] = s; }
        return s;
    }

    private static int ClampStage(int v) => Mathf.Clamp(v, -6, 6);

    private static float StageToMul_AtkDefSpe(int n)
    {
        if (n >= 0) return (2f + n) / 2f;
        return 2f / (2f - n);
    }

    private static float StageToMul_AccEva(int n)
    {
        if (n >= 0) return (3f + n) / 3f;
        return 3f / (3f - n);
    }
}
