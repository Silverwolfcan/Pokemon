using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EncounterController : MonoBehaviour
{
    public enum State { Idle, Positioning, PlayerTurn, EnemyTurn, Ended }
    public enum EncounterResult { PlayerFainted, EnemyFainted, Capture, Run, ForcedEnd }

    [Header("Ring / límites")]
    [SerializeField] private float ringRadiusForPlayer = 10f;
    [SerializeField] private float combatantOffsetFromCenter = 2.5f;
    [SerializeField] private float repositionSpeed = 12f;
    [SerializeField] private float snapTolerance = 0.05f;
    [SerializeField] private float turnIntroDelay = 0.25f;

    [Header("HUD (opcional)")]
    [SerializeField] private GameObject combatantHUDPrefab;
    [SerializeField] private Vector3 hudOffset = new Vector3(0, 2.0f, 0);

    [Header("Desalojo de salvajes dentro del ring")]
    [SerializeField] private float evictionClearance = 1.5f;
    [SerializeField] private float evictionScanInterval = 0.75f;

    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    private GameObject playerHudGO, enemyHudGO;

    private Action<EncounterResult> onEndCallback;
    private State state = State.Idle;
    private Vector3 ringCenter;
    private bool ended = false;

    private readonly Dictionary<CreatureBehavior, Coroutine> activeEvictions = new();
    private Coroutine evictionScannerCo;

    public TurnController Turn => turnCtl;

    // Estado global para IA de mundo
    public static bool RingActive { get; private set; }
    public static Vector3 RingCenterS { get; private set; }
    public static float RingRadiusS { get; private set; }

    public static bool IsRingActive => RingActive;
    public static bool IsInsideRing(Vector3 pos)
    {
        if (!RingActive) return false;
        var p = pos; p.y = 0f;
        var c = RingCenterS; c.y = 0f;
        return Vector3.Distance(p, c) < RingRadiusS - 0.0001f;
    }

    public void ApplyConfig(float? offsetFromCenter = null, float? playerRingRadius = null)
    {
        if (offsetFromCenter.HasValue) combatantOffsetFromCenter = Mathf.Max(0.1f, offsetFromCenter.Value);
        if (playerRingRadius.HasValue) ringRadiusForPlayer = Mathf.Max(1f, playerRingRadius.Value);
    }

    public void Begin(Transform playerMonTf, PokemonInstance playerMon,
                      Transform wildMonTf, PokemonInstance wildMon,
                      Action<EncounterResult> onEnd)
    {
        this.playerMonTf = playerMonTf;
        this.wildMonTf = wildMonTf;
        this.onEndCallback = onEnd;

        ringCenter = (playerMonTf.position + wildMonTf.position) * 0.5f;

        boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(() => GetPlayerPosition(), (pos) => SetPlayerPosition(pos), () => ringCenter, ringRadiusForPlayer);

        // Eliminar cualquier “pared” física previa
        StripPhysicalBlockers();

        RingActive = true;
        RingCenterS = ringCenter;
        RingRadiusS = ringRadiusForPlayer;

        playerCbt = gameObject.AddComponent<CombatantController>();
        playerCbt.Init(playerMonTf, playerMon, true);

        enemyCbt = gameObject.AddComponent<CombatantController>();
        enemyCbt.Init(wildMonTf, wildMon, false);

        var pStatus = playerMonTf.GetComponent<StatusContainer>() ?? playerMonTf.gameObject.AddComponent<StatusContainer>();
        pStatus.Initialize(playerMon, true);
        pStatus.OnResidualDamageRequested += (amt, tag) => ApplyDirectDamage(playerCbt, amt, tag);
        pStatus.OnConfusionSelfHitRequested += (amt, tag) => ApplyDirectDamage(playerCbt, amt, tag);

        var eStatus = wildMonTf.GetComponent<StatusContainer>() ?? wildMonTf.gameObject.AddComponent<StatusContainer>();
        eStatus.Initialize(wildMon, false);
        eStatus.OnResidualDamageRequested += (amt, tag) => ApplyDirectDamage(enemyCbt, amt, tag);
        eStatus.OnConfusionSelfHitRequested += (amt, tag) => ApplyDirectDamage(enemyCbt, amt, tag);

        turnCtl = gameObject.AddComponent<TurnController>();
        turnCtl.Setup(playerCbt, enemyCbt);

        TrySpawnHUDs(playerMonTf, playerMon, wildMonTf, wildMon);

        ToggleCombatOn(playerMonTf, true);
        ToggleCombatOn(wildMonTf, true);

        StartEviction();
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
            if (playerCbt.IsFainted)
            {
                if (HasAliveReplacementForPlayer())
                {
                    if (turnCtl != null && !turnCtl.ForceSwitchPending)
                        turnCtl.RequestForcedPlayerSwitch();

                    while (!ended && (playerCbt.IsFainted || (turnCtl != null && turnCtl.ForceSwitchPending)))
                        yield return null;

                    if (ended) break;
                }
                else { EndEncounter(EncounterResult.PlayerFainted); break; }
            }

            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); break; }

            state = State.PlayerTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) break;

            if (playerCbt.IsFainted)
            {
                if (HasAliveReplacementForPlayer())
                {
                    if (turnCtl != null && !turnCtl.ForceSwitchPending)
                        turnCtl.RequestForcedPlayerSwitch();

                    while (!ended && (playerCbt.IsFainted || (turnCtl != null && turnCtl.ForceSwitchPending)))
                        yield return null;

                    if (ended) break;
                }
                else { EndEncounter(EncounterResult.PlayerFainted); break; }
            }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); break; }

            state = State.EnemyTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) break;
        }
    }

    private bool HasAliveReplacementForPlayer()
    {
        var party = PokemonStorageManager.Instance ? PokemonStorageManager.Instance.PlayerParty : null;
        if (party == null) return false;
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            if (p != null && !ReferenceEquals(p, playerCbt?.Model) && p.currentHP > 0) return true;
        }
        return false;
    }

    private IEnumerator CoPositionCombatants()
    {
        Vector3 dir = (playerMonTf.position - wildMonTf.position); dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        Vector3 playerTarget = ringCenter + dir * combatantOffsetFromCenter;
        Vector3 enemyTarget = ringCenter - dir * combatantOffsetFromCenter;

        float t = 0f, duration = 0.25f;
        Vector3 pStart = playerCbt.Position, eStart = enemyCbt.Position;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            playerCbt.Position = Vector3.Lerp(pStart, playerTarget, a);
            enemyCbt.Position = Vector3.Lerp(eStart, enemyTarget, a);
            playerCbt.Face(ringCenter);
            enemyCbt.Face(ringCenter);
            yield return null;
        }

        playerCbt.Position = playerTarget;
        enemyCbt.Position = enemyTarget;
        playerCbt.Face(ringCenter);
        enemyCbt.Face(ringCenter);
    }

    private void Update()
    {
        if (!ended && playerCbt != null && enemyCbt != null) KeepCombatantsOnRing();
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
        if (fromCenter.sqrMagnitude < 0.0001f) fromCenter = Vector3.forward * 0.01f;

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

        ToggleCombatOn(playerMonTf, false);
        ToggleCombatOn(wildMonTf, false);

        StopEviction();

        // Limpieza por seguridad
        StripPhysicalBlockers();

        if (boundary) Destroy(boundary);
        if (playerHudGO) Destroy(playerHudGO);
        if (enemyHudGO) Destroy(enemyHudGO);

        playerCbt?.CleanupAfterBattle();
        enemyCbt?.CleanupAfterBattle();

        RingActive = false;

        // ← Cursor vuelve a modo gameplay al TERMINAR el combate
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        try { onEndCallback?.Invoke(result); }
        catch (Exception e) { Debug.LogError($"[Encounter] Callback error: {e}"); }

        Destroy(gameObject);
    }

    public void NotifyCaptureSuccess()
    {
        if (ended) return;
        EndEncounter(EncounterResult.Capture);
    }
    public void NotifyCaptureFailed()
    {
        if (ended) return;
        if (turnCtl != null) turnCtl.QueueCapture();
    }

    private void TrySpawnHUDs(Transform playerTf, PokemonInstance playerModel,
                              Transform enemyTf, PokemonInstance enemyModel)
    {
        if (combatantHUDPrefab != null)
        {
            playerHudGO = Instantiate(combatantHUDPrefab);
            AttachHud(playerHudGO, playerTf, playerModel, true);

            enemyHudGO = Instantiate(combatantHUDPrefab);
            AttachHud(enemyHudGO, enemyTf, enemyModel, false);
            return;
        }

        var huds = FindObjectsByType<CombatantHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (huds != null && huds.Length > 0)
        {
            if (huds.Length >= 1) huds[0].Bind(playerTf, playerModel, Camera.main);
            if (huds.Length >= 2) huds[1].Bind(enemyTf, enemyModel, Camera.main);
        }
    }

    private void AttachHud(GameObject hud, Transform target, PokemonInstance model, bool isPlayer)
    {
        if (hud == null || target == null) return;

        var comp = hud.GetComponentInChildren<CombatantHUD>(true);
        if (comp != null)
        {
            hud.transform.SetParent(null, true);
            comp.Bind(target, model, Camera.main);
            return;
        }

        hud.transform.SetParent(target, false);
        hud.transform.localPosition = hudOffset;
        var cam = Camera.main;
        hud.SendMessage("SetModel", model, SendMessageOptions.DontRequireReceiver);
        hud.SendMessage("SetTarget", target, SendMessageOptions.DontRequireReceiver);
        hud.SendMessage("SetIsPlayer", isPlayer, SendMessageOptions.DontRequireReceiver);
        hud.SendMessage("SetCamera", cam, SendMessageOptions.DontRequireReceiver);
        hud.SendMessage("SetOffset", hudOffset, SendMessageOptions.DontRequireReceiver);
        hud.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
    }

    // -------------------- Desalojo de terceros --------------------
    private void StartEviction()
    {
        EvictCreaturesInsideRing();
        evictionScannerCo = StartCoroutine(CoEvictionScanner());
    }
    private void StopEviction()
    {
        if (evictionScannerCo != null) { StopCoroutine(evictionScannerCo); evictionScannerCo = null; }
        foreach (var kv in activeEvictions)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        activeEvictions.Clear();
    }
    private IEnumerator CoEvictionScanner()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.1f, evictionScanInterval));
        while (!ended)
        {
            EvictCreaturesInsideRing();
            yield return wait;
        }
    }
    private void EvictCreaturesInsideRing()
    {
        var all = FindObjectsByType<CreatureBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var beh in all)
        {
            if (beh == null || !beh.isActiveAndEnabled) continue;
            if (IsSameRoot(beh.transform, playerMonTf) || IsSameRoot(beh.transform, wildMonTf)) continue;

            float dist = Vector3.Distance(beh.transform.position, ringCenter);
            if (dist <= ringRadiusForPlayer && !activeEvictions.ContainsKey(beh))
                activeEvictions[beh] = StartCoroutine(CoTemporaryEvict(beh));
        }
    }
    private IEnumerator CoTemporaryEvict(CreatureBehavior beh)
    {
        if (beh == null) yield break;

        var tf = beh.transform;
        Vector3 radial = tf.position - ringCenter; radial.y = 0f;
        if (radial.sqrMagnitude < 0.0001f) radial = UnityEngine.Random.onUnitSphere;
        radial.y = 0f; radial.Normalize();

        Vector3 target = ringCenter + radial * (ringRadiusForPlayer + Mathf.Max(0.1f, evictionClearance));
        target.y = tf.position.y;

        var agent = beh.GetComponent<NavMeshAgent>();
        bool warped = false;
        if (agent && agent.isOnNavMesh &&
            NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            warped = true;
        }
        if (!warped) tf.position = target;

        // orientar hacia fuera
        var dir = (tf.position - ringCenter); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) tf.rotation = Quaternion.LookRotation(dir);

        activeEvictions.Remove(beh);
        yield break;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // utilidades
    private static bool IsSameRoot(Transform a, Transform b) => a && b && a.root == b.root;

    private void ApplyDirectDamage(CombatantController who, int amount, string tag = "[Status]")
    {
        var mon = who?.Model; if (mon == null) return;
        mon.currentHP = Mathf.Max(0, mon.currentHP - Mathf.Max(0, amount));
    }

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
    private void ToggleCombatOn(Transform tf, bool active)
    {
        if (!tf) return;
        var pcb = tf.GetComponent<PlayerController>() ?? tf.GetComponentInParent<PlayerController>(true);
        if (pcb) pcb.EnableControls(!active);
        var wild = tf.GetComponent<CreatureBehavior>() ?? tf.GetComponentInParent<CreatureBehavior>(true);
        if (wild) wild.SetCombatMode(active);
    }

    // Elimina colliders/obstáculos que pudieran quedar en el GO del encuentro
    private void StripPhysicalBlockers()
    {
        foreach (var c in GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var o in GetComponentsInChildren<NavMeshObstacle>(true)) Destroy(o);
    }
}
