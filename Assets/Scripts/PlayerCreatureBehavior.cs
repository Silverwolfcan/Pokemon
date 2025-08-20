using UnityEngine;
using UnityEngine.AI;

public class PlayerCreatureBehavior : MonoBehaviour
{
    public Transform playerTransform;
    public float followDistance = 3f;
    public float stoppingDistance = 0.5f;
    public float maxDistanceFromPlayer = 10f;
    public LayerMask groundMask;

    private NavMeshAgent agent;
    private CreatureState currentState = CreatureState.FollowingPlayer;

    public PokemonInstance PokemonInstance { get; private set; }

    private CombatContact contactHook;

    // --- NUEVO ---
    private bool isInCombat = false;
    public bool IsInCombat => isInCombat;

    private void Awake()
    {
        int playerLayer = LayerMask.NameToLayer("PokemonPlayer");
        gameObject.layer = playerLayer;
        foreach (Transform child in transform) child.gameObject.layer = playerLayer;

        var rigs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigs) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (!TryGetComponent<NavMeshAgent>(out agent))
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;

        contactHook = GetComponent<CombatContact>() ?? gameObject.AddComponent<CombatContact>();

        if (PokemonInstance != null) contactHook.Bind(transform, PokemonInstance, wild: false);
    }

    private void Update()
    {
        // --- BLOQUEO en combate ---
        if (isInCombat) return;

        switch (currentState)
        {
            case CreatureState.FollowingPlayer: FollowPlayer(); break;
            case CreatureState.MovingToTarget: CheckArrival(); CheckIfTooFarFromPlayer(); break;
        }
    }

    void FollowPlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > followDistance) agent.SetDestination(playerTransform.position);
        else agent.ResetPath();
    }

    void CheckArrival()
    {
        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
            currentState = CreatureState.FollowingPlayer;
    }

    void CheckIfTooFarFromPlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > maxDistanceFromPlayer) currentState = CreatureState.FollowingPlayer;
    }

    public void MoveToPoint(Vector3 point)
    {
        if (isInCombat) return; // no aceptar órdenes en combate
        agent.SetDestination(point);
        currentState = CreatureState.MovingToTarget;
    }

    public void AssignPokemonInstance(PokemonInstance instance)
    {
        PokemonInstance = instance;
        if (contactHook == null) contactHook = GetComponent<CombatContact>() ?? gameObject.AddComponent<CombatContact>();
        contactHook.Bind(transform, PokemonInstance, wild: false);
    }

    // --- NUEVO: Pausar/Reanudar modo combate ---
    public void SetCombatMode(bool active)
    {
        isInCombat = active;

        if (agent != null)
        {
            if (active)
            {
                agent.ResetPath();
                // Deshabilitamos el NavMeshAgent para que no intente recolocar la posición
                if (agent.enabled) agent.enabled = false;
            }
            else
            {
                // Rehabilitamos y sincronizamos
                if (!agent.enabled) agent.enabled = true;
                agent.Warp(transform.position);
                currentState = CreatureState.FollowingPlayer;
            }
        }
    }

    public enum CreatureState { FollowingPlayer, MovingToTarget }
}
