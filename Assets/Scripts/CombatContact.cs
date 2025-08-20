using UnityEngine;

/// Único hook de arranque de combate. Intenta autoconfigurarse leyendo:
/// - PlayerCreatureBehavior.PokemonInstance  => jugador
/// - CreatureBehavior.pokemonInstance        => salvaje
/// También ofrece Bind(...) para forzar el bindeo desde Spawner o al lanzar el Pokémon.
[DefaultExecutionOrder(50)]
public class CombatContact : MonoBehaviour
{
    [Tooltip("Tiempo mínimo entre intentos de iniciar combate (evita doble disparo).")]
    [SerializeField] private float debounce = 0.15f;

    private Transform worldTf;
    private PokemonInstance instance;
    private bool isWild;
    private float lastTry = -999f;

    public void Bind(Transform tf, PokemonInstance inst, bool wild)
    {
        worldTf = tf != null ? tf : transform;
        instance = inst;
        isWild = wild;
    }

    private void Awake()
    {
        worldTf = transform;
        // Intento de autodescubrimiento (por si ya estaba la instancia)
        AutoBindIfPossible();
    }

    private void OnEnable()
    {
        // Reintento por si Spawner/Assign llega después del Awake
        if (instance == null) AutoBindIfPossible();
    }

    private void AutoBindIfPossible()
    {
        // Jugador
        var pcb = GetComponent<PlayerCreatureBehavior>();
        if (pcb != null && pcb.PokemonInstance != null)
        {
            Bind(transform, pcb.PokemonInstance, wild: false);
            return;
        }
        // Salvaje
        var wild = GetComponent<CreatureBehavior>();
        if (wild != null && wild.pokemonInstance != null)
        {
            Bind(transform, wild.pokemonInstance, wild: true);
            return;
        }
    }

    private void OnTriggerEnter(Collider other) => TryStart(other.gameObject);
    private void OnCollisionEnter(Collision col) => TryStart(col.gameObject);

    private void TryStart(GameObject otherGo)
    {
        if (Time.time - lastTry < debounce) return; lastTry = Time.time;

        if (CombatService.Instance == null || CombatService.Instance.IsInEncounter) return;

        // Reintentar autovincular justo antes de iniciar (por si llegó tarde)
        if (instance == null) AutoBindIfPossible();
        if (instance == null) return;

        var other = otherGo.GetComponentInParent<CombatContact>();
        if (other == null)
        {
            // intenta subir un poco en la jerarquía
            var t = otherGo.transform.parent;
            while (t != null && other == null) { other = t.GetComponent<CombatContact>(); t = t.parent; }
        }
        if (other == null || other.instance == null) return;

        // Sólo jugador vs salvaje
        if (this.isWild == other.isWild) return;

        var player = this.isWild ? other : this;
        var wild = this.isWild ? this : other;

        CombatService.Instance.StartEncounter(
            player.worldTf ? player.worldTf : player.transform, player.instance,
            wild.worldTf ? wild.worldTf : wild.transform, wild.instance
        );
    }
}
