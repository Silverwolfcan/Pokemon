// IA/WildAI.cs
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public static class WildAI
{
    // Selección principal
    public static int ChooseMoveIndex(PokemonInstance attacker, PokemonInstance defender)
    {
        if (attacker == null || defender == null || attacker.Moves == null || attacker.Moves.Count == 0) return 0;

        var atkSC = FindContainer(attacker);
        var defSC = FindContainer(defender);

        int best = -1;
        float bestScore = float.NegativeInfinity;

        int atkMax = attacker.stats.MaxHP;
        int atkHP = Mathf.Clamp(attacker.currentHP, 0, atkMax);
        float atkMissingFrac = Mathf.Clamp01((atkMax - atkHP) / (float)Mathf.Max(1, atkMax));

        int defHP = Mathf.Max(1, defender.currentHP);

        // Velocidad efectiva para flinch
        int spdAtk = EffSpeed(attacker, atkSC);
        int spdDef = EffSpeed(defender, defSC);
        bool goesFirstLikely = spdAtk >= spdDef;

        for (int i = 0; i < attacker.Moves.Count; i++)
        {
            var mi = attacker.Moves[i];
            if (mi == null || mi.data == null) continue;
            if (mi.currentPP <= 0) continue;

            var m = mi.data;

            float score;

            // 1) Movimientos de daño
            if (m.attackCategory != AttackType.Status && m.power > 0)
            {
                // Precisión efectiva con etapas
                var (accMulAtk, _) = BattleStageService.GetAccEva(attacker);
                var (_, evaMulDef) = BattleStageService.GetAccEva(defender);
                float accProb = Mathf.Clamp01((m.accuracy / 100f) * (accMulAtk / Mathf.Max(0.0001f, evaMulDef)));

                // Daño base de un hit
                int dmg1 = MoveExecutor.ComputeDamage(attacker, defender, m);

                // Multigolpe: esperanza de golpes
                float expectedHits = m.isMultiHit ? (Mathf.Max(1, m.hitsRange.x) + Mathf.Max(m.hitsRange.x, m.hitsRange.y)) * 0.5f : 1f;
                float expectedDamage = dmg1 * expectedHits;

                // Esperado con precisión
                score = expectedDamage * accProb;

                // KO bonus fuerte
                if (expectedDamage >= defHP) score *= 10f;

                // Flinch si suele ir primero
                if (m.causesFlinch && goesFirstLikely)
                    score *= 1f + (m.flinchChance / 100f) * 0.5f;

                // Drenaje: valora más si falta vida
                if (m.hasDrain && m.drainPercentOfDamage > 0)
                    score *= 1f + (m.drainPercentOfDamage / 100f) * 0.3f * (0.5f + atkMissingFrac);

                // Retroceso: penaliza, más si te queda poca vida
                if (m.hasRecoil && m.recoilPercentOfDamage > 0)
                    score *= 1f - (m.recoilPercentOfDamage / 100f) * 0.3f * (0.5f + (1f - atkMissingFrac));
            }
            else
            {
                // 2) Movimientos de estado/etapas
                score = 0.01f; // base mínima para desempates

                // Estado primario si el objetivo no tiene
                if (m.statusToApply != StatusAilment.None && defSC != null && defSC.Primary == StatusService.PrimaryStatus.None && m.hasSecondaryEffect)
                {
                    float p = Mathf.Clamp01(m.secondaryChance / 100f);
                    float value = StatusUtilityWeight(m.statusToApply, attacker, defender);
                    score += p * value;
                }

                // Cambios de etapas
                if (!m.stageDelta.IsZero)
                {
                    int mag = AbsSum(m.stageDelta);
                    bool self = m.effectTarget == MoveTarget.Self;
                    float dir = self ? +1f : +1f; // buff propio o debuff rival, valen ambos
                    score += dir * mag * 0.5f;
                }
            }

            // Pequeña aleatoriedad para evitar bucles
            score *= Random.Range(0.98f, 1.02f);

            if (score > bestScore) { bestScore = score; best = i; }
        }

        if (best >= 0) return best;

        // Fallback simple si algo falla
        return ChooseMoveIndex(attacker);
    }

    // Fallback sin objetivo (heurística antigua)
    public static int ChooseMoveIndex(PokemonInstance wild)
    {
        if (wild == null || wild.Moves == null || wild.Moves.Count == 0) return 0;

        var usableDamage = new List<(int idx, float score)>();
        var usableStatus = new List<int>();

        for (int i = 0; i < wild.Moves.Count; i++)
        {
            var mi = wild.Moves[i];
            if (mi == null || mi.data == null) continue;
            if (mi.currentPP <= 0) continue;

            bool isStatus = (mi.data.attackCategory == AttackType.Status) || mi.data.power <= 0;

            if (!isStatus)
            {
                float acc = Mathf.Clamp(mi.data.accuracy, 1, 100) / 100f;
                float stab = HasSTAB(wild, mi.data) ? 1.5f : 1f;
                float score = Mathf.Max(1f, mi.data.power) * acc * stab;
                usableDamage.Add((i, score));
            }
            else usableStatus.Add(i);
        }

        if (usableDamage.Count > 0) return WeightedPick(usableDamage);
        if (usableStatus.Count > 0) return usableStatus[Random.Range(0, usableStatus.Count)];
        return 0;
    }

    // ---------- Helpers ----------
    private static int AbsSum(StageDelta s)
        => Mathf.Abs(s.attack) + Mathf.Abs(s.defense) + Mathf.Abs(s.spAttack) + Mathf.Abs(s.spDefense)
         + Mathf.Abs(s.speed) + Mathf.Abs(s.accuracy) + Mathf.Abs(s.evasion);

    private static float StatusUtilityWeight(StatusAilment a, PokemonInstance atk, PokemonInstance def)
    {
        // valor relativo por impacto general
        switch (a)
        {
            case StatusAilment.Sleep: return 8f;
            case StatusAilment.Freeze: return 7f;
            case StatusAilment.Paralysis: return 5f;
            case StatusAilment.Burn: return 5f;
            case StatusAilment.Poison: return 4f;
            default: return 0f;
        }
    }

    private static int EffSpeed(PokemonInstance p, StatusContainer sc)
    {
        if (p == null) return 1;
        int spd = BattleStageService.GetModifiedStat(p, p.stats.Speed, BattleStageService.StatKind.Speed);
        if (sc != null) spd = Mathf.Max(1, Mathf.FloorToInt(spd * StatusService.GetSpeedMulByStatus(sc.Primary)));
        return Mathf.Max(1, spd);
    }

    private static StatusContainer FindContainer(PokemonInstance model)
    {
        var all = Object.FindObjectsByType<StatusContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var sc in all)
        {
            if (sc == null) continue;
            var t = typeof(StatusContainer);
            var p = t.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
            PokemonInstance m = null;
            if (p != null) m = p.GetValue(sc) as PokemonInstance;
            if (m == null)
            {
                var f = t.GetField("_pokemon", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) m = f.GetValue(sc) as PokemonInstance;
            }
            if (ReferenceEquals(m, model)) return sc;
        }
        return null;
    }

    private static bool HasSTAB(PokemonInstance attacker, MoveData move)
    {
        if (attacker?.species == null || move == null) return false;
        PokemonType moveAsType = (PokemonType)(int)move.type;
        return attacker.species.primaryType == moveAsType || attacker.species.secondaryType == moveAsType;
    }

    private static int WeightedPick(List<(int idx, float score)> items)
    {
        float total = 0f;
        for (int i = 0; i < items.Count; i++) total += Mathf.Max(0.001f, items[i].score);
        float roll = Random.value * total;
        float accum = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            accum += Mathf.Max(0.001f, items[i].score);
            if (roll <= accum) return items[i].idx;
        }
        return items[items.Count - 1].idx;
    }
}
