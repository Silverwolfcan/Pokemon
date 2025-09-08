using System;

public class CombatStateService
{
    public bool IsActive { get; private set; }
    public event Action<bool> OnChanged;

    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        OnChanged?.Invoke(active);
        GameEventBus.RaiseEncounterStateChanged(active);
        Logger.Encounter(active ? "Combat state = ACTIVE" : "Combat state = INACTIVE");
    }
}
