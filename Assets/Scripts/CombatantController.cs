using UnityEngine;

public class CombatantController : MonoBehaviour
{
    public PokemonInstance Model { get; private set; }
    public bool IsPlayer { get; private set; }

    // Transform REAL del Pokémon en mundo
    private Transform worldTf;

    public bool IsFainted => Model != null && Model.currentHP <= 0;

    public void Init(Transform worldTransform, PokemonInstance model, bool isPlayer)
    {
        worldTf = worldTransform;
        Model = model;
        IsPlayer = isPlayer;
    }

    // Posición del actor (si no hay worldTf por alguna razón, usa el propio transform)
    public Vector3 Position
    {
        get => worldTf ? worldTf.position : transform.position;
        set
        {
            if (worldTf) worldTf.position = value;
            else transform.position = value;
        }
    }

    public Quaternion Rotation
    {
        get => worldTf ? worldTf.rotation : transform.rotation;
        set
        {
            if (worldTf) worldTf.rotation = value;
            else transform.rotation = value;
        }
    }

    public void TeleportTo(Vector3 position, Vector3 lookAt)
    {
        Position = position;
        Face(lookAt);
    }

    public void Face(Vector3 lookAtPoint)
    {
        var dir = (lookAtPoint - Position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    public void MoveTowardsPosition(Vector3 target, float step)
    {
        var p = Position;
        p = Vector3.MoveTowards(p, target, step);
        Position = p;
    }

    public void ApplyDamage(int amount)
    {
        if (Model == null) return;
        Model.currentHP = Mathf.Clamp(Model.currentHP - Mathf.Max(0, amount), 0, Model.stats.MaxHP);
        // TODO: animación de daño, retroceso, etc.
    }

    public void Heal(int amount)
    {
        if (Model == null) return;
        Model.currentHP = Mathf.Clamp(Model.currentHP + Mathf.Max(0, amount), 0, Model.stats.MaxHP);
    }

    public void CleanupAfterBattle()
    {
        // TODO: limpiar estados/etapas temporales si procede
    }
}
