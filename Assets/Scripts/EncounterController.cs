using System;
using System.Collections;
using UnityEngine;

public class EncounterController : MonoBehaviour
{
    public enum State { Idle, Positioning, PlayerTurn, EnemyTurn, Resolving, Ended }

    [Header("Ring / límites")]
    [SerializeField] private float ringRadiusForPlayer = 10f;

    [Tooltip("Distancia desde el centro a cada Pokémon. 2.5 => separación total ~5 m.")]
    [SerializeField] private float combatantOffsetFromCenter = 2.5f;

    [Header("Movimiento de posicionamiento")]
    [SerializeField] private float repositionSpeed = 12f;
    [SerializeField] private float snapTolerance = 0.05f;

    [Header("Turnos")]
    [SerializeField] private float turnIntroDelay = 0.15f;

    [Header("Refs (opcional en prefab)")]
    [SerializeField] private TurnController turnController;
    [SerializeField] private CombatBoundary boundary;

    [Header("HUD")]
    [Tooltip("Prefab con Canvas (World Space) + CombatantHUD. Se instancian 2: jugador y salvaje.")]
    [SerializeField] private GameObject combatantHudPrefab;

    private State state = State.Idle;
    private bool ended = false;

    private CombatantController playerCbt;
    private CombatantController enemyCbt;

    private Transform playerMonTf;
    private Transform wildMonTf;

    private PokemonInstance playerModel;
    private PokemonInstance wildModel;

    private PlayerController playerController;

    private Vector3 ringCenter;
    private Vector3 targetPlayerPos, targetEnemyPos;

    private Action onEndCallback;

    // HUD instanciados
    private GameObject playerHudGO;
    private GameObject enemyHudGO;

    public TurnController TurnController => turnController;

    public void ApplyConfig(float offsetFromCenter, float playerRingRadius)
    {
        combatantOffsetFromCenter = offsetFromCenter;
        ringRadiusForPlayer = playerRingRadius;
    }

