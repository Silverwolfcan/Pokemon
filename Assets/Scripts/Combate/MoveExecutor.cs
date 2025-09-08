using UnityEngine;
using System;

public static class MoveExecutor
{
    // Comprobación de precisión con stages de Accuracy/Evasion.
    // Devuelve true si impacta.
    public static bool CheckAccuracy(PokemonInstance attacker, PokemonInstance defender, int accuracy)
    {
        int baseAcc = Mathf.Clamp(accuracy, 1, 100);

        // Multiplicadores por stages
        float accMul = StatStageService.GetMultiplier(attacker, BattleStat.Accuracy);
        float evaMul = StatStageService.GetMultiplier(defender, BattleStat.Evasion);
        if (accMul <= 0f) accMul = 1f;
        if (evaMul <= 0f) evaMul = 1f;

        float scaled = baseAcc * (accMul / Mathf.Max(0.25f, evaMul));
        int finalAcc = Mathf.Clamp(Mathf.RoundToInt(scaled), 1, 100);

        bool hit = UnityEngine.Random.Range(0, 100) < finalAcc;
        Debug.Log($"[Accuracy] {attacker?.DisplayName} → {defender?.DisplayName}: base={baseAcc}% accMul={accMul:0.##} evaMul={evaMul:0.##} => final={finalAcc}% -> {(hit ? "Hit" : "Miss")}");
        return hit;
    }

    // Daño + secundarios. Mantiene compatibilidad con lo descrito (STAB, efectividades, stats SpAttack/SpDefense, stages).
    public static void ComputeDamage(PokemonInstance attacker, PokemonInstance defender, MoveData move)
    {
        if (attacker == null || defender == null || move == null) return;
        if (defender.currentHP <= 0) return;

        int totalDamage = 0;
        int hits = 1;
        if (move.isMultiHit)
            hits = Mathf.Clamp(UnityEngine.Random.Range(move.multiHitMin, move.multiHitMax + 1), 1, 10);

        for (int h = 0; h < hits; h++)
        {
            int dmg = move.isFixedDamage ? Mathf.Max(0, move.fixedDamage)
                                         : ComputeSingleHitDamage(attacker, defender, move);

            int before = defender.currentHP;
            defender.currentHP = Mathf.Max(0, defender.currentHP - Mathf.Max(0, dmg));
            totalDamage += Mathf.Max(0, before - defender.currentHP);

            GameEventBus.RaisePokemonChanged(defender);
            Debug.Log($"[Damage] {attacker.DisplayName} usó {move.moveName} ({h + 1}/{hits}) e hizo {Mathf.Max(0, before - defender.currentHP)} daño. HP {before}->{defender.currentHP}");

            if (defender.currentHP <= 0) break;
        }

        TryApplyPrimaryStatus(attacker, defender, move, totalDamage > 0);
        TryApplyStageChanges(attacker, defender, move);
        ApplyDrainAndRecoil(attacker, defender, move, totalDamage);
    }

    // ---------------- internals ----------------

    private static int ComputeSingleHitDamage(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        if (move.attackCategory == AttackType.Status || move.power <= 0) return 0;

        int a = (move.attackCategory == AttackType.Physical) ? atk.stats.Attack : atk.stats.SpAttack;
        int d = (move.attackCategory == AttackType.Physical) ? def.stats.Defense : def.stats.SpDefense;

        var aStage = (move.attackCategory == AttackType.Physical) ? BattleStat.Attack : BattleStat.SpAttack;
        var dStage = (move.attackCategory == AttackType.Physical) ? BattleStat.Defense : BattleStat.SpDefense;
        float aMul = StatStageService.GetMultiplier(atk, aStage);
        float dMul = StatStageService.GetMultiplier(def, dStage);

        float atkEff = Mathf.Max(1f, a) * Mathf.Max(0.25f, aMul);
        float defEff = Mathf.Max(1f, d) * Mathf.Max(0.25f, dMul);

        PokemonType moveAsType = (PokemonType)(int)move.type;
        bool hasSTAB = (atk.species?.primaryType == moveAsType) || (atk.species?.secondaryType == moveAsType);
        float stab = hasSTAB ? 1.5f : 1f;

        PokemonType d1 = def.species != null ? def.species.primaryType : PokemonType.None;
        PokemonType d2 = def.species != null ? def.species.secondaryType : PokemonType.None;
        float eff = TypeEffectiveness.GetMultiplier(moveAsType, d1, d2);

        try { GameEventBus.RaiseEffectivenessComputed(atk, def, move.type, eff); } catch { }

        float baseTerm = move.power * (atkEff / Mathf.Max(1f, defEff));
        float variance = UnityEngine.Random.Range(0.85f, 1.00f);
        float raw = baseTerm * stab * eff * variance;
        int damage = Mathf.Max(1, Mathf.FloorToInt(raw));

        if (eff <= 0f) damage = 0;
        return damage;
    }

