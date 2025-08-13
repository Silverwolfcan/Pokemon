using System.Collections;
using UnityEngine;
using System;

public class TurnController : MonoBehaviour
{
    private CombatantController player, enemy;

    public event Action OnPlayerTurnStart; // engancha aquí tu UI para abrir menú
    public event Action OnEnemyTurnStart;

    // Para que la UI devuelva la acción elegida:
    private int? queuedPlayerMoveIndex = null;
    private bool queuedRun = false;
    private bool queuedCapture = false;
    private PokemonInstance queuedSwitchIn = null;

    public void Setup(CombatantController player, CombatantController enemy)
    {
        this.player = player;
        this.enemy = enemy;
    }

    // ---------- Player ----------
    public IEnumerator DoPlayerTurn(Vector3 ringCenter, float offset)
    {
        OnPlayerTurnStart?.Invoke();

        // Esperar decisión (UI debe llamar a una de estas: QueueMove, QueueRun, QueueCapture, QueueSwitch)
        yield return new WaitUntil(() =>
            queuedPlayerMoveIndex.HasValue || queuedRun || queuedCapture || queuedSwitchIn != null);

        if (queuedRun)
        {
            // TODO: confirmar huida si procede (probabilidad si quieres), por ahora siempre exitosa
            CombatEvents.RaiseEncounterEnded(EncounterResult.Run);
            ClearQueued();
            yield break;
        }

        if (queuedCapture)
        {
            // TODO: lanzar pokéball: aquí puedes pausar, esperar resultado y terminar combate si capturas
            // Por ahora solo consume turno sin efecto
            yield return new WaitForSeconds(0.3f);
            ClearQueued();
            yield break;
        }

        if (queuedSwitchIn != null)
        {
            // TODO: animación de cambio. Debes intercambiar el “activo” de la party con queuedSwitchIn
            // Por ahora solo consume turno
            yield return new WaitForSeconds(0.3f);
            queuedSwitchIn = null;
            ClearQueued();
            yield break;
        }

        // Ejecutar movimiento del jugador
        int moveIndex = queuedPlayerMoveIndex.Value;
        ClearQueued();

        yield return ExecuteMove(player, enemy, moveIndex);
    }

    // ---------- Enemy ----------
    public IEnumerator DoEnemyTurn(Vector3 ringCenter, float offset)
    {
        OnEnemyTurnStart?.Invoke();

        // IA simple: elige primer movimiento válido con PP
        int enemyMove = WildAI.ChooseMoveIndex(enemy.Model);
        yield return ExecuteMove(enemy, player, enemyMove);
    }

    // ---------- Exec ----------
    private IEnumerator ExecuteMove(CombatantController attacker, CombatantController defender, int moveIndex)
    {
        var mv = attacker.Model != null && attacker.Model.Moves.Count > moveIndex
               ? attacker.Model.Moves[moveIndex]
               : null;

        if (mv == null || mv.data == null)
        {
            // Falla/espera si movimiento vacío
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        if (!mv.TryConsumePP(1))
        {
            // Sin PP: falla
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        // Accuracy
        if (!MoveExecutor.CheckAccuracy(attacker.Model, defender.Model, mv.data.accuracy))
        {
            // Fallo
            // TODO: animación de fallo
            yield return new WaitForSeconds(0.35f);
            yield break;
        }

        // Daño
        int damage = MoveExecutor.ComputeDamage(attacker.Model, defender.Model, mv.data);
        defender.ApplyDamage(damage);

        // TODO: anim/FX. Pequeño knockback opcional…
        yield return new WaitForSeconds(0.45f);
    }

    // ---------- UI Hooks ----------
    public void QueueMove(int moveIndex) { queuedPlayerMoveIndex = moveIndex; }
    public void QueueRun() { queuedRun = true; }
    public void QueueCapture() { queuedCapture = true; }
    public void QueueSwitch(PokemonInstance switchIn) { queuedSwitchIn = switchIn; }

    private void ClearQueued()
    {
        queuedPlayerMoveIndex = null;
        queuedRun = false;
        queuedCapture = false;
        queuedSwitchIn = null;
    }
}

public static class CombatEvents
{
    public static Action<EncounterResult> OnEncounterEnded;

    public static void RaiseEncounterEnded(EncounterResult r) => OnEncounterEnded?.Invoke(r);
}
