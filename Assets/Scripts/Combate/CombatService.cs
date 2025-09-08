using UnityEngine;

/// Servicio central del combate. Instancia EncounterController y expone utilidades
/// de ciclo de vida + señales usadas por la Pokéball durante las capturas.
public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject encounterPrefab; // Prefab con EncounterController

    [Header("Config por defecto (se aplica a cada combate)")]
    [SerializeField] private float defaultOffsetFromCenter = 2.5f;   // distancia desde el centro a cada pokémon
    [SerializeField] private float defaultPlayerRingRadius = 10f;    // radio máximo para que el jugador se aleje del centro

    private EncounterController activeEncounter;
    private bool captureInProgress = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsInEncounter => activeEncounter != null;

    /// <summary>Arranca un combate. Llamar al colisionar tu Pokémon con un salvaje.</summary>
    public void StartEncounter(Transform playerMonTf, PokemonInstance playerMon,
                               Transform wildMonTf, PokemonInstance wildMon)
    {
        if (IsInEncounter || playerMon == null || wildMon == null) return;
        if (encounterPrefab == null)
        {
            Debug.LogError("[CombatService] Falta encounterPrefab.");
            return;
        }

        var go = Instantiate(encounterPrefab);
        activeEncounter = go.GetComponent<EncounterController>();
        if (!activeEncounter) activeEncounter = go.AddComponent<EncounterController>();

        // aplicar configuración en runtime (sobrescribe lo que tenga el prefab)
        activeEncounter.ApplyConfig(defaultOffsetFromCenter, defaultPlayerRingRadius);

        // Forzar ItemSelector a modo Pokéballs y bloquear alternancia (Q) durante TODO el combate
        var selector = FindAnyObjectByType<ItemSelectorUI>();
        if (selector != null)
        {
            selector.SetCaptureLock(true);                    // deshabilita Q
            selector.SetMode(SelectorMode.Pokeball);          // fuerza modo balls
            selector.RefreshBalls();                          // refresca conteo/iconos
        }

        // Reset banderas de captura
        captureInProgress = false;

        // Comienza encuentro; al terminar, restauramos estado de UI y controles
        activeEncounter.Begin(playerMonTf, playerMon, wildMonTf, wildMon, onEnd: () =>
        {
            selector?.SetCaptureLock(false);                  // vuelve a comportamiento normal
            captureInProgress = false;
            activeEncounter = null;

            // Rehabilitar control del jugador y cursor de gameplay
            RestorePlayerControls();
        });
    }

    /// <summary>Fuerza finalizar el combate activo (por ejemplo, al huir o al cargar escena).</summary>
    public void ForceEndEncounter()
    {
        if (!activeEncounter) { RestorePlayerControls(); return; }

        activeEncounter.ForceEnd();
        activeEncounter = null;
        captureInProgress = false;

        var selector = FindAnyObjectByType<ItemSelectorUI>();
        selector?.SetCaptureLock(false);

        // Asegura que el jugador vuelve a tener control tras salir
        RestorePlayerControls();
    }

    // -------------------- Señales usadas por la Pokéball --------------------

    /// <summary>
    /// Marca el inicio de un intento de captura. Devuelve false si no es válido
    /// (no hay combate activo o ya hay un intento en progreso).
    /// </summary>
    public bool BeginCaptureAttempt()
    {
        if (!IsInEncounter) return false;
        if (captureInProgress) return false;
        captureInProgress = true;
        return true;
    }

    /// <summary>Llamar cuando la captura ha tenido éxito.</summary>
    public void NotifyCaptureSuccess()
    {
        if (!IsInEncounter) return;
        captureInProgress = false;
        // Cerrar combate con resultado "Capture".
        activeEncounter.NotifyCaptureSuccess();
    }

    /// <summary>Llamar cuando la captura ha fallado. Consume el turno del jugador.</summary>
    public void NotifyCaptureFailed()
    {
        if (!IsInEncounter) return;
        captureInProgress = false;
        activeEncounter.NotifyCaptureFailed();
    }

    // -------------------- Utilidades privadas --------------------
    private static void RestorePlayerControls()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.EnableControls(true);

        // Cursor a modo gameplay (bloqueado y oculto)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
