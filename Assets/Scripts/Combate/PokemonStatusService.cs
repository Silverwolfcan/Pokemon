using System;
using System.Collections.Generic;
using UnityEngine;

public static class PokemonStatusService
{
    // Eventos para HUD u otros sistemas
    public static event Action<PokemonInstance> OnStatusChanged;
    public static event Action<PokemonInstance, PrimaryStatus> OnStatusChangedDetailed;

    private class State
    {
        public PrimaryStatus status = PrimaryStatus.None;
        public int sleepTurns = 0;
    }

    // id único → estado
    private static readonly Dictionary<string, State> _map = new();

    private static State Bucket(PokemonInstance p)
    {
        if (p == null) return null;
        if (!_map.TryGetValue(p.UniqueID, out var s))
        {
            s = new State();
            _map[p.UniqueID] = s;
        }
        return s;
    }

    public static PrimaryStatus GetStatus(PokemonInstance p)
    {
        var s = Bucket(p);
        return s?.status ?? PrimaryStatus.None;
    }

    // ------- Compatibilidad y API pública -------

    /// Cura TODOS los estados del Pokémon. (Sobrecarga sin parámetro para compatibilidad)
    public static bool Cure(PokemonInstance p) => CureAll(p);

    /// Cura solo el estado indicado. Devuelve true si cambia algo.
    public static bool Cure(PokemonInstance p, PrimaryStatus which)
    {
        var s = Bucket(p);
        if (s == null) return false;

        if (which == PrimaryStatus.None) return CureAll(p);

        if (s.status == which)
        {
            s.status = PrimaryStatus.None;
            s.sleepTurns = 0;
            RaiseChanged(p, s.status);
            return true;
        }
        return false;
    }

    /// Cura todos los estados. Devuelve true si había alguno aplicado.
    public static bool CureAll(PokemonInstance p)
    {
        var s = Bucket(p);
        if (s == null) return false;
        if (s.status == PrimaryStatus.None) return false;

        s.status = PrimaryStatus.None;
        s.sleepTurns = 0;
        RaiseChanged(p, s.status);
        return true;
    }

    /// Alias de compatibilidad con código existente.
    public static void Clear(PokemonInstance p) => CureAll(p);

    /// Intenta aplicar un estado primario si el objetivo no tenía ninguno.
    public static bool TryApplyPrimary(PokemonInstance p, PrimaryStatus st)
    {
        var s = Bucket(p);
        if (s == null) return false;

        if (st == PrimaryStatus.None)
            return CureAll(p);

        if (s.status != PrimaryStatus.None) return false;

        s.status = st;
        if (st == PrimaryStatus.Sleep)
            s.sleepTurns = UnityEngine.Random.Range(1, 4); // 1–3 turnos

        RaiseChanged(p, s.status);
        return true;
    }

    /// Ticks al inicio de turno.
    public static void ApplyStartOfTurnTicks(PokemonInstance p)
    {
        var s = Bucket(p);
        if (s == null) return;

        // 20% de descongelarse al inicio de turno
        if (s.status == PrimaryStatus.Freeze && UnityEngine.Random.value < 0.20f)
        {
            s.status = PrimaryStatus.None;
            RaiseChanged(p, s.status);
        }
    }

    /// Ticks al final de turno.
    public static void ApplyEndOfTurnTicks(PokemonInstance p)
    {
        var s = Bucket(p);
        if (s == null) return;

        bool changedHP = false;

        switch (s.status)
        {
            case PrimaryStatus.Burn:
                changedHP |= DamageFraction(p, 1f / 16f);
                break;
            case PrimaryStatus.Poison:
                changedHP |= DamageFraction(p, 1f / 8f);
                break;
            case PrimaryStatus.Sleep:
                if (s.sleepTurns > 0) s.sleepTurns--;
                if (s.sleepTurns <= 0)
                {
                    s.status = PrimaryStatus.None;
                    RaiseChanged(p, s.status);
                }
                break;
        }

        if (changedHP) RaiseGameEventBusChanged(p);
    }

    /// Limpia todos los registros (p.ej. al terminar combate globalmente).
    public static void ClearAll() => _map.Clear();

    // ------- Internos -------

    private static bool DamageFraction(PokemonInstance p, float frac)
    {
        if (p == null || p.stats.MaxHP <= 0) return false;
        int dmg = Mathf.Max(1, Mathf.FloorToInt(p.stats.MaxHP * frac));
        int prev = p.currentHP;
        p.currentHP = Mathf.Max(0, p.currentHP - dmg);
        return p.currentHP != prev;
    }

    private static void RaiseChanged(PokemonInstance p, PrimaryStatus s)
    {
        try { OnStatusChanged?.Invoke(p); } catch { }
        try { OnStatusChangedDetailed?.Invoke(p, s); } catch { }
        RaiseGameEventBusChanged(p);
    }

    private static void RaiseGameEventBusChanged(PokemonInstance p)
    {
        try
        {
            var t = Type.GetType("GameEventBus");
            var m = t?.GetMethod("RaisePokemonChanged",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            m?.Invoke(null, new object[] { p });
        }
        catch { }
    }
}
