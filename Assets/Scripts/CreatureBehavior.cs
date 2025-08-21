using System.Collections;
using UnityEngine;

public class CreatureBehavior : MonoBehaviour
{
    public float wanderRadius = 5f;
    public float wanderInterval = 3f;
    public float moveSpeed = 2f;
    public float detectionRadius = 5f;

    [HideInInspector] public Transform player;
    [HideInInspector] public Vector3 spawnPoint;

    private Coroutine currentAction;
    private Coroutine behaviorLoop; // ← controlamos el loop para reanudarlo tras reactivación
    private bool isMoving = false;

    public PokemonInstance pokemonInstance { get; private set; }

    private CombatContact contactHook;

    // --- NUEVO ---
    private bool isInCombat = false;
    public bool IsInCombat => isInCombat;

    void OnEnable()
    {
        // Al reactivar el GO (p.ej. tras captura fallida), Start NO se vuelve a llamar.
        // Reaseguramos referencias mínimas y reanudamos el bucle de comportamiento.
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        EnsureBehaviorLoopRunning();
    }

    void OnDisable()
    {
        // Al desactivar el GO se paran las corutinas; limpiamos punteros para dejar estado consistente.
        if (currentAction != null) { StopCoroutine(currentAction); currentAction = null; }
        if (behaviorLoop != null) { StopCoroutine(behaviorLoop); behaviorLoop = null; }
        isMoving = false;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        spawnPoint = transform.position;
        contactHook = GetComponent<CombatContact>();
        EnsureBehaviorLoopRunning(); // ← en vez de StartCoroutine directo
    }

    void Update()
    {
        if (isInCombat) return; // --- BLOQUEO en combate ---

        if (!isMoving && pokemonInstance?.species != null && player != null &&
            pokemonInstance.species.behaviorType == PokemonBehaviorType.Friendly)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer < detectionRadius) RotateTowardsPlayer();
        }
    }

    private void EnsureBehaviorLoopRunning()
    {
        if (behaviorLoop == null)
            behaviorLoop = StartCoroutine(BehaviorLoop());
    }

    IEnumerator BehaviorLoop()
    {
        while (true)
        {
            if (isInCombat) { yield return null; continue; } // --- BLOQUEO en combate ---

            if (pokemonInstance?.species == null ||
                pokemonInstance.species.behaviorType == PokemonBehaviorType.Idle)
            {
                yield return null;
                continue;
            }

            float waitTime = Random.Range(1f, wanderInterval);

            if (!isMoving &&
                pokemonInstance.species.behaviorType == PokemonBehaviorType.Friendly &&
                player != null && Vector3.Distance(transform.position, player.position) < detectionRadius)
            {
                waitTime += 2f;
            }

            // Espera antes de decidir siguiente destino
            yield return new WaitForSeconds(waitTime);

            float distanceToPlayer = player ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
            float distanceToSpawn = Vector3.Distance(transform.position, spawnPoint);

            if (distanceToSpawn > wanderRadius)
            {
                SetDestination(spawnPoint);
            }
            else if (pokemonInstance.species.behaviorType == PokemonBehaviorType.Aggressive && distanceToPlayer < detectionRadius)
            {
                SetDestination(player.position);
            }
            else if (pokemonInstance.species.behaviorType == PokemonBehaviorType.Friendly)
            {
                Vector3 randomOffset = Random.insideUnitSphere * wanderRadius; randomOffset.y = 0;
                SetDestination(spawnPoint + randomOffset);
            }
        }
    }

    void SetDestination(Vector3 target)
    {
        if (isInCombat) return; // no moverse en combate
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(MoveTo(target));
    }

    IEnumerator MoveTo(Vector3 target)
    {
        isMoving = true;
        while (!isInCombat && Vector3.Distance(transform.position, target) > 0.1f)
        {
            Vector3 direction = (target - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
            }
            yield return null;
        }
        isMoving = false;
    }

    void RotateTowardsPlayer()
    {
        if (player == null) return;
        Vector3 direction = (player.position - transform.position); direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180f * Time.deltaTime);
        }
    }

    public float GetCatchRate()
    {
        if (pokemonInstance != null && pokemonInstance.species != null)
            return pokemonInstance.species.catchRate;
        Debug.LogWarning("pokemonInstance/species es null al obtener la tasa de captura.");
        return 0f;
    }

    public void SetPokemon(PokemonInstance instance)
    {
        pokemonInstance = instance;

        // ✅ Garantiza que tenga movimientos para su nivel (si viene “vacío”)
        pokemonInstance?.EnsureMovesForCurrentLevel();

        if (contactHook == null) contactHook = GetComponent<CombatContact>() ?? gameObject.AddComponent<CombatContact>();
        contactHook.Bind(transform, pokemonInstance, wild: true);

        // Por si el GO se creó/activó en runtime:
        EnsureBehaviorLoopRunning();
    }

    public void InitializeFromInstance(PokemonInstance instance)
    {
        pokemonInstance = instance;

        // ✅ Garantiza movimientos si vienen vacíos
        pokemonInstance?.EnsureMovesForCurrentLevel();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        spawnPoint = transform.position;

        if (contactHook == null) contactHook = GetComponent<CombatContact>() ?? gameObject.AddComponent<CombatContact>();
        contactHook.Bind(transform, pokemonInstance, wild: true);

        EnsureBehaviorLoopRunning(); // ← evita duplicados y garantiza reanudación
    }

    public PokemonInstance GetPokemonInstance() => pokemonInstance;

    // --- NUEVO: Pausar/Reanudar modo combate ---
    public void SetCombatMode(bool active)
    {
        isInCombat = active;

        if (active)
        {
            // parar cualquier movimiento en curso
            if (currentAction != null) { StopCoroutine(currentAction); currentAction = null; }
            isMoving = false;
        }
        else
        {
            // al salir de combate, aseguramos que el loop está corriendo
            EnsureBehaviorLoopRunning();
        }
    }
}
