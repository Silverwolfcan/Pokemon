using UnityEngine;

/// Solo ticks de estado al inicio y preparación del turno.
/// El bloqueo (parálisis/sueño/congelado/flinch) se resuelve en MoveExecutor.CheckAccuracy.
[RequireComponent(typeof(TurnController))]
public class StatusTurnHooks : MonoBehaviour
{
    private TurnController turn;

    private void OnEnable()
    {
        turn = GetComponent<TurnController>();
        if (!turn) return;
        turn.OnPlayerTurnStart += HandlePlayerStart;
        turn.OnEnemyTurnStart += HandleEnemyStart;
    }

    private void OnDisable()
    {
        if (!turn) return;
        turn.OnPlayerTurnStart -= HandlePlayerStart;
        turn.OnEnemyTurnStart -= HandleEnemyStart;
    }

    private void HandlePlayerStart()
    {
        var c = FindCombatant(true); if (c == null) return;
        BattleVolatileService.StartTurn(c.Model);
        PokemonStatusService.ApplyStartOfTurnTicks(c.Model);
    }

    private void HandleEnemyStart()
    {
        var c = FindCombatant(false); if (c == null) return;
        BattleVolatileService.StartTurn(c.Model);
        PokemonStatusService.ApplyStartOfTurnTicks(c.Model);
    }

    private CombatantController FindCombatant(bool isPlayer)
    {
        var list = UnityEngine.Object.FindObjectsByType<CombatantController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].IsPlayer == isPlayer) return list[i];
        return null;
    }
}
