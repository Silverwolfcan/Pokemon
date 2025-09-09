// Servicios/MoveExecutor.cs
using UnityEngine;
using System;
using System.Reflection;

public static class MoveExecutor
{
    // --------- Precisión con etapas ----------
    public static bool CheckAccuracy(PokemonInstance atk, PokemonInstance def, int accuracy)
    {
        if (accuracy <= 0) return true; // 0 = siempre acierta

        var (accMulAtk, _) = BattleStageService.GetAccEva(atk);
        var (_, evaMulDef) = BattleStageService.GetAccEva(def);

        float finalAcc = Mathf.Clamp01((accuracy / 100f) * (accMulAtk / Mathf.Max(0.0001f, evaMulDef)));
        return UnityEngine.Random.value <= finalAcc;
    }

    // --------- Daño ----------
    // Fórmula acordada:
    // Damage = 0.01 * B * E * V * (((0.2*N + 1) * A * P) / (25*D) + 2)
    // B = STAB (1 o 1.5), E = efectividad por tipos, V = variación [85..100]
    public static int ComputeDamage(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        if (atk == null || def == null || move == null || atk.species == null || def.species == null)
            return 0;

        if (move.attackCategory == AttackType.Status || move.power <= 0)
            return 0;

        int N = Mathf.Max(1, atk.level);
        int P = Mathf.Max(1, move.power);

        int A = (move.attackCategory == AttackType.Physical)
            ? BattleStageService.GetModifiedStat(atk, atk.stats.Attack, BattleStageService.StatKind.Attack)
            : BattleStageService.GetModifiedStat(atk, atk.stats.SpAttack, BattleStageService.StatKind.SpAttack);

        int D = (move.attackCategory == AttackType.Physical)
            ? BattleStageService.GetModifiedStat(def, def.stats.Defense, BattleStageService.StatKind.Defense)
            : BattleStageService.GetModifiedStat(def, def.stats.SpDefense, BattleStageService.StatKind.SpDefense);

        // STAB y efectividad
        PokemonType mvType = ConvertElementToPokemonType(move.type); // <- corregido
        float B = TypeChartService.HasSTAB(atk, mvType) ? 1.5f : 1f;
        float E = TypeChartService.GetCombinedMultiplier(mvType, def.species.primaryType, def.species.secondaryType);
        if (E <= 0f) return 0;

        // Variación
        int V = UnityEngine.Random.Range(85, 101);

        float inner = (((0.2f * N + 1f) * Mathf.Max(1, A) * Mathf.Max(1, P)) / (25f * Mathf.Max(1, D))) + 2f;
        float dmgF = 0.01f * B * E * V * inner;

        int damage = Mathf.Max(1, Mathf.FloorToInt(dmgF));
        return damage;
    }

    // --------- Efectos secundarios ----------
    // Aplica estados primarios y confusión al objetivo si están definidos en MoveData.
    // No asume un schema fijo: lee por reflexión campos habituales.
    public static bool ApplySecondaryEffects(PokemonInstance atk, PokemonInstance def, MoveData move)
    {
        if (atk == null || def == null || move == null) return false;

        // Buscar StatusContainer del defensor por su PokemonInstance.
        var all = UnityEngine.Object.FindObjectsByType<StatusContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        StatusContainer targetContainer = null;
        foreach (var sc in all)
        {
            if (sc == null) continue;
            var model = GetModel(sc);
            if (object.ReferenceEquals(model, def)) { targetContainer = sc; break; }
        }

        // A) Estado primario (statusToApply + statusApplyChance/statusChance)
        try
        {
            var ailment = GetFieldOrProp(move, "statusToApply");
            if (ailment != null && ailment.GetType().Name == "StatusAilment")
            {
                int chance = GetIntFieldOrProp(move, "statusApplyChance",
                              GetIntFieldOrProp(move, "statusChance", 100));
                bool pass = UnityEngine.Random.value <= Mathf.Clamp01(chance / 100f);
                if (pass && targetContainer != null)
                {
                    var mapped = MapToPrimaryStatus(ailment);
                    if (mapped.HasValue && mapped.Value != StatusService.PrimaryStatus.None)
                    {
                        targetContainer.TryApplyPrimary(mapped.Value);
                        Debug.Log($"[Status] {(def != null ? def.DisplayName : "-")} -> {mapped.Value}");
                    }
                }
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Status] Error aplicando estado primario: {e.Message}"); }

        // B) Confusión (causesConfusion/inflictConfusion + confusionChance)
        try
        {
            bool cause = GetBoolFieldOrProp(move, "causesConfusion") || GetBoolFieldOrProp(move, "inflictConfusion");
            int confChance = GetIntFieldOrProp(move, "confusionChance", cause ? 100 : 0);
            if (confChance > 0)
                cause = UnityEngine.Random.value <= Mathf.Clamp01(confChance / 100f);

            if (cause && targetContainer != null)
            {
                targetContainer.ApplyConfusion();
                Debug.Log($"[Status] {(def != null ? def.DisplayName : "-")} -> Confusión");
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Status] Error aplicando confusión: {e.Message}"); }

        return true;
    }

    // --------- Util reflejo ----------
    private static object GetFieldOrProp(object obj, string name)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) return f.GetValue(obj);
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return p != null ? p.GetValue(obj) : null;
    }

    private static int GetIntFieldOrProp(object obj, string name, int defVal)
    {
        var o = GetFieldOrProp(obj, name);
        if (o == null) return defVal;
        try
        {
            if (o is int i) return i;
            if (o is float f) return Mathf.RoundToInt(f);
            if (o is double d) return Mathf.RoundToInt((float)d);
            return Convert.ToInt32(o);
        }
        catch { return defVal; }
    }

    private static bool GetBoolFieldOrProp(object obj, string name)
    {
        var o = GetFieldOrProp(obj, name);
        if (o == null) return false;
        try
        {
            if (o is bool b) return b;
            if (o is int i) return i != 0;
            if (o is float f) return f != 0f;
            return Convert.ToBoolean(o);
        }
        catch { return false; }
    }

    // Mapeo StatusAilment -> PrimaryStatus por nombre para evitar dependencias duras
    private static StatusService.PrimaryStatus? MapToPrimaryStatus(object statusAilment)
    {
        if (statusAilment == null) return null;
        string name = statusAilment.ToString();
        switch (name)
        {
            case "Burn": return StatusService.PrimaryStatus.Burn;
            case "Poison": return StatusService.PrimaryStatus.Poison;
            case "Sleep": return StatusService.PrimaryStatus.Sleep;
            case "Paralysis": return StatusService.PrimaryStatus.Paralysis;
            case "Freeze": return StatusService.PrimaryStatus.Freeze;
            default: return StatusService.PrimaryStatus.None;
        }
    }

    // Acceso a PokemonInstance almacenado en StatusContainer
    private static PokemonInstance GetModel(StatusContainer sc)
    {
        // Preferir propiedad pública Model. Si no existe, usa el campo privado _pokemon por reflexión.
        var t = typeof(StatusContainer);
        var p = t.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(sc) as PokemonInstance;
        var f = t.GetField("_pokemon", BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? f.GetValue(sc) as PokemonInstance : null;
    }

    // --------- Util ----------
    private static PokemonType ConvertElementToPokemonType(ElementType e)
    {
        try { return (PokemonType)(int)e; }
        catch { return System.Enum.TryParse<PokemonType>(e.ToString(), out var p) ? p : PokemonType.None; }
    }
}
