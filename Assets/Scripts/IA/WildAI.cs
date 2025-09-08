using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

/// IA salvaje (MVP):
/// - Evita movimientos sin PP.
/// - Penaliza estado redundante.
/// - Considera prioridad y daño esperado.
/// Mantiene firma original y añade sobrecarga con enemigo opcional.
public static class WildAI
{
    /// <summary>
    /// Versión original. Intenta inferir el objetivo desde el TurnController activo.
    /// </summary>
    public static int ChooseMoveIndex(PokemonInstance wild)
    {
        var enemy = InferEnemyForWild(wild);
        return ChooseMoveIndex(wild, enemy);
    }

    /// <summary>
    /// Versión con objetivo explícito para mejor evaluación.
    /// </summary>
    public static int ChooseMoveIndex(PokemonInstance wild, PokemonInstance enemy)
    {
        if (wild == null || wild.Moves == null || wild.Moves.Count == 0) return 0;

        var candidates = new List<(int idx, float score, string reason)>();
        var statusFallback = new List<int>();

        for (int i = 0; i < wild.Moves.Count; i++)
        {
            var mi = wild.Moves[i];
            if (mi == null || mi.data == null) continue;
            if (mi.currentPP <= 0) continue;

            bool isStatus = (mi.data.attackCategory == AttackType.Status) || mi.data.power <= 0;

            if (!isStatus)
            {
                float score = ScoreDamageMove(wild, enemy, mi);
                candidates.Add((i, score, "damage"));
            }
            else
            {
                float statusScore = ScoreStatusMove(wild, enemy, mi);
                if (statusScore > 0f) candidates.Add((i, statusScore, "status"));
                else statusFallback.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            if (statusFallback.Count > 0)
                return statusFallback[UnityEngine.Random.Range(0, statusFallback.Count)];
            return 0;
        }

        int choice = WeightedPick(candidates, wild);
        LogChoice(wild, candidates, choice);
        return choice;
    }

    // ================= Helpers =================

    private static PokemonInstance InferEnemyForWild(PokemonInstance wild)
    {
        try
        {
            var tc = UnityEngine.Object.FindAnyObjectByType<TurnController>();
            var fEnemy = typeof(TurnController).GetField("enemy", BindingFlags.Instance | BindingFlags.NonPublic);
            var fPlayer = typeof(TurnController).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);

            var enemyCbt = fEnemy?.GetValue(tc) as CombatantController;
            var playerCbt = fPlayer?.GetValue(tc) as CombatantController;

            if (enemyCbt != null && ReferenceEquals(enemyCbt.Model, wild))
                return playerCbt != null ? playerCbt.Model : null;

            if (playerCbt != null && ReferenceEquals(playerCbt.Model, wild))
                return enemyCbt != null ? enemyCbt.Model : null;
        }
        catch { }
        return null;
    }

    private static float ScoreDamageMove(PokemonInstance attacker, PokemonInstance defender, MoveInstance mi)
    {
        var md = mi.data;
        float acc = Mathf.Clamp(md.accuracy, 1, 100) / 100f;
        float stab = HasSTAB(attacker, md) ? 1.5f : 1f;
        float prioFactor = 1f + Mathf.Clamp(md.priority, -1, 5) * 0.15f; // ~0.85..1.75

        float typeMul = 1f;
        if (defender?.species != null)
        {
            PokemonType mType = (PokemonType)(int)md.type;
            var d1 = defender.species.primaryType;
            var d2 = defender.species.secondaryType;
            typeMul = TypeEffectiveness.GetMultiplier(mType, d1, d2);
            if (typeMul <= 0f) return 0.01f; // evitar inmunidad
        }

        float basePower = Mathf.Max(1, md.power);
        float score = basePower * acc * stab * Mathf.Max(0.25f, typeMul) * prioFactor;

        float ppFactor = 0.75f + 0.25f * Mathf.Clamp01(mi.currentPP / Mathf.Max(1f, mi.maxPP));
        return score * ppFactor;
    }

