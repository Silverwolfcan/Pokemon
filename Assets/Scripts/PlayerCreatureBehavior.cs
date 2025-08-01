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

    private void Awake()
    {
        // ——— ASIGNAR LA CAPA “PokemonPlayer” al gameObject y propagarlo a cada objeto hijo ———
        int playerLayer = LayerMask.NameToLayer("PokemonPlayer");
        gameObject.layer = playerLayer;
        foreach (Transform child in transform)
        {
            child.gameObject.layer = playerLayer;
        }

        // ——— FIJAR CollisionDetectionMode A CONTINUOUS ———
        // Para evitar que los raycasts "atraviesen" el Rigidbody
        var rigs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigs)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Si no tiene NavMeshAgent, lo añadimos aquí
        if (!TryGetComponent<NavMeshAgent>(out agent))
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        // Configuración por defecto
        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = stoppingDistance;  // usa la misma que tu variable
        agent.autoBraking = true;
    }

    private void Update()
    {
        switch (currentState)
        {
            case CreatureState.FollowingPlayer:
                FollowPlayer();
                break;
            case CreatureState.MovingToTarget:
                CheckArrival();
                CheckIfTooFarFromPlayer();
                break;
        }
    }

    void FollowPlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > followDistance)
            agent.SetDestination(playerTransform.position);
        else
            agent.ResetPath();
    }

    void CheckArrival()
    {
        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
            currentState = CreatureState.FollowingPlayer;
    }

    void CheckIfTooFarFromPlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > maxDistanceFromPlayer)
        {
            //Debug.Log("La criatura está demasiado lejos del jugador. Volviendo a seguir.");
            currentState = CreatureState.FollowingPlayer;
        }
    }

    public void MoveToPoint(Vector3 point)
    {
        agent.SetDestination(point);
        currentState = CreatureState.MovingToTarget;
    }

    public void AssignPokemonInstance(PokemonInstance instance)
    {
        PokemonInstance = instance;
    }

    public enum CreatureState
    {
        FollowingPlayer,
        MovingToTarget
    }
}
