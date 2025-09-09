using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// IA de criatura salvaje. Nunca inicia corutinas si el GO está inactivo.
[DefaultExecutionOrder(10)]
public class CreatureBehavior : MonoBehaviour
{
    [Header("Movimiento libre")]
    [SerializeField] private float wanderRadius = 5f;
    [SerializeField] private float wanderInterval = 3f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Detección")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Refs opcionales")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [HideInInspector] public Transform player;
    [HideInInspector] public Vector3 spawnPoint;

    // Datos de juego (API pública requerida por Ball/Spawner/CombatContact)
    [Tooltip("Instancia del Pokémon que representa esta criatura en el mundo.")]
    public PokemonInstance pokemonInstance;

    // Estado
    private Coroutine coWanderLoop;
    private Coroutine coCurrentMove;
    private bool isInCombat;
    private bool isMoving;

    // --- API COMPATIBLE ---
    public PokemonInstance GetPokemonInstance() => pokemonInstance;
    public void SetPokemon(PokemonInstance p) { pokemonInstance = p; }

    private void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
        spawnPoint = transform.position;
    }

    private void OnEnable()
    {
        if (!isInCombat) EnsureBehaviorLoopRunning();
    }

    private void Start()
    {
        if (!isInCombat) EnsureBehaviorLoopRunning();
    }

    private void OnDisable()
    {
        StopWanderLoop();
        StopCurrentMove();
    }

    // --- API pública ---
    public void SetCombatMode(bool active)
    {
        isInCombat = active;
        if (active)
        {
            StopCurrentMove();
            StopWanderLoop();
            if (agent) agent.ResetPath();
            isMoving = false;
        }
        else
        {
            EnsureBehaviorLoopRunning();
        }
    }

    public void EnsureBehaviorLoopRunning()
    {
        if (isInCombat) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (coWanderLoop == null) coWanderLoop = StartCoroutine(CoWanderLoop());
    }

    // --- Wander ---
    private IEnumerator CoWanderLoop()
    {
        while (!isInCombat && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            var next = GetRandomPointNear(spawnPoint, wanderRadius);
            DoMove(next);

            float t = 0f;
            while (t < wanderInterval && !isInCombat)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
        coWanderLoop = null;
    }

    private void DoMove(Vector3 target)
    {
        StopCurrentMove();
        coCurrentMove = StartCoroutine(CoMoveTo(target));
    }

    private IEnumerator CoMoveTo(Vector3 target)
    {
        isMoving = true;

        if (agent && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.SetDestination(target);
            if (animator) animator.SetBool("IsMoving", true);

            while (!isInCombat && agent.enabled && agent.isOnNavMesh &&
                   !agent.pathPending && Vector3.Distance(transform.position, target) > 0.2f)
            {
                yield return null;
            }
            agent.ResetPath();
        }
        else
        {
            while (!isInCombat && Vector3.Distance(transform.position, target) > 0.2f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        if (animator) animator.SetBool("IsMoving", false);
        isMoving = false;
        coCurrentMove = null;
    }

    private void StopWanderLoop()
    {
        if (coWanderLoop != null)
        {
            StopCoroutine(coWanderLoop);
            coWanderLoop = null;
        }
    }

    private void StopCurrentMove()
    {
        if (coCurrentMove != null)
        {
            StopCoroutine(coCurrentMove);
            coCurrentMove = null;
        }
    }

    // --- Util ---
    private Vector3 GetRandomPointNear(Vector3 origin, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            var rand = origin + Random.insideUnitSphere * radius;
            rand.y = origin.y;

            if (agent && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(rand, out var hit, 1.5f, NavMesh.AllAreas))
                    return hit.position;
            }
            else
            {
                if (Physics.Raycast(rand + Vector3.up * 10f, Vector3.down, out var rh, 20f, groundMask))
                    return rh.point;
            }
        }
        return origin;
    }
}