    private static float ScoreStatusMove(PokemonInstance attacker, PokemonInstance defender, MoveInstance mi)
    {
        var md = mi.data;

        var (applies, targetStatus, chance, onlyIfDamage) = TryReadPrimaryStatus(md);

        if (onlyIfDamage && (md.attackCategory == AttackType.Status || md.power <= 0))
            return 0f;

        if (defender != null)
        {
            var current = PokemonStatusService.GetStatus(defender);
            if (current != PrimaryStatus.None)
            {
                if (applies && targetStatus == current) return 0.01f;
                return 0.1f;
            }
        }

        if (applies)
        {
            float p = Mathf.Clamp01(chance / 100f);
            float prioFactor = 1f + Mathf.Clamp(md.priority, -1, 5) * 0.1f;
            return Mathf.Max(0.25f, 0.75f * p * prioFactor);
        }

        float stageBias = 0.5f + Mathf.Clamp(md.priority, -1, 5) * 0.1f;
        return Mathf.Clamp(stageBias, 0.25f, 1.25f);
    }

    private static (bool applies, PrimaryStatus status, int chance, bool onlyIfDamage) TryReadPrimaryStatus(MoveData md)
    {
        try
        {
            var t = md.GetType();
            var fApply = t.GetField("appliesPrimaryStatus") ?? t.GetField("AppliesPrimaryStatus");
            var fStatus = t.GetField("statusToApply") ?? t.GetField("StatusToApply");
            var fChance = t.GetField("statusChance") ?? t.GetField("StatusChance");
            var fOnlyIf = t.GetField("onlyIfDamageDealt") ?? t.GetField("OnlyIfDamageDealt");

            bool applies = fApply != null && Convert.ToBoolean(fApply.GetValue(md));
            PrimaryStatus status = fStatus != null ? (PrimaryStatus)fStatus.GetValue(md) : PrimaryStatus.None;
            int chance = fChance != null ? Mathf.Clamp(Convert.ToInt32(fChance.GetValue(md)), 0, 100) : 100;
            bool onlyIf = fOnlyIf != null && Convert.ToBoolean(fOnlyIf.GetValue(md));
            return (applies, status, chance, onlyIf);
        }
        catch { return (false, PrimaryStatus.None, 100, false); }
    }

    private static int WeightedPick(List<(int idx, float score, string reason)> items, PokemonInstance wild)
    {
        float total = 0f;
        for (int i = 0; i < items.Count; i++)
            total += Mathf.Max(0.0001f, items[i].score);

        float roll = UnityEngine.Random.value * total;
        float accum = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            accum += Mathf.Max(0.0001f, items[i].score);
            if (roll <= accum) return items[i].idx;
        }
        return items[items.Count - 1].idx;
    }

    private static void LogChoice(PokemonInstance wild, List<(int idx, float score, string reason)> items, int chosenIdx)
    {
        try
        {
            items.Sort((a, b) => b.score.CompareTo(a.score));
            string msg = "[WildAI] scores: ";
            int n = Mathf.Min(items.Count, 4);
            for (int i = 0; i < n; i++)
            {
                int idx = items[i].idx;
                string name = SafeMoveName(wild, idx);
                msg += $"[{idx}:{name}={items[i].score:0.##} {items[i].reason}] ";
            }
            msg += $"| chosen={chosenIdx}:{SafeMoveName(wild, chosenIdx)}";
            Debug.Log(msg);
        }
        catch { }
    }

    private static string SafeMoveName(PokemonInstance p, int idx)
    {
        try
        {
            return p?.Moves != null && idx >= 0 && idx < p.Moves.Count ? p.Moves[idx]?.data?.moveName ?? "?" : "?";
        }
        catch { return "?"; }
    }

    private static bool HasSTAB(PokemonInstance attacker, MoveData move)
    {
        if (attacker?.species == null || move == null) return false;
        PokemonType moveAsType = (PokemonType)(int)move.type;
        return attacker.species.primaryType == moveAsType || attacker.species.secondaryType == moveAsType;
    }
}
