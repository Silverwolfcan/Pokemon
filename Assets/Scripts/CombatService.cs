using UnityEngine;

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject encounterPrefab; // Prefab con EncounterController

    private EncounterController activeEncounter;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsInEncounter => activeEncounter != null;

    /// <summary>
    /// Arranca un combate. Llamar al colisionar tu Pokémon con un salvaje.
    /// </summary>
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

        activeEncounter.Begin(playerMonTf, playerMon, wildMonTf, wildMon, onEnd: () =>
        {
            activeEncounter = null;
        });
    }

    /// <summary>Fuerza finalizar el combate activo (por ejemplo, al cargar escena).</summary>
    public void ForceEndEncounter()
    {
        if (!activeEncounter) return;
        activeEncounter.ForceEnd();
        activeEncounter = null;
    }
}
