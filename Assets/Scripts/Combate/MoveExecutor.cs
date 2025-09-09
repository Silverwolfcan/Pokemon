// Servicios/MoveExecutor.cs
using UnityEngine;

public static class MoveExecutor
{
    public struct MoveResolution
    {
        public int totalDamage;
        public int hits;
        public int drainHeal;
        public int recoilDamage;
        public bool appliedStatus;
        public bool appliedStages;
        public bool targetFlinched;
    }

    public static bool CheckAccuracy(PokemonInstance atk, PokemonInstance def, int accuracy)
    {
        if (accuracy <= 0) return true;
        var (accMulAtk, _) = BattleStageService.GetAccEva(atk);
        var (_, evaMulDef) = BattleStageService.GetAccEva(def);
        float finalAcc = Mathf.Clamp01((accuracy / 100f) * (accMulAtk / Mathf.Max(0.0001f, evaMulDef)));
        return Random.value <= finalAcc;
    }

    // 0.01*B*E*V*(((0.2*N+1)*A*P)/(25*D)+2)
    public static int ComputeDamage(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        if (atk == null || def == null || move == null || atk.species == null || def.species == null) return 0;
        if (move.attackCategory == AttackType.Status || move.power <= 0) return 0;

        int N = Mathf.Max(1, atk.level);
        int P = Mathf.Max(1, move.power);

        int A = (move.attackCategory == AttackType.Physical)
            ? BattleStageService.GetModifiedStat(atk, atk.stats.Attack, BattleStageService.StatKind.Attack)
            : BattleStageService.GetModifiedStat(atk, atk.stats.SpAttack, BattleStageService.StatKind.SpAttack);

        // quemado reduce ataque físico
        if (move.attackCategory == AttackType.Physical)
        {
            var scAtk = FindContainerFor(atk);
            if (scAtk != null)
                A = Mathf.Max(1, Mathf.FloorToInt(A * StatusService.GetAttackMulByStatus(scAtk.Primary)));
        }

        int D = (move.attackCategory == AttackType.Physical)
            ? BattleStageService.GetModifiedStat(def, def.stats.Defense, BattleStageService.StatKind.Defense)
            : BattleStageService.GetModifiedStat(def, def.stats.SpDefense, BattleStageService.StatKind.SpDefense);

        PokemonType mvType = (PokemonType)(int)move.type;
        float B = TypeChartService.HasSTAB(atk, mvType) ? 1.5f : 1f;
        float E = TypeChartService.GetCombinedMultiplier(mvType, def.species.primaryType, def.species.secondaryType);
        if (E <= 0f) return 0;

        int V = Random.Range(85, 101);
        float inner = (((0.2f * N + 1f) * Mathf.Max(1, A) * Mathf.Max(1, P)) / (25f * Mathf.Max(1, D))) + 2f;
        return Mathf.Max(1, Mathf.FloorToInt(0.01f * B * E * V * inner));
    }

    public static MoveResolution ResolveMove(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        var res = new MoveResolution { hits = 1 };
        if (atk == null || def == null || move == null) return res;

        int hits = 1;
        if (move.isMultiHit)
        {
            int minH = Mathf.Max(1, move.hitsRange.x);
            int maxH = Mathf.Max(minH, move.hitsRange.y);
            hits = Random.Range(minH, maxH + 1);
        }

        int total = 0;
        for (int i = 0; i < hits; i++) total += ComputeDamage(atk, def, move);

        res.hits = hits;
        res.totalDamage = total;

        if (move.hasDrain && total > 0)
            res.drainHeal = Mathf.FloorToInt(total * Mathf.Clamp01(move.drainPercentOfDamage / 100f));

        if (move.hasRecoil && total > 0)
            res.recoilDamage = Mathf.FloorToInt(total * Mathf.Clamp01(move.recoilPercentOfDamage / 100f));

        if (move.hasSecondaryEffect && Random.value <= Mathf.Clamp01(move.secondaryChance / 100f))
        {
            var tgtPI = (move.effectTarget == MoveTarget.Self) ? atk : def;
            var sc = FindContainerFor(tgtPI);

            if (move.statusToApply != StatusAilment.None && sc != null)
                res.appliedStatus = sc.TryApplyPrimary(MapToPrimary(move.statusToApply));

            if (!move.stageDelta.IsZero)
            {
                var who = (move.effectTarget == MoveTarget.Self) ? atk : def;
                BattleStageService.ApplyDelta(who, move.stageDelta);
                res.appliedStages = true;
            }
        }

        if (move.causesFlinch && Random.value <= Mathf.Clamp01(move.flinchChance / 100f))
            res.targetFlinched = true;

        return res;
    }

    public static bool ApplyStatusAndStagesOnly(PokemonInstance user, PokemonInstance target, MoveData move)
    {
        if (user == null || target == null || move == null) return false;
        bool any = false;
        if (move.hasSecondaryEffect && Random.value <= Mathf.Clamp01(move.secondaryChance / 100f))
        {
            var tgt = (move.effectTarget == MoveTarget.Self) ? user : target;
            var sc = FindContainerFor(tgt);
            if (move.statusToApply != StatusAilment.None && sc != null)
                any |= sc.TryApplyPrimary(MapToPrimary(move.statusToApply));
            if (!move.stageDelta.IsZero)
            {
                var who = (move.effectTarget == MoveTarget.Self) ? user : target;
                BattleStageService.ApplyDelta(who, move.stageDelta);
                any = true;
            }
        }
        return any;
    }

    private static StatusService.PrimaryStatus MapToPrimary(StatusAilment a)
    {
        switch (a)
        {
            case StatusAilment.Burn: return StatusService.PrimaryStatus.Burn;
            case StatusAilment.Poison: return StatusService.PrimaryStatus.Poison;
            case StatusAilment.Sleep: return StatusService.PrimaryStatus.Sleep;
            case StatusAilment.Paralysis: return StatusService.PrimaryStatus.Paralysis;
            case StatusAilment.Freeze: return StatusService.PrimaryStatus.Freeze;
            default: return StatusService.PrimaryStatus.None;
        }
    }

    private static StatusContainer FindContainerFor(PokemonInstance model)
    {
        var all = Object.FindObjectsByType<StatusContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var sc = all[i];
            if (sc == null) continue;
            var t = typeof(StatusContainer);
            var p = t.GetProperty("Model", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            PokemonInstance m = null;
            if (p != null) m = p.GetValue(sc) as PokemonInstance;
            if (m == null)
            {
                var f = t.GetField("_pokemon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) m = f.GetValue(sc) as PokemonInstance;
            }
            if (ReferenceEquals(m, model)) return sc;
        }
        return null;
    }
}
