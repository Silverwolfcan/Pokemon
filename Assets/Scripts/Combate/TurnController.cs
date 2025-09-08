using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class TurnController : MonoBehaviour
{
    public event Action OnPlayerTurnStart;
    public event Action OnEnemyTurnStart;

    private CombatantController player;
    private CombatantController enemy;

    private QueuedAction playerAction = QueuedAction.NonePlayer();
    private QueuedAction enemyAction = QueuedAction.NoneEnemy();

    public void Setup(CombatantController playerCbt, CombatantController enemyCbt)
    {
        player = playerCbt;
        enemy = enemyCbt;
    }

    // ---------- API de la UI ----------
    public void QueueMove(int moveIndex)
    {
        if (player == null || player.Model == null) return;
        playerAction = QueuedAction.ForMove(true, moveIndex, GetMovePriority(player.Model, moveIndex));
    }

    public void ConsumePlayerTurn() { playerAction = QueuedAction.ForItem(true); } // “turno consumido externamente”
    public void QueueCapture() { playerAction = QueuedAction.ForCapture(true); }
    public void QueueRun() { playerAction = QueuedAction.ForRun(true); }
    public void QueueSwitch(int partyIndex) { playerAction = QueuedAction.ForSwitch(true, partyIndex); }

    // ---------- Bucle ----------
    public IEnumerator DoPlayerTurn(Vector3 ringCenter, float offsetFromCenter)
    {
        OnPlayerTurnStart?.Invoke();
        SelectEnemyAction();
        yield return new WaitUntil(() => playerAction.type != ActionType.None);
        yield return ExecuteRound();
        playerAction = QueuedAction.NonePlayer();
        enemyAction = QueuedAction.NoneEnemy();
    }

    public IEnumerator DoEnemyTurn(Vector3 ringCenter, float offsetFromCenter)
    {
        OnEnemyTurnStart?.Invoke();
        SelectEnemyAction();
        yield return new WaitUntil(() => playerAction.type != ActionType.None);
        yield return ExecuteRound();
        playerAction = QueuedAction.NonePlayer();
        enemyAction = QueuedAction.NoneEnemy();
    }

    private void SelectEnemyAction()
    {
        if (enemy == null || enemy.Model == null) { enemyAction = QueuedAction.NoneEnemy(); return; }
        int idx = WildAI.ChooseMoveIndex(enemy.Model);
        enemyAction = QueuedAction.ForMove(false, idx, GetMovePriority(enemy.Model, idx));
    }

    private int GetMovePriority(PokemonInstance p, int moveIndex)
    {
        try
        {
            if (p?.Moves == null || moveIndex < 0 || moveIndex >= p.Moves.Count) return 0;
            var md = p.Moves[moveIndex]?.data;
            return md != null ? md.priority : 0;
        }
        catch { return 0; }
    }

    private IEnumerator ExecuteRound()
    {
        bool playerFirst = TurnPriorityResolver.PlayerActsFirst(
            player?.Model, playerAction.priority,
            enemy?.Model, enemyAction.priority);

        if (playerFirst)
        {
            yield return ExecuteActionWithStartTicks(player, enemy, playerAction);
            if (!enemy.IsFainted) yield return ExecuteActionWithStartTicks(enemy, player, enemyAction);
        }
        else
        {
            yield return ExecuteActionWithStartTicks(enemy, player, enemyAction);
            if (!player.IsFainted) yield return ExecuteActionWithStartTicks(player, enemy, playerAction);
        }

        // Fin de turno: ticks residuales
        PokemonStatusService.ApplyEndOfTurnTicks(player?.Model);
        PokemonStatusService.ApplyEndOfTurnTicks(enemy?.Model);
    }

    private IEnumerator ExecuteActionWithStartTicks(CombatantController actor, CombatantController target, QueuedAction act)
    {
        var model = actor?.Model;
        if (model == null) yield break;

        PokemonStatusService.ApplyStartOfTurnTicks(model);
        if (model.currentHP <= 0) yield break;

        yield return ExecuteAction(actor, target, act);
    }

    private IEnumerator ExecuteAction(CombatantController actor, CombatantController target, QueuedAction act)
    {
        if (actor == null || actor.Model == null) yield break;

        switch (act.type)
        {
            case ActionType.Move:
                yield return DoMove(actor, target, act.moveIndex);
                break;

            case ActionType.Item:
                // Consumido externamente (bolas/objetos)
                break;

            case ActionType.Switch:
                {
                    var enc = FindAnyObjectByType<EncounterController>();
                    enc?.TryPlayerSwitch(act.moveIndex);
                    break;
                }

            case ActionType.Capture:
                // El flujo de captura lo inicia la UI → Ball → CombatService → EncounterController.
                // Aquí no hacemos nada; el turno quedará consumido desde Ball/EncounterController.
                break;

            case ActionType.Run:
                {
                    var enc = FindAnyObjectByType<EncounterController>();
                    enc?.TryPlayerRun();
                    break;
                }
        }
    }

    private IEnumerator DoMove(CombatantController atk, CombatantController def, int moveIndex)
    {
        var attacker = atk.Model;
        var defender = def?.Model;
        if (attacker?.Moves == null || moveIndex < 0 || moveIndex >= attacker.Moves.Count) yield break;
        var mi = attacker.Moves[moveIndex];
        if (mi == null || mi.data == null) yield break;

        if (!CanActThisTurn(attacker, out string reason))
        {
            Debug.Log($"[Turn] {attacker.DisplayName} no puede actuar ({reason}).");
            yield break;
        }

        if (mi.currentPP <= 0) yield break;

        mi.currentPP = Mathf.Max(0, mi.currentPP - 1);
        GameEventBus.RaisePokemonChanged(attacker);

        bool hit = MoveExecutor.CheckAccuracy(attacker, defender, mi.data.accuracy);
        if (hit) MoveExecutor.ComputeDamage(attacker, defender, mi.data);
        else Debug.Log($"[Turn] {attacker.DisplayName} falló {mi.data.moveName}.");

        var enc = FindAnyObjectByType<EncounterController>();
        if (def.IsFainted)
        {
            if (def == enemy) enc?.OnEnemyFainted();
            else enc?.OnPlayerFainted();
        }

        yield return null;
    }

    private bool CanActThisTurn(PokemonInstance p, out string reason)
    {
        reason = null;
        var s = PokemonStatusService.GetStatus(p);
        if (s == PrimaryStatus.Sleep) { reason = "Sueño"; return false; }
        if (s == PrimaryStatus.Freeze) { reason = "Congelado"; return false; }
        if (s == PrimaryStatus.Paralysis && UnityEngine.Random.value < 0.25f)
        { reason = "Parálisis"; return false; }
        return true;
    }

    public enum ActionType { None, Move, Item, Switch, Capture, Run }

    public struct QueuedAction
    {
        public ActionType type;
        public bool isPlayer;
        public int moveIndex;
        public int priority;

        public static QueuedAction NonePlayer() => new() { type = ActionType.None, isPlayer = true, moveIndex = -1, priority = 0 };
        public static QueuedAction NoneEnemy() => new() { type = ActionType.None, isPlayer = false, moveIndex = -1, priority = 0 };

        public static QueuedAction ForMove(bool isPlayer, int idx, int prio)
            => new() { type = ActionType.Move, isPlayer = isPlayer, moveIndex = idx, priority = prio };
        public static QueuedAction ForItem(bool isPlayer)
            => new() { type = ActionType.Item, isPlayer = isPlayer, moveIndex = -1, priority = 0 };
        public static QueuedAction ForSwitch(bool isPlayer, int partyIndex)
            => new() { type = ActionType.Switch, isPlayer = isPlayer, moveIndex = partyIndex, priority = 6 };
        public static QueuedAction ForCapture(bool isPlayer)
            => new() { type = ActionType.Capture, isPlayer = isPlayer, moveIndex = -1, priority = 6 };
        public static QueuedAction ForRun(bool isPlayer)
            => new() { type = ActionType.Run, isPlayer = isPlayer, moveIndex = -1, priority = 6 };
    }
}
