using UnityEngine;

/// Controla a un combatiente durante el combate operando sobre su Transform real.
/// NO instancia ni mueve un clon: siempre usa el Transform del mundo que se le pasa en Init().
public class CombatantController : MonoBehaviour
{
    public PokemonInstance Model { get; private set; }
    public bool IsPlayer { get; private set; }

    /// Transform del Pokémon real en escena.
    public Transform WorldTransform => worldTransform;

    public bool IsFainted => Model == null || Model.currentHP <= 0;

    private Transform worldTransform;

    // --- Setup ---
    public void Init(Transform tf, PokemonInstance model, bool isPlayer)
    {
        worldTransform = tf;
        Model = model;
        IsPlayer = isPlayer;
    }

    /// Cambia el Pokémon activo manteniendo el mismo Transform/posición.
    /// No spawnea nada nuevo. Se limita a actualizar el modelo y notificar UI/sistemas.
    public void SetModel(PokemonInstance newModel)
    {
        Model = newModel;

        // Notificar a HUD/observadores de que los datos del Pokémon han cambiado.
        // Este bus ya lo usas en otros puntos del proyecto.
        GameEventBus.RaisePokemonChanged(Model);
        Debug.Log($"[Combatant] {(IsPlayer ? "Jugador" : "Enemigo")} cambió a {Model?.DisplayName ?? "null"}.");
    }

    /// Forzar KO visual/lógico (si lo necesitas en otros flujos).
    public void ForceFaint()
    {
        if (Model == null) return;
        Model.currentHP = 0;
        GameEventBus.RaisePokemonChanged(Model);

        // Reglas de mundo simples: ocultar el GO al debilitarse.
        if (worldTransform != null) worldTransform.gameObject.SetActive(false);
    }

    /// Limpieza al finalizar el combate.
    public void CleanupAfterBattle()
    {
        // Nada especial: los behaviours de mundo se restauran desde EncounterController.SetCombatMode(false).
        // Si el pokémon quedó inactivo por debilidad, lo dejamos así (regla de diseño).
    }
}