    public void Begin(Transform playerTf, PokemonInstance player,
                      Transform wildTf, PokemonInstance wild,
                      Action onEnd = null)
    {
        if (state != State.Idle) { Debug.LogWarning("[Encounter] Begin() ignorado (no Idle)."); return; }

        onEndCallback = onEnd;

        playerMonTf = playerTf;
        wildMonTf = wildTf;
        playerModel = player;
        wildModel = wild;

        // Bloquea control/cámara como en tus menús
        playerController = FindAnyObjectByType<PlayerController>();
        playerController?.EnableControls(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Combatants (operan sobre el Transform real)
        playerCbt = gameObject.AddComponent<CombatantController>();
        playerCbt.Init(playerMonTf, playerModel, true);

        enemyCbt = gameObject.AddComponent<CombatantController>();
        enemyCbt.Init(wildMonTf, wildModel, false);

        // Pausar comportamientos de mundo
        ToggleCombatMode(playerMonTf, true);
        ToggleCombatMode(wildMonTf, true);

        // Centro y destinos
        ringCenter = ComputeMidpointXZ(playerMonTf.position, wildMonTf.position);
        ComputeTargets();

        // Boundary del jugador
        if (!boundary) boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(
            getPlayerPos: () => playerCbt.Position,
            setPlayerPos: p => playerCbt.Position = p,
            getCenter: () => ringCenter,
            radius: ringRadiusForPlayer
        );

        // TurnController con callback de fin (Huir, KOs, etc.)
        if (!turnController) turnController = gameObject.AddComponent<TurnController>();
        turnController.Setup(playerCbt, enemyCbt, (result) => EndEncounter(result));

        // ====== HUDs ======
        SpawnHUDs();

        // Loop
        StartCoroutine(CoRun());
    }

    public void ForceEnd() => EndEncounter(EncounterResult.ForcedEnd);

    // Punto de entrada oficial para cierre por CAPTURA (lo llamará CombatService.NotifyCaptureSuccess)
    public void EndByCapture() => EndEncounter(EncounterResult.Capture);

    private IEnumerator CoRun()
    {
        state = State.Positioning;
        yield return StartCoroutine(CoPositionCombatants());

        while (!ended)
        {
            if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); yield break; }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); yield break; }

            state = State.PlayerTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnController.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) yield break;

            if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); yield break; }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); yield break; }

            state = State.EnemyTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnController.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));
        }
    }

    private IEnumerator CoPositionCombatants()
    {
        ComputeTargets();

        playerCbt.Face(wildMonTf.position);
        enemyCbt.Face(playerMonTf.position);

        while (true)
        {
            float step = repositionSpeed * Time.deltaTime;

            playerCbt.MoveTowardsPosition(targetPlayerPos, step);
            enemyCbt.MoveTowardsPosition(targetEnemyPos, step);

            bool pDone = new Vector2(playerCbt.Position.x, playerCbt.Position.z).Equals2D(targetPlayerPos, snapTolerance);
            bool eDone = new Vector2(enemyCbt.Position.x, enemyCbt.Position.z).Equals2D(targetEnemyPos, snapTolerance);

            if (pDone && eDone) break;
            yield return null;
        }

        playerCbt.Position = targetPlayerPos;
        enemyCbt.Position = targetEnemyPos;
    }

    private void ComputeTargets()
    {
        Vector3 pj = playerMonTf.position;
        Vector3 en = wildMonTf.position;

        Vector3 dir = en - pj;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        ringCenter = ComputeMidpointXZ(pj, en);

        targetPlayerPos = ringCenter - dir * combatantOffsetFromCenter;
        targetPlayerPos.y = pj.y;

        targetEnemyPos = ringCenter + dir * combatantOffsetFromCenter;
        targetEnemyPos.y = en.y;
    }

    private static Vector3 ComputeMidpointXZ(Vector3 a, Vector3 b)
    {
        return new Vector3((a.x + b.x) * 0.5f, Mathf.Min(a.y, b.y), (a.z + b.z) * 0.5f);
    }

    private void EndEncounter(EncounterResult result)
    {
        if (ended) return;
        ended = true;
        state = State.Ended;

        // Limpieza de combatientes
        playerCbt?.CleanupAfterBattle();
        enemyCbt?.CleanupAfterBattle();

        // Reanudar rutinas de mundo (jugador y salvaje)
        ToggleCombatMode(playerMonTf, false);
        ToggleCombatMode(wildMonTf, false);

        // Restaurar control normal y cursor de gameplay
        playerController?.EnableControls(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Destruir HUDs
        if (playerHudGO) Destroy(playerHudGO);
        if (enemyHudGO) Destroy(enemyHudGO);

        // Notificar al caller (CombatService cierra UI y limpia)
        onEndCallback?.Invoke();

        Destroy(gameObject);
    }

    private static void ToggleCombatMode(Transform t, bool v)
    {
        if (!t) return;
        var pcb = t.GetComponent<PlayerCreatureBehavior>();
        if (pcb) { pcb.SetCombatMode(v); return; }
        var cb = t.GetComponent<CreatureBehavior>();
        if (cb) cb.SetCombatMode(v);
    }

    private void SpawnHUDs()
    {
        if (!combatantHudPrefab)
        {
            Debug.LogWarning("[Encounter] No se asignó 'combatantHudPrefab'. No se mostrarán HUDs.");
            return;
        }

        Camera cam = Camera.main;

        playerHudGO = Instantiate(combatantHudPrefab);
        var hudP = playerHudGO.GetComponent<CombatantHUD>();
        if (hudP) hudP.Bind(playerMonTf, playerModel, cam);

        enemyHudGO = Instantiate(combatantHudPrefab);
        var hudE = enemyHudGO.GetComponent<CombatantHUD>();
        if (hudE) hudE.Bind(wildMonTf, wildModel, cam);
    }
}

public enum EncounterResult { PlayerFainted, EnemyFainted, Capture, Run, ForcedEnd }

internal static class Vec2Ext
{
    public static bool Equals2D(this Vector2 a, Vector3 b, float tol)
    {
        float dx = a.x - b.x;
        float dz = a.y - b.z;
        return (dx * dx + dz * dz) <= tol * tol;
    }
}
