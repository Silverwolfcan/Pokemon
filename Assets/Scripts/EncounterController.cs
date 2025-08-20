using System;
using System.Collections;
using UnityEngine;

public class EncounterController : MonoBehaviour
{
    public enum State { Idle, Positioning, PlayerTurn, EnemyTurn, Resolving, Ended }

    [Header("Ring / límites")]
    [SerializeField] private float ringRadiusForPlayer = 10f;
    [SerializeField] private float combatantOffsetFromCenter = 1f;
    [SerializeField] private float repositionSpeed = 12f;
    [SerializeField] private float snapTolerance = 0.05f;

    [Header("Turnos")]
    [SerializeField] private float turnIntroDelay = 0.35f;

    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    private Action onEndCallback;
    private State state = State.Idle;
    private Vector3 ringCenter;
    private bool ended = false;

    public void Begin(Transform playerMonTf, PokemonInstance playerMon,
                      Transform wildMonTf, PokemonInstance wildMon,
                      Action onEnd)
    {
        this.playerMonTf = playerMonTf;
        this.wildMonTf = wildMonTf;
        onEndCallback = onEnd;

        boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(() => GetPlayerPosition(), (pos) => SetPlayerPosition(pos), () => ringCenter, ringRadiusForPlayer);

        playerCbt = new GameObject("PlayerCombatant").AddComponent<CombatantController>();
        playerCbt.transform.SetParent(transform);
        playerCbt.Init(playerMonTf, playerMon, isPlayer: true);

        enemyCbt = new GameObject("EnemyCombatant").AddComponent<CombatantController>();
        enemyCbt.transform.SetParent(transform);
        enemyCbt.Init(wildMonTf, wildMon, isPlayer: false);

        turnCtl = new GameObject("TurnController").AddComponent<TurnController>();
        turnCtl.transform.SetParent(transform);
        turnCtl.Setup(playerCbt, enemyCbt);

        ToggleCombatOn(playerMonTf, true);
        ToggleCombatOn(wildMonTf, true);

        ComputeAndPlaceRing();
        StartCoroutine(CoRun());
    }

    public void ForceEnd()
    {
        if (ended) return;
        EndEncounter(EncounterResult.ForcedEnd);
    }

    private IEnumerator CoRun()
    {
        state = State.Positioning;

        yield return StartCoroutine(CoPositionCombatants());

        while (!ended)
        {
            if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); break; }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); break; }

            state = State.PlayerTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));
            if (CheckEndMidTurn()) break;

            state = State.EnemyTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));
            if (CheckEndMidTurn()) break;
        }
    }

    private void Update()
    {
        if (!ended && playerCbt != null && enemyCbt != null)
            KeepCombatantsOnRing();
    }

    private bool CheckEndMidTurn()
    {
        if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); return true; }
        if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); return true; }
        return false;
    }

    private void ComputeAndPlaceRing()
    {
        var dir = (GetPlayerPosition() - wildMonTf.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();

        ringCenter = wildMonTf.position + dir * 1f;

        var pPos = ringCenter - dir * combatantOffsetFromCenter;
        var ePos = ringCenter + dir * combatantOffsetFromCenter;

        playerCbt.TeleportTo(pPos, lookAt: ringCenter);
        enemyCbt.TeleportTo(ePos, lookAt: ringCenter);
    }

    private IEnumerator CoPositionCombatants()
    {
        float t = 0f;
        while (t < 0.2f)
        {
            KeepCombatantsOnRing();
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void KeepCombatantsOnRing()
    {
        KeepOnRing(playerCbt);
        KeepOnRing(enemyCbt);
    }

    private void KeepOnRing(CombatantController cbt)
    {
        var current = cbt.Position;
        var fromCenter = current - ringCenter; fromCenter.y = 0f;

        // Si está justo en el centro (raro), empuja hacia delante del rival
        if (fromCenter.sqrMagnitude < 0.0001f)
            fromCenter = (current - ringCenter + Vector3.forward * 0.01f);

        var desired = ringCenter + fromCenter.normalized * combatantOffsetFromCenter;
        desired.y = current.y;

        var dist = Vector3.Distance(current, desired);
        if (dist > snapTolerance)
        {
            var step = repositionSpeed * Time.deltaTime;
            cbt.MoveTowardsPosition(desired, step);
            cbt.Face(ringCenter);
        }
    }

    private void EndEncounter(EncounterResult result)
    {
        ended = true;
        state = State.Ended;

        playerCbt.CleanupAfterBattle();
        enemyCbt.CleanupAfterBattle();

        ToggleCombatOn(playerMonTf, false);
        ToggleCombatOn(wildMonTf, false);

        onEndCallback?.Invoke();
        Destroy(gameObject);
    }

    // ---------- Helpers ----------
    private Vector3 GetPlayerPosition()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        return pc ? pc.transform.position : Vector3.zero;
    }
    private void SetPlayerPosition(Vector3 p)
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) pc.transform.position = p;
    }

    private static void ToggleCombatOn(Transform tf, bool active)
    {
        if (!tf) return;

        var pcb = tf.GetComponent<PlayerCreatureBehavior>() ?? tf.GetComponentInParent<PlayerCreatureBehavior>(true);
        if (pcb != null) pcb.SetCombatMode(active);

        var wild = tf.GetComponent<CreatureBehavior>() ?? tf.GetComponentInParent<CreatureBehavior>(true);
        if (wild != null) wild.SetCombatMode(active);
    }
}

public enum EncounterResult
{
    PlayerFainted, EnemyFainted, Capture, Run, ForcedEnd
}
