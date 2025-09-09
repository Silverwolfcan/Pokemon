// Combate/StatusContainer.cs
using System;
using UnityEngine;
using static StatusService;

/// Contenedor de estado por combatiente. No modifica HP directamente:
/// emite eventos para que TurnController/EncounterController apliquen daño y refresquen HUD.
/// Logs: [Status], [Turn]
[DisallowMultipleComponent]
public class StatusContainer : MonoBehaviour
{
    [Header("Referencia al modelo del combatiente")]
    [SerializeField] private PokemonInstance _pokemon;
    [SerializeField] private bool _isPlayer;

    [Header("Estado primario")]
    [SerializeField] private PrimaryStatus _primary = PrimaryStatus.None;
    [SerializeField] private int _sleepTurns;   // turnos restantes si Sleep
    [SerializeField] private bool _frozen;      // bandera de congelado activo

    [Header("Estados volátiles")]
    [SerializeField] private int _confusionTurns;

    // Eventos
    public event Action<PrimaryStatus> OnPrimaryChanged;
    public event Action<int> OnConfusionChanged;
    public event Action<StatusBlockReason> OnActionBlocked;
    public event Action<int, string> OnResidualDamageRequested;   // amount, tag
    public event Action<int, string> OnConfusionSelfHitRequested; // amount, tag

    // Lecturas
    public PrimaryStatus Primary => _primary;
    public bool IsConfused => _confusionTurns > 0;
    public bool IsAsleep => _primary == PrimaryStatus.Sleep && _sleepTurns > 0;
    public bool IsParalyzed => _primary == PrimaryStatus.Paralysis;
    public bool IsBurned => _primary == PrimaryStatus.Burn;
    public bool IsPoisoned => _primary == PrimaryStatus.Poison;
    public bool IsFrozen => _primary == PrimaryStatus.Freeze && _frozen;

    public void Initialize(PokemonInstance pokemon, bool isPlayer)
    {
        _pokemon = pokemon;
        _isPlayer = isPlayer;
        NotifyAll();
    }

    // Aplicación / cura
    public bool TryApplyPrimary(PrimaryStatus status)
    {
        if (status == PrimaryStatus.None) return false;
        if (_primary != PrimaryStatus.None)
        {
            Debug.Log($"[Status] Ya tenía {_primary}. Ignoro nuevo {status} en {NameForLog()}.");
            return false;
        }

        _primary = status;
        switch (status)
        {
            case PrimaryStatus.Sleep:
                _sleepTurns = Mathf.Max(0, StatusService.RollSleepTurns());
                _frozen = false;
                break;
            case PrimaryStatus.Freeze:
                _frozen = true;
                _sleepTurns = 0;
                break;
            default:
                _sleepTurns = 0;
                _frozen = false;
                break;
        }

        Debug.Log($"[Status] {NameForLog()} -> {_primary}.");
        OnPrimaryChanged?.Invoke(_primary);
        return true;
    }

    public void CurePrimary()
    {
        if (_primary == PrimaryStatus.None) return;
        Debug.Log($"[Status] Cura {_primary} en {NameForLog()}.");
        _primary = PrimaryStatus.None;
        _sleepTurns = 0;
        _frozen = false;
        OnPrimaryChanged?.Invoke(_primary);
    }

    public void ApplyConfusion(int turns = -1)
    {
        if (turns <= 0) turns = StatusService.RollConfusionTurns();
        _confusionTurns = turns;
        Debug.Log($"[Status] Confusión en {NameForLog()} por {_confusionTurns} turnos.");
        OnConfusionChanged?.Invoke(_confusionTurns);
    }

    public void CureConfusion()
    {
        if (_confusionTurns <= 0) return;
        _confusionTurns = 0;
        Debug.Log($"[Status] Confusión curada en {NameForLog()}.");
        OnConfusionChanged?.Invoke(_confusionTurns);
    }

