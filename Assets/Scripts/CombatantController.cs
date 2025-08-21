using UnityEngine;

/// Controla a un combatiente durante el combate operando sobre su Transform real.
/// NO instancia ni mueve un clon: siempre usa el Transform del mundo que se le pasa en Init().
public class CombatantController : MonoBehaviour
{
    public PokemonInstance Model { get; private set; }
    public bool IsPlayer { get; private set; }

    private Transform worldTransform;
    private bool fainted = false;

    // --- Setup ---
    public void Init(Transform tf, PokemonInstance model, bool isPlayer)
    {
        worldTransform = tf;
        Model = model;
        IsPlayer = isPlayer;
        fainted = (Model != null && Model.currentHP <= 0);
    }

    // --- Estado ---
    public bool IsFainted => fainted || (Model != null && Model.currentHP <= 0);

    public Vector3 Position
    {
        get => worldTransform != null ? worldTransform.position : Vector3.zero;
        set { if (worldTransform != null) worldTransform.position = value; }
    }

    // --- Acciones suaves de movimiento/orientación usadas por EncounterController ---
    public void MoveTowardsPosition(Vector3 target, float step)
    {
        if (worldTransform == null || IsFainted) return;
        var pos = worldTransform.position;
        var dir = target - pos;
        dir.y = 0f;
        if (dir.sqrMagnitude <= step * step)
        {
            worldTransform.position = new Vector3(target.x, worldTransform.position.y, target.z);
        }
        else
        {
            dir.Normalize();
            worldTransform.position += new Vector3(dir.x, 0f, dir.z) * step;
        }
    }

    public void Face(Vector3 lookAtWorldPos)
    {
        if (worldTransform == null) return;
        Vector3 dir = lookAtWorldPos - worldTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-5f)
        {
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            worldTransform.rotation = rot;
        }
    }

    /// Aplica daño al modelo y dispara lógica de debilitado si llega a 0.
    public void ApplyDamage(int amount)
    {
        if (Model == null) return;
        if (amount < 0) amount = 0;

        int before = Model.currentHP;
        Model.currentHP = Mathf.Max(0, Model.currentHP - amount);

        if (before > 0 && Model.currentHP == 0)
            OnFainted();
    }

    private void OnFainted()
    {
        if (fainted) return;
        fainted = true;

        if (worldTransform == null) return;

        // Intentar animación simple de "caer" si hay rigidbody; si no, desactivar.
        var rb = worldTransform.GetComponent<Rigidbody>();
        var col = worldTransform.GetComponent<Collider>();

        if (rb != null && !rb.isKinematic)
        {
            rb.AddTorque(worldTransform.right * 5f, ForceMode.Impulse);
        }

        if (col != null) col.enabled = false;

        // Reglas:
        // - Enemigo: desaparecer inmediatamente.
        // - Jugador: si estaba invocado en el mundo, también desaparece (debilitado).
        worldTransform.gameObject.SetActive(false);
    }

    /// Limpieza al finalizar el combate.
    public void CleanupAfterBattle()
    {
        // Nada especial: los behaviours de mundo se restauran desde EncounterController.SetCombatMode(false).
        // Si el pokémon quedó inactivo por debilidad, lo dejamos así (regla de diseño).
    }
}
