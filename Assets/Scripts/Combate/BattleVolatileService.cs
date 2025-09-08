using System.Collections.Generic;
using UnityEngine;

/// Volátiles de combate: flinch y “ya actuó”.
public static class BattleVolatileService
{
    private sealed class V { public bool flinchNext; public bool actedThisTurn; }

    private static readonly Dictionary<PokemonInstance, V> map =
        new Dictionary<PokemonInstance, V>(PokemonRefComparer.Instance);

    private static V Get(PokemonInstance p)
    {
        if (p == null) return null;
        if (!map.TryGetValue(p, out var v)) { v = new V(); map[p] = v; }
        return v;
    }

    public static void ResetAll() => map.Clear();

    public static void SetFlinchNextTurn(PokemonInstance p, bool on = true)
    { var v = Get(p); if (v != null) v.flinchNext = on; }

    public static bool ConsumeFlinch(PokemonInstance p)
    {
        var v = Get(p); if (v == null || !v.flinchNext) return false;
        v.flinchNext = false;
        Debug.Log($"[Volatile] {p.DisplayName} se estremece y no puede moverse (flinch).");
        return true;
    }

    public static void StartTurn(PokemonInstance p)
    { var v = Get(p); if (v != null) v.actedThisTurn = false; }

    public static void MarkActed(PokemonInstance p)
    { var v = Get(p); if (v != null) v.actedThisTurn = true; }

    public static bool HasActedThisTurn(PokemonInstance p)
    { var v = Get(p); return v != null && v.actedThisTurn; }

    private sealed class PokemonRefComparer : IEqualityComparer<PokemonInstance>
    {
        public static readonly PokemonRefComparer Instance = new PokemonRefComparer();
        public bool Equals(PokemonInstance x, PokemonInstance y) => ReferenceEquals(x, y);
        public int GetHashCode(PokemonInstance obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
