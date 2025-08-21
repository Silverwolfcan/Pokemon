using UnityEngine;

/// Punto de entrada del combate. Instancia EncounterController y activa/desactiva la UI de combate.
public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject encounterPrefab; // Prefab con EncounterController
    [Tooltip("Canvas raíz que contiene CombatUIController (así lo activamos al entrar en combate).")]
    [SerializeField] private GameObject combatUIRoot;

    [Header("Config por defecto para cada encuentro")]
    [SerializeField] private float defaultOffsetFromCenter = 2.5f;
    [SerializeField] private float defaultPlayerRingRadius = 10f;

    private EncounterController activeEncounter;

    // --- Control de captura por turno ---
    private bool captureAttemptActive = false;

    public bool IsInBattle => activeEncounter != null;
    public bool IsInEncounter => activeEncounter != null;
    public EncounterController ActiveEncounter => activeEncounter;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Arranca un encuentro 1v1 entre el pokémon del jugador y uno salvaje.
    public void StartEncounter(Transform playerMonTf, PokemonInstance playerMon,
                               Transform wildMonTf, PokemonInstance wildMon)
    {
        if (IsInEncounter)
        {
            Debug.LogWarning("[CombatService] Ya hay un combate activo.");
            return;
        }

        if (encounterPrefab == null)
        {
            Debug.LogError("[CombatService] Falta encounterPrefab.");
            return;
        }

        // Instanciar Encounter
        var go = Instantiate(encounterPrefab);
        var enc = go.GetComponent<EncounterController>();
        if (!enc)
        {
            Debug.LogError("[CombatService] El prefab no contiene EncounterController.");
            Destroy(go);
            return;
        }

        activeEncounter = enc;

        // Aplicar la config por defecto
        activeEncounter.ApplyConfig(defaultOffsetFromCenter, defaultPlayerRingRadius);

        // Asegurar UI activa ANTES de iniciar, para que CombatUIController se inicialice
        if (combatUIRoot != null && !combatUIRoot.activeSelf)
            combatUIRoot.SetActive(true);

        // Comenzar el encuentro
        activeEncounter.Begin(playerMonTf, playerMon, wildMonTf, wildMon, onEnd: () =>
        {
            // Al terminar, ocultamos la UI y limpiamos referencia/flags
            if (combatUIRoot != null && combatUIRoot.activeSelf)
                combatUIRoot.SetActive(false);

            captureAttemptActive = false;
            activeEncounter = null;
        });
    }

    /// Fuerza finalizar el combate activo (por ejemplo, al cargar escena).
    public void ForceEndEncounter()
    {
        if (!activeEncounter) return;

        activeEncounter.ForceEnd();

        if (combatUIRoot != null && combatUIRoot.activeSelf)
            combatUIRoot.SetActive(false);

        captureAttemptActive = false;
        activeEncounter = null;
    }

    // =======================
    // Capture flow helpers
    // =======================

    /// Llamar desde la Pokéball al lanzarse. Devuelve false si NO se permite (ya hay un intento en curso).
    public bool BeginCaptureAttempt()
    {
        if (!IsInEncounter) return true; // fuera de combate no nos metemos
        if (captureAttemptActive) return false;
        captureAttemptActive = true;
        return true;
    }

    /// Llamar cuando la captura termine con ÉXITO.
    public void NotifyCaptureSuccess()
    {
        if (!activeEncounter) { captureAttemptActive = false; return; }
        captureAttemptActive = false;
        activeEncounter.EndByCapture();
    }

    /// Llamar cuando la captura termine con FALLO. Consume el turno del jugador.
    public void NotifyCaptureFailed()
    {
        if (!activeEncounter) { captureAttemptActive = false; return; }
        captureAttemptActive = false;

        var tc = activeEncounter.TurnController;
        if (tc != null)
            tc.ConsumePlayerTurn(); // pasa al turno del enemigo

        // La UI de combate ya está oculta en modo captura; los controles siguen bloqueados por Encounter.
    }
}
