using UnityEngine;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;

    [Header("Rotación de cámara")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;
    public float minY = -40f;
    public float maxY = 80f;

    [Header("Zoom y apuntado")]
    public Camera cam;
    public float zoomFov = 35f;
    public float zoomSpeed = 10f;
    public GameObject crosshair;

    [Header("Lanzamiento")]
    public float maxThrowDistance = 20f;
    public GameObject pokeballPrefab;
    public Transform throwOrigin;
    public float throwCooldown = 2f;
    private float lastThrowTime = -Mathf.Infinity;

    [Header("UI de Pokéballs y Pokémon")]
    public ItemSelectorUI itemSelector;

    [Header("Raycast Layers")]
    public LayerMask pokemonPlayerMask;
    public LayerMask movementMask;

    [Header("Animación")]
    public Animator animator; // arrástralo en el inspector
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIsAiming = Animator.StringToHash("IsAiming");
    private static readonly int AnimThrow = Animator.StringToHash("Throw");

    [Header("Controles de disparo")]
    [Tooltip("Permite lanzar con clic IZQUIERDO mientras apuntas.")]
    public bool useLeftClickToThrow = true;
    [Tooltip("Permite lanzar al SOLTAR clic DERECHO (mientras apuntas).")]
    public bool useRightReleaseToThrow = true;

    private CharacterController character;
    private float rotX, rotY;
    private bool controlsEnabled = true;

    private bool isAiming = false;
    private float defaultFov = 60f;
    private bool isThrowing = false; // mientras el clip Throw está en curso

    private PlayerCreatureBehavior activeSummonedPokemon;
    private string activePokemonID;

    void Awake()
    {
        character = GetComponent<CharacterController>();

        if (!cam) cam = Camera.main;
        if (cam) defaultFov = cam.fieldOfView;

        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;

        if (crosshair) crosshair.SetActive(false);
    }

    void Start()
    {
        EnsureAnimatorLayers();
    }

    void Update()
    {
        if (!controlsEnabled)
        {
            SetAiming(false);
            UpdateAnimatorSpeed(0f);
            return;
        }

        ValidateActiveSummon();

        // Apuntado con RMB mantenido
        SetAiming(Input.GetMouseButton(1));

        HandleMovement();

        // Disparo: LMB down o RMB up (configurable), solo si estamos apuntando
        bool leftFire = useLeftClickToThrow && Input.GetMouseButtonDown(0);
        bool rightFire = useRightReleaseToThrow && Input.GetMouseButtonUp(1); // soltar botón derecho

        if (isAiming && (leftFire || rightFire))
        {
            if (itemSelector != null && itemSelector.CurrentMode == SelectorMode.Pokeball)
            {
                TryTriggerThrow(); // trigger de animación; ReleaseBall hará el spawn real
            }
            else
            {
                Debug.Log("[PlayerController] No estás en modo Pokéballs. Pulsa R para cambiar.");
                if (activeSummonedPokemon == null) SummonPokemon();
                else HandleSummonedPokemonAction();
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
            ToggleSelectorMode();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;
        Vector3 moveVel = moveDir * moveSpeed;

        // CharacterController activo
        if (character != null && character.enabled && character.gameObject.activeInHierarchy)
        {
            character.SimpleMove(moveVel);
        }
        else
        {
            // Fallback por si no tienes CC
            transform.position += moveVel * Time.deltaTime;
        }

        // Rotación cámara
        rotX += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotY = Mathf.Clamp(rotY, minY, maxY);

        transform.rotation = Quaternion.Euler(0, rotX, 0);
        if (cameraTransform) cameraTransform.localRotation = Quaternion.Euler(rotY, 0, 0);

        // FOV apuntando
        if (cam)
        {
            float target = isAiming ? zoomFov : defaultFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, Time.deltaTime * zoomSpeed);
        }

        // Velocidad horizontal para el Animator
        float speedForAnim = moveVel.magnitude;
        if (character != null && character.enabled && character.gameObject.activeInHierarchy)
        {
            Vector3 vel = character.velocity; vel.y = 0f;
            speedForAnim = vel.magnitude;
        }
        UpdateAnimatorSpeed(speedForAnim);
    }

    private void UpdateAnimatorSpeed(float speed)
    {
        if (animator) animator.SetFloat(AnimSpeed, speed);
    }

    private void SetAiming(bool aiming)
    {
        if (isAiming == aiming) return;
        isAiming = aiming;

        if (crosshair) crosshair.SetActive(isAiming);
        if (animator) animator.SetBool(AnimIsAiming, isAiming);
    }

    private void TryTriggerThrow()
    {
        if (Time.time - lastThrowTime < throwCooldown)
        {
            Debug.Log("[PlayerController] Cooldown de lanzamiento.");
            return;
        }

        if (animator == null)
        {
            // Sin animador: lanzar directamente
            TryThrowPokeball();
            return;
        }

        if (isThrowing) return;

        animator.ResetTrigger(AnimThrow);
        animator.SetTrigger(AnimThrow);
        isThrowing = true; // se libera en ReleaseBall()
    }

    /// <summary>Llamado por el Animation Event del clip Throw (nombre exacto del evento: ReleaseBall)</summary>
    public void ReleaseBall()
    {
        TryThrowPokeball();
        isThrowing = false;
    }

    private void TryThrowPokeball()
    {
        if (Time.time - lastThrowTime < throwCooldown) return;

        var entry = itemSelector != null ? itemSelector.GetSelectedBallEntry() : null;
        if (entry == null || entry.item == null)
        {
            Debug.LogWarning("[PlayerController] No hay ninguna Pokéball seleccionada.");
            return;
        }
        if (entry.quantity <= 0)
        {
            Debug.Log("[PlayerController] Sin unidades de: " + entry.item.name);
            return;
        }
        if (!itemSelector.TryConsumeSelectedBall())
        {
            Debug.Log("[PlayerController] No se pudo consumir Pokéball.");
            return;
        }

        lastThrowTime = Time.time;

        Vector3 origin = throwOrigin ? throwOrigin.position : (cam ? cam.transform.position : transform.position);
        Vector3 dir = (cam ? cam.transform.forward : transform.forward);

        Vector3 end = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance, movementMask))
            end = hit.point;

        var ballData = entry.item as PokeballData;
        var ball = Instantiate(pokeballPrefab, origin, Quaternion.identity).GetComponent<Ball>();
        ball.Initialize(origin, end, 4f, 1.5f, ballData);
    }

    private void SummonPokemon()
    {
        var pi = itemSelector != null ? itemSelector.GetCurrentPokemon() : null;
        if (pi == null) return;
        if (pi.currentHP <= 0) { Debug.Log("[PlayerController] Pokémon debilitado."); return; }

        Vector3 origin = cam ? cam.transform.position : transform.position;
        Vector3 dir = cam ? cam.transform.forward : transform.forward;
        Vector3 spawnPoint = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance)) spawnPoint = hit.point;

        GameObject go = Instantiate(pi.species.prefab, spawnPoint, Quaternion.identity);
        var pcb = go.GetComponent<PlayerCreatureBehavior>() ?? go.AddComponent<PlayerCreatureBehavior>();
        pcb.playerTransform = transform;
        pcb.AssignPokemonInstance(pi);

        activeSummonedPokemon = pcb;
        activePokemonID = pi.UniqueID;

        itemSelector?.RefreshCapturedPokemon();
    }

    private void HandleSummonedPokemonAction()
    {
        Ray ray = cam ? cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2))
                      : new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxThrowDistance, pokemonPlayerMask))
        {
            if (activeSummonedPokemon != null)
            {
                Destroy(activeSummonedPokemon.gameObject);
                ClearActiveSummonRef();
            }
            return;
        }
        // Aquí podrías dar órdenes al Pokémon activo.
    }

    private void ToggleSelectorMode()
    {
        if (itemSelector == null) return;
        var newMode = itemSelector.CurrentMode == SelectorMode.Pokeball ? SelectorMode.Pokemon : SelectorMode.Pokeball;
        itemSelector.SetMode(newMode);
    }

    public void EnableControls(bool enabled)
    {
        controlsEnabled = enabled;
        if (!controlsEnabled) SetAiming(false);
    }

    private void EnsureAnimatorLayers()
    {
        if (animator == null) return;
        int aim = animator.GetLayerIndex("Aim");
        if (aim >= 0) animator.SetLayerWeight(aim, 1f);
        int react = animator.GetLayerIndex("Reactions");
        if (react >= 0) animator.SetLayerWeight(react, 1f);
    }

    // ---- helpers activo ----
    public PokemonInstance GetActivePokemon()
    {
        if (string.IsNullOrEmpty(activePokemonID)) return null;
        var party = PokemonStorageManager.Instance.PlayerParty?.ToList();
        return party?.FirstOrDefault(p => p.UniqueID == activePokemonID);
    }

    private void ValidateActiveSummon()
    {
        if (activeSummonedPokemon == null)
        {
            var p = GetActivePokemon();
            if (p != null && p.currentHP <= 0) ClearActiveSummonRef();
            return;
        }

        if (!activeSummonedPokemon.gameObject || !activeSummonedPokemon.gameObject.activeInHierarchy)
        {
            ClearActiveSummonRef();
            return;
        }

        var pi = GetActivePokemon();
        if (pi == null || pi.currentHP <= 0) ClearActiveSummonRef();
    }

    private void ClearActiveSummonRef()
    {
        activeSummonedPokemon = null;
        activePokemonID = null;
        itemSelector?.RefreshCapturedPokemon();
    }
}
