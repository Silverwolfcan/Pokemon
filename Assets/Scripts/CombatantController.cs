using UnityEngine;

public class CombatantController : MonoBehaviour
{
    public PokemonInstance Model { get; private set; }
    public bool IsPlayer { get; private set; }
    public bool IsFainted => Model != null && Model.currentHP <= 0;

    private Transform worldTf;

    public void Init(Transform worldTransform, PokemonInstance model, bool isPlayer)
    {
        worldTf = worldTransform;
        Model = model;
        IsPlayer = isPlayer;
    }

    public void TeleportTo(Vector3 position, Vector3 lookAt)
    {
        if (!worldTf) return;
        worldTf.position = position;
        Face(lookAt);
    }

    public void Face(Vector3 lookAtPoint)
    {
        if (!worldTf) return;
        var dir = (lookAtPoint - worldTf.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        worldTf.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    public void ApplyDamage(int amount)
    {
        if (Model == null) return;
        Model.currentHP = Mathf.Clamp(Model.currentHP - Mathf.Max(0, amount), 0, Model.stats.MaxHP);
        // TODO: animaciones de daño, retroceso físico leve si quieres
    }

    public void Heal(int amount)
    {
        if (Model == null) return;
        Model.currentHP = Mathf.Clamp(Model.currentHP + Mathf.Max(0, amount), 0, Model.stats.MaxHP);
    }

    public void CleanupAfterBattle()
    {
        // Quita stages temporales, estados con duración de combate, etc. (si los implementas)
        // Por ahora, no hace nada.
    }
}
