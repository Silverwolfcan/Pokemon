using UnityEngine;
using static EncounterController;

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject encounterPrefab;
    [SerializeField] private GameObject combatCanvasRoot;

    [Header("Config por defecto")]
    [SerializeField] private float defaultOffsetFromCenter = 2.5f;
    [SerializeField] private float defaultPlayerRingRadius = 10f;

    private EncounterController activeEncounter;
    private CreatureBehavior pendingWildToDestroy;
    private bool captureInProgress = false;
    private PlayerCreatureBehavior encounterPlayerPCB;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetCombatCanvas(false);
    }

    public bool IsInEncounter => activeEncounter != null;
    public TurnController CurrentTurn => activeEncounter ? activeEncounter.Turn : null;

    public void StartEncounter(Transform playerMonTf, PokemonInstance playerMon,
                               Transform wildMonTf, PokemonInstance wildMon)
    {
        if (IsInEncounter || playerMon == null || wildMon == null) return;
        if (encounterPrefab == null) { Debug.LogError("[CombatService] Falta encounterPrefab."); return; }

        // limpiar logs al entrar por si quedó algo
        if (CombatLogPanel.Instance) CombatLogPanel.Instance.Clear();

        SetCombatCanvas(true);
        PrepareForBattleUI();

        encounterPlayerPCB = playerMonTf ? playerMonTf.GetComponentInParent<PlayerCreatureBehavior>(true) : null;
        if (encounterPlayerPCB) encounterPlayerPCB.SetCombatMode(true);

        pendingWildToDestroy =
            (wildMonTf ? (wildMonTf.GetComponentInParent<CreatureBehavior>(true) ??
                          wildMonTf.GetComponent<CreatureBehavior>()) : null);

        var go = Instantiate(encounterPrefab);
        activeEncounter = go.GetComponent<EncounterController>() ?? go.AddComponent<EncounterController>();
        activeEncounter.ApplyConfig(defaultOffsetFromCenter, defaultPlayerRingRadius);

        var selector = FindAnyObjectByType<ItemSelectorUI>();
        if (selector != null)
        {
            selector.SetCaptureLock(true);
            selector.SetMode(SelectorMode.Pokeball);
            selector.RefreshBalls();
        }

        captureInProgress = false;

        activeEncounter.Begin(playerMonTf, playerMon, wildMonTf, wildMon, onEnd: (result) =>
        {
            selector?.SetCaptureLock(false);

            string lastId = activeEncounter?.Turn?.PlayerModel?.UniqueID;

            captureInProgress = false;

            if ((result == EncounterResult.EnemyFainted || result == EncounterResult.Capture) && pendingWildToDestroy)
                Destroy(pendingWildToDestroy.gameObject);
            pendingWildToDestroy = null;

            var pc = FindAnyObjectByType<PlayerController>();
            if (pc) pc.RecallActiveSummonedToBall();
            encounterPlayerPCB = null;

            if (selector != null)
            {
                selector.SetCaptureLock(false);
                if (!string.IsNullOrEmpty(lastId)) selector.FocusPokemonById(lastId);
                else selector.SetMode(SelectorMode.Pokemon);
            }

            // limpiar logs al terminar
            if (CombatLogPanel.Instance) CombatLogPanel.Instance.Clear();

            activeEncounter = null;
            SetCombatCanvas(false);
            RestorePlayerControls();
        });
    }

    public void ForceEndEncounter()
    {
        if (!activeEncounter)
        {
            SetCombatCanvas(false);
            var pc0 = FindAnyObjectByType<PlayerController>();
            if (pc0) pc0.RecallActiveSummonedToBall();

            var selector0 = FindAnyObjectByType<ItemSelectorUI>();
            selector0?.SetCaptureLock(false);
            selector0?.SetMode(SelectorMode.Pokemon);

            if (CombatLogPanel.Instance) CombatLogPanel.Instance.Clear();

            RestorePlayerControls();
            return;
        }

        string lastId = activeEncounter?.Turn?.PlayerModel?.UniqueID;

        activeEncounter.ForceEnd();
        activeEncounter = null;
        captureInProgress = false;

        var selector = FindAnyObjectByType<ItemSelectorUI>();
        selector?.SetCaptureLock(false);
        if (!string.IsNullOrEmpty(lastId)) selector?.FocusPokemonById(lastId);
        else selector?.SetMode(SelectorMode.Pokemon);

        pendingWildToDestroy = null;

        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) pc.RecallActiveSummonedToBall();
        encounterPlayerPCB = null;

        if (CombatLogPanel.Instance) CombatLogPanel.Instance.Clear();

        SetCombatCanvas(false);
        RestorePlayerControls();
    }

    public bool BeginCaptureAttempt()
    {
        if (!IsInEncounter) return false;
        if (captureInProgress) return false;
        captureInProgress = true;
        return true;
    }

    public void NotifyCaptureSuccess()
    {
        if (!IsInEncounter) return;
        captureInProgress = false;
        activeEncounter.NotifyCaptureSuccess();
    }

    public void NotifyCaptureFailed()
    {
        if (!IsInEncounter) return;
        captureInProgress = false;
        activeEncounter.NotifyCaptureFailed();
    }

    private void SetCombatCanvas(bool on)
    {
        if (!combatCanvasRoot) return;
        if (combatCanvasRoot.activeSelf != on) combatCanvasRoot.SetActive(on);
    }

    private static void PrepareForBattleUI()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc)
        {
            pc.ForceExitAimAndResetFov();
            pc.EnableControls(false);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void RestorePlayerControls()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) pc.EnableControls(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
