// Combate/CombatantController.cs
using System.Collections;
using UnityEngine;

/// Controla el avatar del combatiente y provee API usada por EncounterController.
public class CombatantController : MonoBehaviour
{
    public PokemonInstance Model { get; private set; }
    public bool IsPlayer { get; private set; }

    [SerializeField] private Transform worldTransform;

    // Estado básico
    public bool IsFainted => Model == null || Model.currentHP <= 0;

    // Posición expuesta con getter/setter
    public Vector3 Position
    {
        get { return worldTransform ? worldTransform.position : transform.position; }
        set
        {
            if (worldTransform) worldTransform.position = value;
            else transform.position = value;
        }
    }

    public Transform WorldTransform => worldTransform ? worldTransform : transform;

    // ---- Setup ----
    public void Init(Transform tf, PokemonInstance model, bool isPlayer)
    {
        worldTransform = tf ? tf : transform;
        Model = model;
        IsPlayer = isPlayer;
    }

    public void SwitchIn(PokemonInstance newModel)
    {
        Model = newModel;
        // Aquí refrescar VFX/anim si procede.
    }

    public void Face(Vector3 lookAtWorldPos)
    {
        var tf = WorldTransform;
        Vector3 dir = lookAtWorldPos - tf.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f)
            tf.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    /// Movimiento incremental usado por EncounterController.KeepOnRing
    public void MoveTowardsPosition(Vector3 target, float step)
    {
        var tf = WorldTransform;
        tf.position = Vector3.MoveTowards(tf.position, target, Mathf.Max(0f, step));
    }

    /// Limpieza post-combate (stubs para compatibilidad)
    public void CleanupAfterBattle()
    {
        // Añade resets de animaciones/VFX si los usas.
    }
}