    // Hooks de turno
    public bool OnBeforeAction()
    {
        // Sueño
        if (IsAsleep)
        {
            _sleepTurns = Mathf.Max(0, _sleepTurns - 1);
            Debug.Log($"[Turn] {NameForLog()} está dormido. Restan {_sleepTurns} turnos.");
            OnActionBlocked?.Invoke(StatusBlockReason.Sleep);
            if (_sleepTurns == 0)
            {
                Debug.Log($"[Status] {NameForLog()} despertó.");
                _primary = PrimaryStatus.None;
                OnPrimaryChanged?.Invoke(_primary);
            }
            return false;
        }

        // Congelado
        if (IsFrozen)
        {
            if (StatusService.RollThawThisTurn())
            {
                Debug.Log($"[Status] {NameForLog()} se descongeló.");
                _frozen = false;
                _primary = PrimaryStatus.None;
                OnPrimaryChanged?.Invoke(_primary);
            }
            else
            {
                Debug.Log($"[Turn] {NameForLog()} no actúa por congelación.");
                OnActionBlocked?.Invoke(StatusBlockReason.Freeze);
                return false;
            }
        }

        // Parálisis
        if (IsParalyzed && StatusService.RollParalysisBlock())
        {
            Debug.Log($"[Turn] {NameForLog()} no actúa por parálisis.");
            OnActionBlocked?.Invoke(StatusBlockReason.Paralysis);
            return false;
        }

        // Confusión: auto-golpe 1/3
        if (IsConfused && UnityEngine.Random.value < (1f / 3f))
        {
            int dmg = ComputeConfusionSelfHitDamage();
            Debug.Log($"[Status] {NameForLog()} se golpea a sí mismo por {dmg}.");
            OnConfusionSelfHitRequested?.Invoke(dmg, "[Status]");
            ConsumeConfusionTurn();
            return false;
        }

        return true;
    }

    public void OnEndOfTurn()
    {
        // DOT Burn/Poison
        float frac = StatusService.GetDotFraction(_primary);
        if (frac > 0f && _pokemon != null && _pokemon.stats.MaxHP > 0)
        {
            int amount = Mathf.Max(1, Mathf.FloorToInt(_pokemon.stats.MaxHP * frac));
            Debug.Log($"[Status] DOT {_primary} solicita {amount} de daño.");
            OnResidualDamageRequested?.Invoke(amount, "[Status]");
        }

        // Confusión: consumir si no se consumió antes
        if (IsConfused) ConsumeConfusionTurn();
    }

    // Deltas persistentes
    public int GetAttackStageDeltaByStatus() => StatusService.GetPersistentStageDelta(_primary, StatusService.StatTarget.PhysicalAttack);
    public int GetSpeedStageDeltaByStatus() => StatusService.GetPersistentStageDelta(_primary, StatusService.StatTarget.Speed);

    // Auto-daño por confusión (fórmula acordada)
    private int ComputeConfusionSelfHitDamage()
    {
        if (_pokemon == null) return 1;

        int N = Mathf.Max(1, _pokemon.level);
        int P = Mathf.Max(1, StatusService.GetConfusionSelfPower());

        int A = BattleStageService.GetModifiedStat(_pokemon, _pokemon.stats.Attack, BattleStageService.StatKind.Attack);
        int D = BattleStageService.GetModifiedStat(_pokemon, _pokemon.stats.Defense, BattleStageService.StatKind.Defense);

        // Penalización por quemadura al Ataque físico del propio objetivo
        A = Mathf.Max(1, Mathf.FloorToInt(A * StatusService.GetAttackMulByStatus(_primary)));

        int V = StatusService.RollVariancePercent();

        float inner = (((0.2f * N) + 1f) * A * P) / (25f * Mathf.Max(1, D)) + 2f;
        float dmgF = 0.01f * V * inner;
        return Mathf.Max(1, Mathf.FloorToInt(dmgF));
    }

    private void ConsumeConfusionTurn()
    {
        _confusionTurns = Mathf.Max(0, _confusionTurns - 1);
        OnConfusionChanged?.Invoke(_confusionTurns);
        if (_confusionTurns == 0) Debug.Log($"[Status] {NameForLog()} ya no está confundido.");
    }

    private string NameForLog() => _pokemon != null ? _pokemon.DisplayName : name;

    private void NotifyAll()
    {
        OnPrimaryChanged?.Invoke(_primary);
        OnConfusionChanged?.Invoke(_confusionTurns);
    }
}

/*
Asignaciones en el Inspector:
- En cada Combatant (Player y Enemy):
  - Pokemon Instance: la instancia usada por CombatantController.
  - Is Player: marcar si es del jugador.
- Suscripciones: daño residual, auto-daño, bloqueos y cambios para HUD.
*/
