using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class TurnController : MonoBehaviour
{
    private CombatantController player;
    private CombatantController enemy;

    private StatusContainer playerStatus;
    private StatusContainer enemyStatus;

    public event Action OnPlayerTurnStart;
    public event Action OnEnemyTurnStart;
    public event Action OnPlayerFaintedRequireSwitch;

    public bool IsResolving { get; private set; }
    public bool IsWaitingForPlayerInput { get; private set; }
    public bool ForceSwitchPending { get; private set; }

    private int? queuedMoveIndex;
    private bool queuedRun;
    private (ItemData item, PokemonInstance target)? queuedUseItem;
    private PokemonInstance queuedSwitchIn;
    private bool queuedCapture;

    private bool playerFlinchNext;
    private bool enemyFlinchNext;

    public void Setup(CombatantController playerCbt, CombatantController enemyCbt)
    {
        player = playerCbt;
        enemy = enemyCbt;

        playerStatus = player?.WorldTransform ? player.WorldTransform.GetComponent<StatusContainer>() : null;
        enemyStatus = enemy?.WorldTransform ? enemy.WorldTransform.GetComponent<StatusContainer>() : null;

        ClearQueue();
        IsResolving = false;
        IsWaitingForPlayerInput = false;
        ForceSwitchPending = false;

        BattleStageService.ResetForAll(player?.Model, enemy?.Model);
        playerFlinchNext = false;
        enemyFlinchNext = false;
    }

    public void QueueMove(int moveIndex) { queuedMoveIndex = moveIndex; }
    public void QueueRun() { queuedRun = true; }
    public void QueueUseItem(ItemData item, PokemonInstance tgt) { queuedUseItem = (item, tgt); }
    public void QueueSwitch(PokemonInstance replacement)
    {
        if (replacement == null || replacement.currentHP <= 0) return;
        if (player?.Model != null && ReferenceEquals(player.Model, replacement)) return;
        queuedSwitchIn = replacement;
    }
    public void QueueCapture() { queuedCapture = true; }
    public void CancelQueuedAction() { ClearQueue(); }

    // Forzado externo (KO del jugador)
    public void RequestForcedPlayerSwitch()
    {
        if (ForceSwitchPending) return;
        ForceSwitchPending = true;
        OnPlayerFaintedRequireSwitch?.Invoke();
    }

    // NUEVO: ejecutar inmediatamente el cambio forzoso
    public void ForceSwitchImmediately(PokemonInstance replacement)
    {
        if (replacement == null || replacement.currentHP <= 0) return;
        if (player == null) return;
        if (player.Model != null && ReferenceEquals(player.Model, replacement)) return;
        StartCoroutine(DoSwitch(player, replacement));
        ClearQueue();
        ForceSwitchPending = false;
    }

    // ----------------- TURNOS -----------------
    public IEnumerator DoPlayerTurn(Vector3 ringCenter, float combatantOffsetFromCenter)
    {
        IsResolving = true; OnPlayerTurnStart?.Invoke();

        if (playerFlinchNext) { playerFlinchNext = false; playerStatus?.OnEndOfTurn(); IsResolving = false; yield break; }
        if (playerStatus != null && !playerStatus.OnBeforeAction())
        {
            yield return new WaitForSeconds(0.2f);
            playerStatus.OnEndOfTurn();
            PostEoTPlayerFaintCheck();
            IsResolving = false;
            yield break;
        }

        IsResolving = false; IsWaitingForPlayerInput = true;
        while (!HasQueuedAction()) yield return null;
        IsWaitingForPlayerInput = false; IsResolving = true;

        if (queuedCapture)
        {
            yield return new WaitForSeconds(0.2f);
            ClearQueue();
            playerStatus?.OnEndOfTurn();
            PostEoTPlayerFaintCheck();
            IsResolving = false;
            yield break;
        }

        if (queuedRun)
        {
            yield return DoRun();
            ClearQueue();
            playerStatus?.OnEndOfTurn();
            PostEoTPlayerFaintCheck();
            IsResolving = false;
            yield break;
        }

        if (queuedSwitchIn != null)
        {
            yield return DoSwitch(player, queuedSwitchIn);
            ClearQueue();
            playerStatus?.OnEndOfTurn();
            IsResolving = false;
            yield break;
        }

        if (queuedUseItem.HasValue)
        {
            var (item, tgt) = queuedUseItem.Value;
            yield return DoUseItem(item, tgt, true);
            ClearQueue();
            playerStatus?.OnEndOfTurn();
            PostEoTPlayerFaintCheck();
            IsResolving = false;
            yield break;
        }

        if (queuedMoveIndex.HasValue)
        {
            yield return ExecuteMove(player, enemy, queuedMoveIndex.Value, true);
            ClearQueue();
        }

        playerStatus?.OnEndOfTurn();
        PostEoTPlayerFaintCheck();
        IsResolving = false;
    }

    public IEnumerator DoEnemyTurn(Vector3 ringCenter, float combatantOffsetFromCenter)
    {
        IsResolving = true; OnEnemyTurnStart?.Invoke();

        if (enemyFlinchNext) { enemyFlinchNext = false; enemyStatus?.OnEndOfTurn(); IsResolving = false; yield break; }
        if (enemyStatus != null && !enemyStatus.OnBeforeAction())
        {
            yield return new WaitForSeconds(0.2f);
            enemyStatus.OnEndOfTurn();
            IsResolving = false;
            yield break;
        }

        int idx = -1;
        if (enemy?.Model != null && player?.Model != null)
            idx = WildAI.ChooseMoveIndex(enemy.Model, player.Model);
        if (idx < 0) idx = FindUsableMoveIndex(enemy?.Model, -1);

        if (idx >= 0) yield return ExecuteMove(enemy, player, idx, false);
        else yield return new WaitForSeconds(0.25f);

        if (player?.Model != null && player.Model.currentHP <= 0)
            RequestForcedPlayerSwitch();

        enemyStatus?.OnEndOfTurn();
        IsResolving = false;
    }

    private void PostEoTPlayerFaintCheck()
    {
        if (player?.Model == null || player.Model.currentHP > 0) return;
        RequestForcedPlayerSwitch();
    }

    // ----------------- ACCIONES -----------------
    private IEnumerator DoRun()
    {
        if (player?.Model == null || enemy?.Model == null) yield break;

        int spdP = Mathf.Max(1, BattleStageService.GetModifiedStat(player.Model, player.Model.stats.Speed, BattleStageService.StatKind.Speed));
        int spdE = Mathf.Max(1, BattleStageService.GetModifiedStat(enemy.Model, enemy.Model.stats.Speed, BattleStageService.StatKind.Speed));
        if (playerStatus != null) spdP = Mathf.Max(1, Mathf.FloorToInt(spdP * StatusService.GetSpeedMulByStatus(playerStatus.Primary)));
        if (enemyStatus != null) spdE = Mathf.Max(1, Mathf.FloorToInt(spdE * StatusService.GetSpeedMulByStatus(enemyStatus.Primary)));

        float chance = 0.5f + (spdP > spdE ? 0.25f : 0f);
        bool success = UnityEngine.Random.value <= Mathf.Clamp01(chance);
        yield return new WaitForSeconds(0.2f);
        if (success) CombatService.Instance?.ForceEndEncounter();
    }

    private IEnumerator DoSwitch(CombatantController who, PokemonInstance replacement)
    {
        if (who == null || replacement == null || replacement.currentHP <= 0) yield break;

        who.SwitchIn(replacement);
        RebindHUDFor(who);

        if (who == player) ForceSwitchPending = false;

        yield return new WaitForSeconds(0.2f);
    }

    private void RebindHUDFor(CombatantController who)
    {
        if (who == null || who.WorldTransform == null) return;
        var huds = FindObjectsByType<CombatantHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            var h = huds[i];
            if (h != null && h.Anchor == who.WorldTransform)
            {
                h.ForceRebind(who.Model);
                break;
            }
        }
    }

    private IEnumerator DoUseItem(ItemData item, PokemonInstance target, bool usedByPlayer)
    {
        if (item == null) yield break;

        var who = usedByPlayer ? player : enemy;
        var model = (target != null) ? target : who?.Model;
        if (model == null) yield break;

        bool applied = false;

        if (item is HealingItemData heal)
        {
            if (!heal.usableInBattle) yield break;

            int mvIndex = -1;
            if (heal.effect == HealingEffectType.RestorePPSingleFixed ||
                heal.effect == HealingEffectType.RestorePPSingleFull)
            {
                mvIndex = FindFirstMoveNeedingPP(model);
                if (mvIndex < 0) yield break;
            }

            var r = ItemEffectsUtility.ApplyHealingItem(model, heal, mvIndex);
            applied = r == ItemUseResult.Applied;
        }

        if (applied) TryConsumeFromInventory(item, 1);

        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator ExecuteMove(CombatantController attacker, CombatantController defender, int moveIndex, bool isPlayer)
    {
        var atkMon = attacker?.Model; var defMon = defender?.Model;
        if (atkMon == null || defMon == null) yield break;

        var moves = atkMon.Moves; if (moveIndex < 0 || moveIndex >= moves.Count) yield break;
        var inst = moves[moveIndex]; var data = inst?.data; if (data == null) yield break;

        if (!inst.TryConsumePP(1)) yield break;

        bool hit = MoveExecutor.CheckAccuracy(atkMon, defMon, data.accuracy);

        if (hit)
        {
            var res = MoveExecutor.ResolveMove(atkMon, defMon, data);

            if (res.totalDamage > 0) defMon.currentHP = Mathf.Max(0, defMon.currentHP - res.totalDamage);
            if (res.drainHeal > 0) atkMon.currentHP = Mathf.Min(atkMon.stats.MaxHP, atkMon.currentHP + res.drainHeal);
            if (res.recoilDamage > 0) atkMon.currentHP = Mathf.Max(0, atkMon.currentHP - res.recoilDamage);

            if (res.targetFlinched)
            {
                if (attacker == player) enemyFlinchNext = true;
                else playerFlinchNext = true;
            }
        }

        yield return new WaitForSeconds(0.35f);
    }

    // ----------------- UTIL -----------------
    private bool HasQueuedAction()
        => queuedRun || queuedMoveIndex.HasValue || queuedUseItem.HasValue || queuedSwitchIn != null || queuedCapture;

    private int FindUsableMoveIndex(PokemonInstance mon, int preferredIndex)
    {
        if (mon == null) return -1;
        var list = mon.Moves;
        if (preferredIndex >= 0 && preferredIndex < list.Count && list[preferredIndex] != null && list[preferredIndex].currentPP > 0) return preferredIndex;
        for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].currentPP > 0) return i;
        return -1;
    }

    private int FindFirstMoveNeedingPP(PokemonInstance p)
    {
        if (p == null || p.Moves == null) return -1;
        for (int i = 0; i < p.Moves.Count; i++)
        {
            var mv = p.Moves[i];
            if (mv != null && mv.data != null && mv.currentPP < mv.maxPP) return i;
        }
        return -1;
    }

    private void TryConsumeFromInventory(ItemData item, int count)
    {
        try
        {
            var invType = Type.GetType("InventoryManager");
            if (invType == null) return;
            var instProp = invType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var inst = instProp?.GetValue(null);
            if (inst == null) return;

            var methods = inst.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var m in methods)
            {
                if (!m.Name.Contains("Remove") && !m.Name.Contains("Consume") && !m.Name.Contains("Use")) continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType.IsAssignableFrom(item.GetType()) && ps[1].ParameterType == typeof(int))
                { m.Invoke(inst, new object[] { item, count }); return; }
                if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(item.GetType()))
                { m.Invoke(inst, new object[] { item }); return; }
            }
        }
        catch { }
    }

    private void ClearQueue()
    {
        queuedMoveIndex = null;
        queuedRun = false;
        queuedUseItem = null;
        queuedSwitchIn = null;
        queuedCapture = false;
    }
}
