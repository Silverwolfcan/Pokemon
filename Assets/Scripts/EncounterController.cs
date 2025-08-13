using System;
using System.Collections;
using UnityEngine;

public class EncounterController : MonoBehaviour
{
    public enum State { Idle, Positioning, PlayerTurn, EnemyTurn, Resolving, Ended }

    [Header("Ring / límites")]
    [SerializeField] private float ringRadiusForPlayer = 10f; // máximo que puede alejarse el jugador
    [SerializeField] private float combatantOffsetFromCenter = 1f; // 1 m
    [SerializeField] private float repositionSpeed = 12f; // m/s para recolocar rápido si salen del ring (pokémon)
    [SerializeField] private float snapTolerance = 0.05f;

    [Header("Turnos")]
    [SerializeField] private float turnIntroDelay = 0.35f; // pequeña pausa entre turnos

    // Contexto
    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    private Action onEndCallback;
    private State state = State.Idle;
    private Vector3 ringCenter;
    private bool ended = false;

    // Entrypoint
    public void Begin(Transform playerMonTf, PokemonInstance playerMon,
                      Transform wildMonTf, PokemonInstance wildMon,
                      Action onEnd)
    {
        this.playerMonTf = playerMonTf;
        this.wildMonTf = wildMonTf;
        onEndCallback = onEnd;

        // Crear boundary que limita al jugador (si quieres, cámbialo a un collider)
        boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(() => GetPlayerPosition(), (pos) => SetPlayerPosition(pos), () => ringCenter, ringRadiusForPlayer);

        // Combatants
        playerCbt = new GameObject("PlayerCombatant").AddComponent<CombatantController>();
        playerCbt.transform.SetParent(transform);
        playerCbt.Init(playerMonTf, playerMon, isPlayer: true);

        enemyCbt = new GameObject("EnemyCombatant").AddComponent<CombatantController>();
        enemyCbt.transform.SetParent(transform);
        enemyCbt.Init(wildMonTf, wildMon, isPlayer: false);

        // Turnos
        turnCtl = new GameObject("TurnController").AddComponent<TurnController>();
        turnCtl.transform.SetParent(transform);
        turnCtl.Setup(playerCbt, enemyCbt);

        // Posicionar anillo
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

        // Apartar a “bystanders” (TODO: aquí puedes añadir tu lógica de IA para alejarlos)
        yield return StartCoroutine(CoPositionCombatants());

        // Bucle principal de turnos
        while (!ended)
        {
            // Comprobaciones de KO / fin
            if (playerCbt.IsFainted)
            {
                EndEncounter(EncounterResult.PlayerFainted);
                break;
            }
            if (enemyCbt.IsFainted)
            {
                EndEncounter(EncounterResult.EnemyFainted);
                break;
            }

            // PLAYER TURN
            state = State.PlayerTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));

            if (CheckEndMidTurn()) break;

            // ENEMY TURN
            state = State.EnemyTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));

            if (CheckEndMidTurn()) break;
        }
    }

    private bool CheckEndMidTurn()
    {
        if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); return true; }
        if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); return true; }
        return false;
    }

    private void ComputeAndPlaceRing()
    {
        // Centro: 1m desde el salvaje hacia el jugador
        var dir = (GetPlayerPosition() - wildMonTf.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();

        ringCenter = wildMonTf.position + dir * 1f;

        // Sitúa a ambos a 1m del centro, enfrentados
        var pPos = ringCenter - dir * combatantOffsetFromCenter;
        var ePos = ringCenter + dir * combatantOffsetFromCenter;

        playerCbt.TeleportTo(pPos, lookAt: ringCenter);
        enemyCbt.TeleportTo(ePos, lookAt: ringCenter);
    }

    private IEnumerator CoPositionCombatants()
    {
        // Pequeño “snap” a posiciones exactas con una breve interpolación
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
        var desired = ringCenter + (cbt.transform.position - ringCenter).normalized * combatantOffsetFromCenter;
        desired.y = cbt.transform.position.y;
        var dist = Vector3.Distance(cbt.transform.position, desired);
        if (dist > snapTolerance)
        {
            var step = repositionSpeed * Time.deltaTime;
            cbt.transform.position = Vector3.MoveTowards(cbt.transform.position, desired, step);
            cbt.Face(ringCenter);
        }
    }

    private void EndEncounter(EncounterResult result)
    {
        ended = true;
        state = State.Ended;

        // Limpia estados temporales (si pones stat stages y tal)
        playerCbt.CleanupAfterBattle();
        enemyCbt.CleanupAfterBattle();

        // Devuelve controles del mundo, etc. (si pausaste algo)
        // TODO: reactivar cosas del mundo si las desactivaste al empezar.

        onEndCallback?.Invoke();
        Destroy(gameObject);
    }

    // Helpers para el boundary del jugador (puedes adaptarlo a tu PlayerController)
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
}

public enum EncounterResult
{
    PlayerFainted, EnemyFainted, Capture, Run, ForcedEnd
}
