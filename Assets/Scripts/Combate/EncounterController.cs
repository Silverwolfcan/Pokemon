using System;
using System.Collections;
using UnityEngine;

public class EncounterController : MonoBehaviour
{
    [Header("Ring / límites")]
    [SerializeField] private float ringRadiusForPlayer = 10f;
    [SerializeField] private float combatantOffsetFromCenter = 2.5f;

    [Header("HUD (opcional)")]
    [SerializeField] private GameObject combatantHUDPrefab;

    [Header("Reglas del encuentro")]
    [Tooltip("Permite lanzar Poké Balls.")]
    public bool IsCaptureAllowed = true;
    [Tooltip("Permite usar la acción Huir.")]
    public bool CanRun = true;

    private Transform playerMonTf, wildMonTf;
    private CombatantController playerCbt, enemyCbt;
    private TurnController turnCtl;
    private CombatBoundary boundary;

    private GameObject playerHudGO, enemyHudGO;
    private Action onEndCallback;
    private Vector3 ringCenter;
    private bool ended = false;

    // ---------- Config pública ----------
    public void ApplyConfig(float? offsetFromCenter = null, float? playerRingRadius = null)
    {
        if (offsetFromCenter.HasValue) combatantOffsetFromCenter = Mathf.Max(0.1f, offsetFromCenter.Value);
        if (playerRingRadius.HasValue) ringRadiusForPlayer = Mathf.Max(1f, playerRingRadius.Value);
    }

    /// Llama esto desde tu servicio de combate si el encuentro es de entrenador.
    public void SetEncounterRules(bool canCapture, bool canRun)
    {
        IsCaptureAllowed = canCapture;
        CanRun = canRun;
        Debug.Log($"[Encounter] Reglas → Capture:{IsCaptureAllowed} Run:{CanRun}");
    }

    // ---------- Inicio del encuentro ----------
    public void Begin(Transform playerMonTf, PokemonInstance playerMon,
                      Transform wildMonTf, PokemonInstance wildMon,
                      Action onEnd)
    {
        this.playerMonTf = playerMonTf;
        this.wildMonTf = wildMonTf;
        this.onEndCallback = onEnd;

        ringCenter = (playerMonTf.position + wildMonTf.position) * 0.5f;

        boundary = gameObject.AddComponent<CombatBoundary>();
        boundary.Setup(() => GetPlayerPosition(), (p) => SetPlayerPosition(p), () => ringCenter, ringRadiusForPlayer);

        playerCbt = gameObject.AddComponent<CombatantController>();
        playerCbt.Init(playerMonTf, playerMon, true);

        enemyCbt = gameObject.AddComponent<CombatantController>();
        enemyCbt.Init(wildMonTf, wildMon, false);

        turnCtl = gameObject.AddComponent<TurnController>();
        turnCtl.Setup(playerCbt, enemyCbt);

        TrySpawnHUDs(playerMonTf, playerMon, wildMonTf, wildMon);

        ToggleCombatOn(playerMonTf, true);
        ToggleCombatOn(wildMonTf, true);

        try { GameEventBus.RaiseEncounterStateChanged(true); } catch { }

        StartCoroutine(CoRun());
    }

    public void ForceEnd()
    {
        if (ended) return;
        EndEncounter(EncounterResult.ForcedEnd);
    }

    // ---------- Bucle principal ----------
    private IEnumerator CoRun()
    {
        yield return StartCoroutine(CoPositionCombatants());

        while (!ended)
        {
            if (playerCbt.IsFainted) { OnPlayerFainted(); if (ended) break; }
            if (enemyCbt.IsFainted) { OnEnemyFainted(); if (ended) break; }

            yield return StartCoroutine(turnCtl.DoPlayerTurn(ringCenter, combatantOffsetFromCenter));
            if (ended) break;

            yield return StartCoroutine(turnCtl.DoEnemyTurn(ringCenter, combatantOffsetFromCenter));
        }
    }

    private IEnumerator CoPositionCombatants()
    {
        Vector3 playerPos = ringCenter + (-playerMonTf.forward).normalized * combatantOffsetFromCenter;
        Vector3 enemyPos = ringCenter + (playerMonTf.forward).normalized * combatantOffsetFromCenter;

        playerMonTf.position = playerPos;
        wildMonTf.position = enemyPos;

        Face(ringCenter, playerMonTf);
        Face(ringCenter, wildMonTf);
        yield return null;
    }

