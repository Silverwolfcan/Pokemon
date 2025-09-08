using UnityEngine;

/// Sincroniza CombatService → CombatStateService y emite eventos de encuentro.
/// No modifica CombatService ni EncounterController. Poll ligero y seguro.
public sealed class CombatStateBridge : MonoBehaviour
{
    [Tooltip("Frecuencia de comprobación en segundos.")]
    [Min(0.02f)] public float pollInterval = 0.2f;

    float _timer;
    bool _last;

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < pollInterval) return;
        _timer = 0f;

        bool inEncounter = false;
        var cs = CombatService.Instance;
        if (cs != null) inEncounter = cs.IsInEncounter;

        if (inEncounter == _last) return;
        _last = inEncounter;

        var svc = ServiceLocator.Get<CombatStateService>();
        if (svc != null) svc.SetActive(inEncounter);
        else GameEventBus.RaiseEncounterStateChanged(inEncounter);
    }
}