    private static void TryApplyPrimaryStatus(PokemonInstance attacker, PokemonInstance defender, MoveData move, bool didDamage)
    {
        if (!move.appliesPrimaryStatus || move.statusToApply == PrimaryStatus.None) return;
        if (move.onlyIfDamageDealt && !didDamage) return;

        int chance = Mathf.Clamp(move.statusChance, 0, 100);
        if (chance < 100 && UnityEngine.Random.Range(0, 100) >= chance) return;

        bool applied = PokemonStatusService.TryApplyPrimary(defender, move.statusToApply);
        if (applied)
            Debug.Log($"[Status] {defender.DisplayName} sufre {move.statusToApply}.");
    }

    private static void TryApplyStageChanges(PokemonInstance attacker, PokemonInstance defender, MoveData move)
    {
        if (move.stageChanges != null && move.stageChanges.Length > 0)
        {
            foreach (var sc in move.stageChanges)
                ApplyStageChange(attacker, defender, sc);
            return;
        }

        if (!move.modifiesStages) return;
        var entry = new StageChange
        {
            target = move.stageTarget,
            stat = move.stageStat,
            delta = move.stageDelta,
            chance = move.stageChance
        };
        ApplyStageChange(attacker, defender, entry);
    }

    private static void ApplyStageChange(PokemonInstance attacker, PokemonInstance defender, StageChange sc)
    {
        int chance = Mathf.Clamp(sc.chance <= 0 ? 100 : sc.chance, 0, 100);
        if (chance < 100 && UnityEngine.Random.Range(0, 100) >= chance) return;

        var target = (sc.target == StageTarget.Self) ? attacker : defender;
        StatStageService.Add(target, sc.stat, sc.delta);

        Debug.Log($"[Stages] {target.DisplayName}: {sc.stat} {(sc.delta >= 0 ? "+" : "")}{sc.delta}.");
    }

    private static void ApplyDrainAndRecoil(PokemonInstance attacker, PokemonInstance defender, MoveData move, int totalDamageDealt)
    {
        if (move.drainPercent > 0f && totalDamageDealt > 0)
        {
            int heal = Mathf.Max(1, Mathf.FloorToInt(totalDamageDealt * Mathf.Clamp01(move.drainPercent)));
            int before = attacker.currentHP;
            attacker.currentHP = Mathf.Min(attacker.stats.MaxHP, attacker.currentHP + heal);
            if (attacker.currentHP != before)
            {
                GameEventBus.RaisePokemonChanged(attacker);
                Debug.Log($"[Drain] {attacker.DisplayName} recupera {attacker.currentHP - before} HP.");
            }
        }

        if (move.recoilPercent > 0f && totalDamageDealt > 0)
        {
            int recoil = Mathf.Max(1, Mathf.FloorToInt(totalDamageDealt * Mathf.Clamp01(move.recoilPercent)));
            int before = attacker.currentHP;
            attacker.currentHP = Mathf.Max(0, attacker.currentHP - recoil);
            if (attacker.currentHP != before)
            {
                GameEventBus.RaisePokemonChanged(attacker);
                Debug.Log($"[Recoil] {attacker.DisplayName} sufre {before - attacker.currentHP} de retroceso.");
            }
        }
    }
}
