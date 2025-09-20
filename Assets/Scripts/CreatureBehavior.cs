using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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

    public PokemonInstance pokemonInstance;

    private Coroutine coWanderLoop;
    private Coroutine coCurrentMove;
    private bool isInCombat;
    private bool isMoving;

    public PokemonInstance GetPokemonInstance() => pokemonInstance;
    public void SetPokemon(PokemonInstance p) { pokemonInstance = p; }

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
        spawnPoint = transform.position;
    }

    void OnEnable()
    {
        EnsureOutsideRingImmediate();
        if (!isInCombat) EnsureBehaviorLoopRunning();
    }

    void Start()
    {
        EnsureOutsideRingImmediate();
        if (!isInCombat) EnsureBehaviorLoopRunning();
    }

    void LateUpdate()
    {
        // Arrastres manuales u otras fuerzas: sacar si quedó dentro.
        if (!isInCombat) EnsureOutsideRingImmediate();
    }

    void OnDisable()
    {
        StopWanderLoop();
        StopCurrentMove();
    }

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
            EnsureOutsideRingImmediate();
            EnsureBehaviorLoopRunning();
        }
    }

    public void EnsureBehaviorLoopRunning()
    {
        if (isInCombat) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (coWanderLoop == null) coWanderLoop = StartCoroutine(CoWanderLoop());
    }

    IEnumerator CoWanderLoop()
    {
        while (!isInCombat && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            var origin = GetWanderOrigin();
            var next = GetRandomPointNear(origin, wanderRadius);
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

    IEnumerator CoMoveTo(Vector3 target)
    {
        isMoving = true;

        // Nunca dentro del anillo
        if (EncounterController.IsRingActive && EncounterController.IsInsideRing(target))
            target = ForceOutsidePoint(target);

        if (agent && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.SetDestination(SampleOnNavmesh(target));
            if (animator) animator.SetBool("IsMoving", true);

            while (!isInCombat && agent.enabled && agent.isOnNavMesh &&
                   !agent.pathPending && Vector3.Distance(transform.position, target) > 0.25f)
            {
                if (EncounterController.IsRingActive && EncounterController.IsInsideRing(agent.destination))
                    agent.SetDestination(SampleOnNavmesh(ForceOutsidePoint(agent.destination)));
                yield return null;
            }
            agent.ResetPath();
        }
        else
        {
            while (!isInCombat && Vector3.Distance(transform.position, target) > 0.25f)
            {
                if (EncounterController.IsRingActive && EncounterController.IsInsideRing(target))
                    target = ForceOutsidePoint(target);

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
        if (coWanderLoop != null) { StopCoroutine(coWanderLoop); coWanderLoop = null; }
    }

    private void StopCurrentMove()
    {
        if (coCurrentMove != null) { StopCoroutine(coCurrentMove); coCurrentMove = null; }
    }

    // Origen de vagado mientras el anillo esté activo.
    private Vector3 GetWanderOrigin()
    {
        if (!EncounterController.IsRingActive) return spawnPoint;

        // Si el spawn está dentro del anillo, vagar alrededor de la posición actual,
        // no alrededor del spawn (evita el “tirón” al borde).
        if (EncounterController.IsInsideRing(spawnPoint))
        {
            // Si por cualquier motivo estoy dentro, uso un punto empujado hacia fuera.
            return EncounterController.IsInsideRing(transform.position)
                   ? ForceOutsidePoint(transform.position)
                   : transform.position;
        }

        // Spawn fuera: usar spawn, pero si me colé dentro, sacar primero.
        if (EncounterController.IsInsideRing(transform.position))
            return ForceOutsidePoint(transform.position);

        return spawnPoint;
    }

    private Vector3 GetRandomPointNear(Vector3 origin, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            var rand = origin + Random.insideUnitSphere * radius;
            rand.y = origin.y;

            if (EncounterController.IsRingActive && EncounterController.IsInsideRing(rand))
                rand = ForceOutsidePoint(rand);

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

    private void EnsureOutsideRingImmediate()
    {
        if (!EncounterController.IsRingActive) return;
        if (!EncounterController.IsInsideRing(transform.position)) return;

        var outside = ForceOutsidePoint(transform.position);
        bool warped = false;

        if (agent && agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(outside, out var hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                warped = true;
            }
        }
        if (!warped) transform.position = outside;

        var c = EncounterController.RingCenterS;
        var dir = (transform.position - c); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private Vector3 SampleOnNavmesh(Vector3 p)
    {
        if (agent && agent.isOnNavMesh && NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas))
            return hit.position;
        return p;
    }

    private static Vector3 ForceOutsidePoint(Vector3 from)
    {
        var c = EncounterController.RingCenterS;
        float r = EncounterController.RingRadiusS + 0.75f;
        var v = from - c; v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) v = Random.insideUnitSphere;
        v.y = 0f; v.Normalize();
        var outPos = c + v * r;
        outPos.y = from.y;
        return outPos;
    }
}
