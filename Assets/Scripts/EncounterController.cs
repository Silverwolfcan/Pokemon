using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EncounterController : MonoBehaviour
{
    public enum State { Idle, Positioning, PlayerTurn, EnemyTurn, Ended }

    [Header("Ring / límites")]
    [Tooltip("Radio máximo para que el jugador se aleje del centro del combate.")]
    [SerializeField] private float ringRadiusForPlayer = 10f;

    [Tooltip("Distancia desde el centro (midpoint) a cada Pokémon. 2.5 => separación total ~5 m.")]
    [SerializeField] private float combatantOffsetFromCenter = 2.5f;

    [SerializeField] private float repositionSpeed = 12f;
    [SerializeField] private float snapTolerance = 0.05f;
    [SerializeField] private float turnIntroDelay = 0.25f;

    [Header("HUD (opcional)")]
    [Tooltip("Prefab del HUD (barra vida/nombre/nivel).")]
    [SerializeField] private GameObject combatantHUDPrefab;
    [Tooltip("Offset local si se usa fallback por SendMessage (cuando el prefab no tiene CombatantHUD).")]
    [SerializeField] private Vector3 hudOffset = new Vector3(0, 2.0f, 0);

    [Header("Desalojo de salvajes dentro del ring")]
    [Tooltip("Margen extra que deberán sobrepasar (ringRadius + clearance).")]
    [SerializeField] private float evictionClearance = 1.5f;
    [Tooltip("Velocidad con la que se empuja a los salvajes hacia fuera del ring.")]
    [SerializeField] private float evictionSpeed = 6f;
    [Tooltip("Cada cuántos segundos re-evaluamos si hay intrusos que desalojar mientras dura el combate.")]
    [SerializeField] private float evictionScanInterval = 0.75f;

    // Refs de escena/vivos
    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    // HUD instanciados (si procede)
    private GameObject playerHudGO, enemyHudGO;

    private Action onEndCallback;
    private State state = State.Idle;
    private Vector3 ringCenter;
    private bool ended = false;

    // Desalojo
    private readonly Dictionary<CreatureBehavior, Coroutine> activeEvictions = new Dictionary<CreatureBehavior, Coroutine>();
    private Coroutine evictionScannerCo;

    // --- Config en runtime (llamada por CombatService) ---
    public void ApplyConfig(float? offsetFromCenter = null, float? playerRingRadius = null)
    {
        if (offsetFromCenter.HasValue)
            combatantOffsetFromCenter = Mathf.Max(0.1f, offsetFromCenter.Value);
        if (playerRingRadius.HasValue)
            ringRadiusForPlayer = Mathf.Max(1f, playerRingRadius.Value);
    }

    /// <summary>Inicializa el encuentro con las referencias de los dos pokémon y callback de salida.</summary>
    public void Begin(Transform playerMonTf, PokemonInstance playerMon,
                      Transform wildMonTf, PokemonInstance wildMon,
                      Action onEnd)
    {
        this.playerMonTf = playerMonTf;
        this.wildMonTf = wildMonTf;
        this.onEndCallback = onEnd;

        // Centro del ring: punto medio entre ambos al iniciar
        ringCenter = (playerMonTf.position + wildMonTf.position) * 0.5f;

        // Límite de alejamiento del jugador (clamp dentro del ring)
        boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(() => GetPlayerPosition(), (pos) => SetPlayerPosition(pos), () => ringCenter, ringRadiusForPlayer);

        // Combatants (siempre operan sobre el Transform real en escena)
        playerCbt = gameObject.AddComponent<CombatantController>();
        playerCbt.Init(playerMonTf, playerMon, true);

        enemyCbt = gameObject.AddComponent<CombatantController>();
        enemyCbt.Init(wildMonTf, wildMon, false);

        // Turn controller
        turnCtl = gameObject.AddComponent<TurnController>();
        turnCtl.Setup(playerCbt, enemyCbt);

        // HUDs (opcionales)
        TrySpawnHUDs(playerMonTf, playerMon, wildMonTf, wildMon);

        // Poner a ambos en "modo combate" (desactiva rutinas de mundo)
        ToggleCombatOn(playerMonTf, true);
        ToggleCombatOn(wildMonTf, true);

        // Desalojar salvajes dentro del ring y mantener el ring limpio durante el combate
        StartEviction();

        // Arranque del ciclo
        StartCoroutine(CoRun());
    }

    /// <summary>Finaliza el encuentro sin condiciones (por ejemplo, cambio/teletransporte de escena).</summary>
    public void ForceEnd()
    {
        if (ended) return;
        EndEncounter(EncounterResult.ForcedEnd);
    }

    // -------------------- Bucle del encuentro --------------------
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
        // Dirección horizontal desde enemigo → jugador para colocar a ±offset
        Vector3 dir = (playerMonTf.position - wildMonTf.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        Vector3 playerTarget = ringCenter + dir * combatantOffsetFromCenter;
        Vector3 enemyTarget = ringCenter - dir * combatantOffsetFromCenter;

        // Teleport/lerp corto a posiciones objetivo + mirar al centro
        float t = 0f;
        float duration = 0.25f;
        Vector3 pStart = playerCbt.Position;
        Vector3 eStart = enemyCbt.Position;

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
        if (!ended && playerCbt != null && enemyCbt != null)
            KeepCombatantsOnRing();
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

    // -------------------- Cierre del encuentro --------------------
    private void EndEncounter(EncounterResult result)
    {
        ended = true;
        state = State.Ended;

        // Parar scanner/evictions y limpiar
        StopEviction();

        // Destruir HUDs si existen
        if (playerHudGO) Destroy(playerHudGO);
        if (enemyHudGO) Destroy(enemyHudGO);

        // Limpieza de combatants (restaura flags/animaciones, etc.)
        if (playerCbt != null) playerCbt.CleanupAfterBattle();
        if (enemyCbt != null) enemyCbt.CleanupAfterBattle();

        // Salir de modo combate en comportamientos de mundo
        ToggleCombatOn(playerMonTf, false);
        ToggleCombatOn(wildMonTf, false);

        // Callback al creador (CombatService) y autodestrucción
        onEndCallback?.Invoke();
        Destroy(gameObject);
    }

    private static void ToggleCombatOn(Transform tf, bool active)
    {
        if (!tf) return;

        var pcb = tf.GetComponent<PlayerCreatureBehavior>() ?? tf.GetComponentInParent<PlayerCreatureBehavior>(true);
        if (pcb != null) pcb.SetCombatMode(active);

        var wild = tf.GetComponent<CreatureBehavior>() ?? tf.GetComponentInParent<CreatureBehavior>(true);
        if (wild != null) wild.SetCombatMode(active);
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

    // -------------------- Señales de captura (Ball → CombatService → aquí) --------------------
    public void NotifyCaptureSuccess()
    {
        if (ended) return;
        EndEncounter(EncounterResult.Capture);
    }

    public void NotifyCaptureFailed()
    {
        if (ended) return;
        if (turnCtl != null)
            turnCtl.QueueCapture(); // consume el turno del jugador
    }

    // -------------------- HUD helpers --------------------
    private void TrySpawnHUDs(Transform playerTf, PokemonInstance playerModel,
                              Transform enemyTf, PokemonInstance enemyModel)
    {
        if (combatantHUDPrefab == null) return;

        // Player HUD
        playerHudGO = Instantiate(combatantHUDPrefab);
        AttachHud(playerHudGO, playerTf, playerModel, true);

        // Enemy HUD
        enemyHudGO = Instantiate(combatantHUDPrefab);
        AttachHud(enemyHudGO, enemyTf, enemyModel, false);
    }

    private void AttachHud(GameObject hud, Transform target, PokemonInstance model, bool isPlayer)
    {
        if (hud == null || target == null) return;

        // Si el prefab tiene CombatantHUD, usamos su API fuertemente tipada.
        var comp = hud.GetComponentInChildren<CombatantHUD>(true);
        if (comp != null)
        {
            // Importante: dejar el HUD en la raíz (no como hijo) para que su propio seguimiento
            // gestione la posición en LateUpdate sin heredar escala del Pokémon.
            hud.transform.SetParent(null, true);
            comp.Bind(target, model, Camera.main);
            return;
        }

        // Fallback genérico (si tu prefab no usa CombatantHUD).
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
        // Primer barrido inmediato
        EvictCreaturesInsideRing();

        // Scanner periódico para mantener el ring limpio
        evictionScannerCo = StartCoroutine(CoEvictionScanner());
    }

    private void StopEviction()
    {
        if (evictionScannerCo != null) { StopCoroutine(evictionScannerCo); evictionScannerCo = null; }

        foreach (var kv in activeEvictions)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
            // Asegurar que su IA queda habilitada si el combate terminó
            var beh = kv.Key;
            if (beh != null && beh.isActiveAndEnabled)
                beh.SetCombatMode(false);
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
        // Buscar todos los salvajes activos
        var all = FindObjectsOfType<CreatureBehavior>();
        foreach (var beh in all)
        {
            if (beh == null || !beh.isActiveAndEnabled) continue;

            // Excluir al enemigo del encuentro y a cualquier cosa colgada de los combatientes
            if (IsSameRoot(beh.transform, wildMonTf) || IsSameRoot(beh.transform, playerMonTf))
                continue;

            var pos = beh.transform.position;
            var toCenter = pos - ringCenter; toCenter.y = 0f;
            float dist = toCenter.magnitude;

            // Si está dentro del ring + margen pequeño, desalojar
            if (dist < ringRadiusForPlayer)
            {
                // Si ya estamos desalojándolo, continuar
                if (activeEvictions.ContainsKey(beh)) continue;

                // Lanzar tarea de desalojo
                var co = StartCoroutine(CoEvict(beh));
                activeEvictions[beh] = co;
            }
        }
    }

    private IEnumerator CoEvict(CreatureBehavior beh)
    {
        if (beh == null) yield break;

        // Pausar su IA de mundo durante el desalojo
        beh.SetCombatMode(true); // reutilizamos el bloqueo de movimiento/IA

        var tf = beh.transform;
        Vector3 start = tf.position;

        // Dirección radial desde el centro hacia el salvaje (nunca hacia el centro)
        Vector3 radial = (start - ringCenter); radial.y = 0f;
        if (radial.sqrMagnitude < 0.0001f) radial = UnityEngine.Random.onUnitSphere; // caso degenerado
        radial.y = 0f; radial.Normalize();

        // Objetivo: ring + clearance
        Vector3 target = ringCenter + radial * (ringRadiusForPlayer + Mathf.Max(0.1f, evictionClearance));
        target.y = start.y;

        float maxTime = 2.5f + (Vector3.Distance(start, target) / Mathf.Max(0.01f, evictionSpeed));
        float t = 0f;

        while (!ended && beh != null && tf != null)
        {
            float step = evictionSpeed * Time.deltaTime;
            tf.position = Vector3.MoveTowards(tf.position, target, step);

            // Orientación opcional hacia su dirección de movimiento
            Vector3 dir = (target - tf.position); dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(dir);
                tf.rotation = Quaternion.Slerp(tf.rotation, look, 10f * Time.deltaTime);
            }

            // ¿Ya está fuera?
            Vector3 flat = tf.position - ringCenter; flat.y = 0f;
            if (flat.magnitude >= ringRadiusForPlayer + Mathf.Max(0.1f, evictionClearance) - 0.05f)
                break;

            t += Time.deltaTime;
            if (t >= maxTime) break; // cortar por seguridad
            yield return null;
        }

        // Reanudar su IA de mundo si el combate continúa y el salvaje sigue válido
        if (!ended && beh != null && beh.isActiveAndEnabled)
            beh.SetCombatMode(false);

        activeEvictions.Remove(beh);
    }

    private static bool IsSameRoot(Transform a, Transform b)
    {
        if (a == null || b == null) return false;
        Transform ra = a.root, rb = b.root;
        return ra == rb;
    }
}

public enum EncounterResult
{
    PlayerFainted,
    EnemyFainted,
    Capture,
    Run,
    ForcedEnd
}
