// Servicios/StatusContainer.cs
using System;
using UnityEngine;

public class StatusContainer : MonoBehaviour
{
    [SerializeField] private PokemonInstance _pokemon;
    [SerializeField] private StatusService.PrimaryStatus _primary = StatusService.PrimaryStatus.None;
    [SerializeField] private bool _isPlayer;

    // Eventos
    public event Action<int, string> OnResidualDamageRequested;
    public event Action<int, string> OnConfusionSelfHitRequested;
    public event Action<StatusService.PrimaryStatus> OnPrimaryChanged; // FIX

    // Expuestos
    public PokemonInstance Pokemon => _pokemon;
    public StatusService.PrimaryStatus Primary => _primary;

    // Internos
    private int sleepTurns;
    private bool confused;
    private int confuseTurns;
    private CombatantController owner;
    private bool? isAllyCached;

    private void Awake()
    {
        owner = GetComponentInParent<CombatantController>();
        if (_pokemon == null && owner) _pokemon = owner.Model;
    }

    public void Initialize(PokemonInstance pokemon, bool isPlayer)
    {
        _pokemon = pokemon;
        _isPlayer = isPlayer;
        isAllyCached = isPlayer;
    }

    public void EnableLogCallbacks(bool enable) { }

    private bool IsAlly()
    {
        if (isAllyCached.HasValue) return isAllyCached.Value;
        bool isAlly = _isPlayer;
        try
        {
            var t = owner ? owner.GetType() : null;
            var pi = t?.GetProperty("IsPlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (pi != null) isAlly = (bool)pi.GetValue(owner);
        }
        catch { }
        isAllyCached = isAlly;
        return isAlly;
    }

    // Turno: antes de actuar
    public bool OnBeforeAction()
    {
        if (_pokemon == null) return true;

        if (_primary == StatusService.PrimaryStatus.Sleep)
        {
            if (sleepTurns > 0) sleepTurns--;
            if (sleepTurns > 0)
            {
                CombatLogPanel.LogStatus(_pokemon, "Sueño", true, IsAlly(), 0);
                return false;
            }
            _primary = StatusService.PrimaryStatus.None;
            OnPrimaryChanged?.Invoke(_primary); // FIX: notificar
            CombatLogPanel.LogStatus(_pokemon, "Despierta", true, IsAlly(), 0);
        }

        if (_primary == StatusService.PrimaryStatus.Paralysis)
        {
            if (UnityEngine.Random.value < 0.25f)
            {
                CombatLogPanel.LogStatus(_pokemon, "Parálisis", true, IsAlly(), 0);
                return false;
            }
        }

        if (_primary == StatusService.PrimaryStatus.Freeze)
        {
            if (UnityEngine.Random.value < 0.2f)
            {
                _primary = StatusService.PrimaryStatus.None;
                OnPrimaryChanged?.Invoke(_primary); // FIX
                CombatLogPanel.LogStatus(_pokemon, "Se descongela", true, IsAlly(), 0);
            }
            else
            {
                CombatLogPanel.LogStatus(_pokemon, "Congelado", true, IsAlly(), 0);
                return false;
            }
        }

        if (confused)
        {
            if (confuseTurns > 0) confuseTurns--;
            if (UnityEngine.Random.value < (1f / 3f))
            {
                int dmg = ComputeConfusionSelfHitDamage();
                CombatLogPanel.LogStatus(_pokemon, "Confusión (se hiere)", true, IsAlly(), dmg);
                OnConfusionSelfHitRequested?.Invoke(dmg, "[Status]");
                return false;
            }
            if (confuseTurns <= 0)
            {
                confused = false;
                CombatLogPanel.LogStatus(_pokemon, "Confusión termina", true, IsAlly(), 0);
            }
        }

        return true;
    }

    // Turno: fin de turno
    public void OnEndOfTurn()
    {
        if (_pokemon == null) return;

        float frac = StatusService.GetDotFraction(_primary);
        if (frac > 0f && _pokemon.stats.MaxHP > 0 && _pokemon.currentHP > 0)
        {
            int amount = Mathf.Max(1, Mathf.FloorToInt(_pokemon.stats.MaxHP * frac));
            string label = _primary == StatusService.PrimaryStatus.Burn ? "Quemadura"
                          : _primary == StatusService.PrimaryStatus.Poison ? "Veneno"
                          : "Estado";

            CombatLogPanel.LogStatus(_pokemon, label, true, IsAlly(), amount);
            OnResidualDamageRequested?.Invoke(amount, "[Status]");
        }
    }

    // Aplicar estado primario
    public bool TryApplyPrimary(StatusService.PrimaryStatus s)
    {
        if (_pokemon == null) return false;
        if (s == StatusService.PrimaryStatus.None) return false;

        if (_primary != StatusService.PrimaryStatus.None)
        {
            CombatLogPanel.LogStatus(_pokemon, s.ToString(), false, IsAlly(), 0);
            return false;
        }

        _primary = s;
        OnPrimaryChanged?.Invoke(_primary); // FIX

        switch (s)
        {
            case StatusService.PrimaryStatus.Sleep:
                sleepTurns = UnityEngine.Random.Range(1, 4);
                break;
            default:
                break;
        }

        CombatLogPanel.LogStatus(_pokemon, s.ToString(), true, IsAlly(), 0);
        return true;
    }

    public void ClearPrimary()
    {
        _primary = StatusService.PrimaryStatus.None;
        OnPrimaryChanged?.Invoke(_primary); // FIX
    }

    public void ApplyConfusion(int turns = 2)
    {
        confused = true;
        confuseTurns = Mathf.Max(1, turns);
        CombatLogPanel.LogStatus(_pokemon, "Confusión", true, IsAlly(), 0);
    }

    private int ComputeConfusionSelfHitDamage()
    {
        if (_pokemon == null) return 1;
        int N = Mathf.Max(1, _pokemon.level);
        int A = Mathf.Max(1, _pokemon.stats.Attack);
        int D = Mathf.Max(1, _pokemon.stats.Defense);
        int P = 40;
        float baseDmg = Mathf.Floor(0.01f * (((0.2f * N + 1f) * A * P) / (25f * D) + 2f));
        int V = UnityEngine.Random.Range(85, 101);
        return Mathf.Max(1, Mathf.FloorToInt(baseDmg * (V / 100f)));
    }
}
