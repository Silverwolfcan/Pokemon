using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private float evictionSpeed = 6f;
    [SerializeField] private float evictionScanInterval = 0.75f;

    // Refs de escena/vivos
    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    // HUD instanciados (si procede)
    private GameObject playerHudGO, enemyHudGO;

    private Action<EncounterResult> onEndCallback;
    private State state = State.Idle;
    private Vector3 ringCenter;
    private bool ended = false;

    // Desalojo
    private readonly Dictionary<CreatureBehavior, Coroutine> activeEvictions = new();
    private Coroutine evictionScannerCo;

    // Expuesto para servicios/UI
    public TurnController Turn => turnCtl;

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

        // Combatants sobre ESTE GO (diseño original del proyecto)
        playerCbt = gameObject.AddComponent<CombatantController>();
        playerCbt.Init(playerMonTf, playerMon, true);

        enemyCbt = gameObject.AddComponent<CombatantController>();
        enemyCbt.Init(wildMonTf, wildMon, false);

        // Estados: en los TRANSFORMS reales, no en el Encounter (evita duplicados)
        var pStatus = playerMonTf.GetComponent<StatusContainer>();
        if (pStatus == null) pStatus = playerMonTf.gameObject.AddComponent<StatusContainer>();
        pStatus.Initialize(playerMon, true);
        pStatus.OnResidualDamageRequested += (amt, tag) => ApplyDirectDamage(playerCbt, amt, tag);
        pStatus.OnConfusionSelfHitRequested += (amt, tag) => ApplyDirectDamage(playerCbt, amt, tag);

        var eStatus = wildMonTf.GetComponent<StatusContainer>();
        if (eStatus == null) eStatus = wildMonTf.gameObject.AddComponent<StatusContainer>();
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
            if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); break; }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); break; }

            state = State.PlayerTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) break;

            if (playerCbt.IsFainted) { EndEncounter(EncounterResult.PlayerFainted); break; }
            if (enemyCbt.IsFainted) { EndEncounter(EncounterResult.EnemyFainted); break; }

            state = State.EnemyTurn;
            yield return new WaitForSeconds(turnIntroDelay);
            yield return StartCoroutine(turnCtl.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) break;
        }
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

        if (boundary) Destroy(boundary);
        if (playerHudGO) Destroy(playerHudGO);
        if (enemyHudGO) Destroy(enemyHudGO);

        playerCbt?.CleanupAfterBattle();
        enemyCbt?.CleanupAfterBattle();

        try { onEndCallback?.Invoke(result); }
        catch (Exception e) { Debug.LogError($"[Encounter] Callback error: {e}"); }

        Destroy(gameObject);
    }

    // -------------------- Señales de captura --------------------
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

    // -------------------- HUD helpers --------------------
    private void TrySpawnHUDs(Transform playerTf, PokemonInstance playerModel,
                              Transform enemyTf, PokemonInstance enemyModel)
    {
        // 1) Si hay prefab, úsalo
        if (combatantHUDPrefab != null)
        {
            playerHudGO = Instantiate(combatantHUDPrefab);
            AttachHud(playerHudGO, playerTf, playerModel, true);

            enemyHudGO = Instantiate(combatantHUDPrefab);
            AttachHud(enemyHudGO, enemyTf, enemyModel, false);
            return;
        }

        // 2) Fallback: rebindea HUDs existentes en la escena (primeros dos)
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
            var beh = kv.Key;
            if (beh != null && beh.isActiveAndEnabled) beh.SetCombatMode(false);
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
            if (dist <= ringRadiusForPlayer)
                if (!activeEvictions.ContainsKey(beh))
                    activeEvictions[beh] = StartCoroutine(CoTemporaryEvict(beh));
        }
    }
    private IEnumerator CoTemporaryEvict(CreatureBehavior beh)
    {
        if (beh == null) yield break;
        beh.SetCombatMode(true);

        var tf = beh.transform;
        Vector3 start = tf.position;
        Vector3 radial = (tf.position - ringCenter); radial.y = 0f;
        if (radial.sqrMagnitude < 0.0001f) radial = UnityEngine.Random.onUnitSphere;
        radial.y = 0f; radial.Normalize();

        Vector3 target = ringCenter + radial * (ringRadiusForPlayer + Mathf.Max(0.1f, evictionClearance));
        target.y = start.y;

        float maxTime = 2.5f + (Vector3.Distance(start, target) / Mathf.Max(0.01f, evictionSpeed));
        float t = 0f;

        while (!ended && beh != null && tf != null)
        {
            float step = evictionSpeed * Time.deltaTime;
            tf.position = Vector3.MoveTowards(tf.position, target, step);

            Vector3 dir = (target - tf.position); dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(dir);
                tf.rotation = Quaternion.Slerp(tf.rotation, look, 10f * Time.deltaTime);
            }

            Vector3 flat = tf.position - ringCenter; flat.y = 0f;
            if (flat.magnitude >= ringRadiusForPlayer + Mathf.Max(0.1f, evictionClearance) - 0.05f) break;

            t += Time.deltaTime;
            if (t >= maxTime) break;
            yield return null;
        }

        if (!ended && beh != null && beh.isActiveAndEnabled) beh.SetCombatMode(false);
        activeEvictions.Remove(beh);
    }

    private static bool IsSameRoot(Transform a, Transform b)
    {
        if (a == null || b == null) return false;
        return a.root == b.root;
    }

    // Daño directo solicitado por estados (DOT/confusión)
    private void ApplyDirectDamage(CombatantController who, int amount, string tag = "[Status]")
    {
        var mon = who?.Model;
        if (mon == null) return;
        int before = mon.currentHP;
        mon.currentHP = Mathf.Max(0, mon.currentHP - Mathf.Max(0, amount));
        Debug.Log($"[Damage]{tag} {mon.DisplayName} -{Mathf.Max(0, amount)} ({mon.currentHP}/{mon.stats.MaxHP})");
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
        if (tf == null) return;
        var pcb = tf.GetComponent<PlayerController>() ?? tf.GetComponentInParent<PlayerController>(true);
        if (pcb != null) pcb.EnableControls(!active);
        var wild = tf.GetComponent<CreatureBehavior>() ?? tf.GetComponentInParent<CreatureBehavior>(true);
        if (wild != null) wild.SetCombatMode(active);
    }
}