    private static void Face(Vector3 target, Transform tf)
    {
        var lookDir = (target - tf.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            tf.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }

    private static void ToggleCombatOn(Transform tf, bool active)
    {
        if (!tf) return;
        tf.GetComponent<PlayerCreatureBehavior>()?.SetCombatMode(active);
        tf.GetComponent<CreatureBehavior>()?.SetCombatMode(active);
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

    // ---------- Señales externas ----------
    public void NotifyCaptureSuccess()
    {
        if (ended) return;
        EndEncounter(EncounterResult.Capture);
    }

    public void NotifyCaptureFailed()
    {
        if (ended) return;
        if (turnCtl != null) turnCtl.ConsumePlayerTurn();
    }

    // ---------- Acciones del jugador ----------
    public bool TryPlayerRun()
    {
        if (ended) return false;
        if (!CanRun)
        {
            Debug.Log("[Run] Acción deshabilitada por reglas del encuentro.");
            return false;
        }

        int pSpeed = CombatStatUtility.GetEffectiveSpeed(playerCbt.Model);
        int eSpeed = CombatStatUtility.GetEffectiveSpeed(enemyCbt.Model);

        float chance = 0.5f + (pSpeed > eSpeed ? 0.25f : 0f);
        chance = Mathf.Clamp01(chance);

        bool ok = UnityEngine.Random.value < chance;

        if (ok)
        {
            Debug.Log($"[Run] Huida exitosa. vP={pSpeed}, vE={eSpeed}, p={chance:0%}");
            EndEncounter(EncounterResult.Run);
        }
        else
        {
            Debug.Log($"[Run] Huida fallida. vP={pSpeed}, vE={eSpeed}, p={chance:0%}");
        }

        return ok;
    }

    public bool TryPlayerSwitch(int partyIndex)
    {
        var storage = PokemonStorageManager.Instance?.PlayerParty;
        if (storage == null) return false;

        PokemonInstance newMon = null;
        try { newMon = storage.GetAt(partyIndex); } catch { newMon = null; }
        if (newMon == null || newMon.currentHP <= 0) return false;
        if (ReferenceEquals(newMon, playerCbt.Model)) return false;

        StatStageService.Clear(playerCbt.Model);
        playerCbt.SetModel(newMon);

        if (playerHudGO != null) AttachHud(playerHudGO, playerMonTf, newMon, true);
        return true;
    }

    public bool TryAutoSwitchNextAlive()
    {
        var party = PokemonStorageManager.Instance?.PlayerParty;
        if (party == null) return false;

        int cap = party.MaxCapacity;
        for (int i = 0; i < cap; i++)
        {
            PokemonInstance p = null;
            try { p = party.GetAt(i); } catch { p = null; }
            if (p != null && p.currentHP > 0 && !ReferenceEquals(p, playerCbt.Model))
            {
                Debug.Log($"[Encounter] Auto-cambio a {p.DisplayName} por KO.");
                return TryPlayerSwitch(i);
            }
        }
        return false;
    }

    public void OnEnemyFainted()
    {
        if (ended) return;
        EndEncounter(EncounterResult.EnemyFainted);
    }

    public void OnPlayerFainted()
    {
        if (ended) return;
        bool switched = TryAutoSwitchNextAlive();
        if (!switched) EndEncounter(EncounterResult.PlayerFainted);
    }

    // ---------- HUD ----------
    private void TrySpawnHUDs(Transform pTf, PokemonInstance pModel,
                              Transform eTf, PokemonInstance eModel)
    {
        if (combatantHUDPrefab == null) return;

        playerHudGO = Instantiate(combatantHUDPrefab);
        AttachHud(playerHudGO, pTf, pModel, true);

        enemyHudGO = Instantiate(combatantHUDPrefab);
        AttachHud(enemyHudGO, eTf, eModel, false);
    }

    private static void AttachHud(GameObject go, Transform followTf, PokemonInstance model, bool isPlayer)
    {
        if (!go || !followTf) return;

        var comp = go.GetComponentInChildren<CombatantHUD>(true);
        if (comp != null)
        {
            go.transform.SetParent(null, true);
            comp.Bind(followTf, model, Camera.main);
            return;
        }

        go.transform.SetParent(followTf, false);
        go.transform.localPosition = Vector3.up * 2f;
    }

    // ---------- Cierre ----------
    private void EndEncounter(EncounterResult result)
    {
        ended = true;

        if (playerHudGO) Destroy(playerHudGO);
        if (enemyHudGO) Destroy(enemyHudGO);

        if (playerCbt != null) playerCbt.CleanupAfterBattle();
        if (enemyCbt != null) enemyCbt.CleanupAfterBattle();

        try
        {
            if (playerCbt?.Model != null)
            {
                StatStageService.ResetAllFor(playerCbt.Model);
                PokemonStatusService.Clear(playerCbt.Model);
            }
            if (enemyCbt?.Model != null)
            {
                StatStageService.ResetAllFor(enemyCbt.Model);
                PokemonStatusService.Clear(enemyCbt.Model);
            }
        }
        catch { }

        ToggleCombatOn(playerMonTf, false);
        ToggleCombatOn(wildMonTf, false);

        try { GameEventBus.RaiseEncounterStateChanged(false); } catch { }

        onEndCallback?.Invoke();
        Destroy(gameObject);
    }
}

public enum EncounterResult { PlayerFainted, EnemyFainted, Capture, Run, ForcedEnd }
