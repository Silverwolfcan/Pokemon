using UnityEngine;
using static EncounterController;

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject encounterPrefab;          // Prefab con EncounterController
    [SerializeField] private GameObject combatCanvasRoot;         // Canvas de combate (opcional)

    [Header("Config por defecto")]
    [SerializeField] private float defaultOffsetFromCenter = 2.5f;
    [SerializeField] private float defaultPlayerRingRadius = 10f;

    private EncounterController activeEncounter;
    private CreatureBehavior pendingWildToDestroy;                // salvaje asociado a este combate
    private bool captureInProgress = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetCombatCanvas(false);
    }

    public bool IsInEncounter => activeEncounter != null;
    public TurnController CurrentTurn => activeEncounter ? activeEncounter.Turn : null;

    /// <summary>Arranca un combate jugador vs. salvaje.</summary>
    public void StartEncounter(Transform playerMonTf, PokemonInstance playerMon,
                               Transform wildMonTf, PokemonInstance wildMon)
    {
        if (IsInEncounter || playerMon == null || wildMon == null) return;
        if (encounterPrefab == null)
        {
            Debug.LogError("[CombatService] Falta encounterPrefab.");
            return;
        }

        // Canvas y controles
        SetCombatCanvas(true);
        PrepareForBattleUI();

        // Resolver el CreatureBehavior raíz del salvaje para posible destrucción al final
        pendingWildToDestroy =
            (wildMonTf ? (wildMonTf.GetComponentInParent<CreatureBehavior>(true) ??
                          wildMonTf.GetComponent<CreatureBehavior>()) : null);

        // Instanciar controlador del encuentro
        var go = Instantiate(encounterPrefab);
        activeEncounter = go.GetComponent<EncounterController>() ?? go.AddComponent<EncounterController>();
        activeEncounter.ApplyConfig(defaultOffsetFromCenter, defaultPlayerRingRadius);

        // Forzar ItemSelector a modo Pokéballs y bloquear alternancia
        var selector = FindAnyObjectByType<ItemSelectorUI>();
        if (selector != null)
        {
            selector.SetCaptureLock(true);
            selector.SetMode(SelectorMode.Pokeball);
            selector.RefreshBalls();
        }

        captureInProgress = false;

        // Comenzar encuentro con callback con resultado
        activeEncounter.Begin(playerMonTf, playerMon, wildMonTf, wildMon, onEnd: (result) =>
        {
            // Desbloquear selector
            selector?.SetCaptureLock(false);
            captureInProgress = false;

            // Despawn si procede
            if ((result == EncounterResult.EnemyFainted || result == EncounterResult.Capture) && pendingWildToDestroy)
            {
                Destroy(pendingWildToDestroy.gameObject);
            }
            pendingWildToDestroy = null;

            activeEncounter = null;

            // UI y controles
            SetCombatCanvas(false);
            RestorePlayerControls();
        });
    }

    /// <summary>Finaliza forzado (cambio de escena, huida manual, etc.). No destruye salvaje.</summary>
    public void ForceEndEncounter()
    {
        if (!activeEncounter)
        {
            SetCombatCanvas(false);
            RestorePlayerControls();
            return;
        }

        activeEncounter.ForceEnd();
        activeEncounter = null;
        captureInProgress = false;

        var selector = FindAnyObjectByType<ItemSelectorUI>();
        selector?.SetCaptureLock(false);

        pendingWildToDestroy = null; // no destruir al forzar salida

        SetCombatCanvas(false);
        RestorePlayerControls();
    }

    // ---------- Señales usadas por la Pokéball ----------
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

    // ---------- Utilidades privadas ----------
    private void SetCombatCanvas(bool on)
    {
        if (!combatCanvasRoot) return;
        if (combatCanvasRoot.activeSelf != on) combatCanvasRoot.SetActive(on);
    }

    private static void PrepareForBattleUI()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) pc.EnableControls(false);
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
