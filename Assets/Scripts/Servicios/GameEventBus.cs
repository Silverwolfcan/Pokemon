using System;

/// Bus de eventos ligero. UI se suscribe; servicios emiten.
public static class GameEventBus
{
    // Inventario / Party
    public static event Action InventoryChanged;
    public static event Action PartyChanged;

    // Cambios sobre una instancia concreta (HP/PP/etc.)
    public static event Action<PokemonInstance> PokemonChanged;

    // Estado de encuentro activo
    public static event Action<bool> EncounterStateChanged; // true = en combate

    // Feedback de efectividad (si lo usas en MoveExecutor)
    public static event Action<PokemonInstance, PokemonInstance, ElementType, float> EffectivenessComputed;

    // Feedback de captura: shakes y probabilidad usada
    public static event Action<int, float, bool> CaptureShakesComputed; // shakes, chance [0..1], success

    public static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
    public static void RaisePartyChanged() => PartyChanged?.Invoke();
    public static void RaisePokemonChanged(PokemonInstance p) => PokemonChanged?.Invoke(p);
    public static void RaiseEncounterStateChanged(bool active) => EncounterStateChanged?.Invoke(active);

    public static void RaiseEffectivenessComputed(
        PokemonInstance atk, PokemonInstance def, ElementType moveType, float multiplier)
        => EffectivenessComputed?.Invoke(atk, def, moveType, multiplier);

    public static void RaiseCaptureShakes(int shakes, float chance, bool success)
        => CaptureShakesComputed?.Invoke(shakes, chance, success);
}
