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
    [Tooltip("FOV mientras apuntas (click derecho).")]
    public float zoomFov = 35f;
    [Tooltip("Velocidad de interpolación hacia/desde el FOV de zoom.")]
    public float zoomSpeed = 10f;
    [Tooltip("Crosshair que se muestra solo mientras apuntas (opcional).")]
    public GameObject crosshair;

    [Header("Lanzamiento")]
    public float maxThrowDistance = 15f;
    public GameObject pokeballPrefab;
    public Transform throwOrigin;
    public float throwCooldown = 2f; private float lastThrowTime = -Mathf.Infinity;

    [Header("UI de Pokéballs y Pokémon")]
    public ItemSelectorUI itemSelector;

    [Header("Raycast Layers")]
    public LayerMask pokemonPlayerMask;
    public LayerMask movementMask;

    private PlayerCreatureBehavior activeSummonedPokemon;
    private string activePokemonID;

    private CharacterController character;
    private float rotX, rotY;
    private bool controlsEnabled = true;

    // Estado de apuntado
    private bool isAiming = false;
    private float defaultFov = 60f;

    void Awake()
    {
        character = GetComponent<CharacterController>();
        if (!cam) cam = Camera.main;
        if (cam) defaultFov = cam.fieldOfView;

        // crosshair oculto de inicio
        if (crosshair) crosshair.SetActive(false);
    }

    void Update()
    {
        if (!controlsEnabled)
        {
            // Si se deshabilitan controles (p.ej. menú), salimos del modo apuntado.
            SetAiming(false);
            return;
        }

        // Limpiar activo si fue destruido/desactivado o quedó a 0 HP
        ValidateActiveSummon();

        // Actualiza apuntado (manteniendo botón derecho)
        bool aimHeld = Input.GetMouseButton(1);
        SetAiming(aimHeld);

        HandleMovement();

        // Click izquierdo: solo si estamos apuntando
        if (isAiming && Input.GetMouseButtonDown(0))
        {
            if (itemSelector != null && itemSelector.CurrentMode == SelectorMode.Pokeball)
            {
                TryThrowPokeball();
            }
            else
            {
                if (activeSummonedPokemon == null) SummonPokemon();
                else HandleSummonedPokemonAction();
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleSelectorMode();
        }
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v) * moveSpeed;
        if (character) character.SimpleMove(move);

        rotX += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotY = Mathf.Clamp(rotY, minY, maxY);

        transform.rotation = Quaternion.Euler(0, rotX, 0);
        if (cameraTransform) cameraTransform.localRotation = Quaternion.Euler(rotY, 0, 0);

        // Interpolar FOV según estado de apuntado
        if (cam)
        {
            float target = isAiming ? zoomFov : defaultFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, Time.deltaTime * zoomSpeed);
        }
    }

    private void SetAiming(bool aiming)
    {
        if (isAiming == aiming) return;
        isAiming = aiming;

        // Crosshair ON/OFF
        if (crosshair) crosshair.SetActive(isAiming);

        // Si dejamos de apuntar, no hacemos nada más; el FOV vuelve solo por Update()
    }

    void TryThrowPokeball()
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
            Debug.Log("[PlayerController] No te quedan unidades de: " + entry.item.name);
            return;
        }

        if (!itemSelector.TryConsumeSelectedBall())
        {
            Debug.Log("[PlayerController] No se pudo consumir Pokéball.");
            return;
        }

        lastThrowTime = Time.time;

        Vector3 origin = throwOrigin ? throwOrigin.position : cam.transform.position;
        Vector3 dir = cam.transform.forward;

        Vector3 end = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance, movementMask))
            end = hit.point;

        var ballData = entry.item as PokeballData;
        var ball = Instantiate(pokeballPrefab, origin, Quaternion.identity).GetComponent<Ball>();
        ball.Initialize(origin, end, 4f, 1.5f, ballData);
    }

    void SummonPokemon()
    {
        var pi = itemSelector != null ? itemSelector.GetCurrentPokemon() : null;
        if (pi == null) return;
        if (pi.currentHP <= 0) { Debug.Log("[PlayerController] No puedes invocar un Pokémon debilitado."); return; }

        Vector3 origin = cam.transform.position; Vector3 dir = cam.transform.forward;
        Vector3 spawnPoint = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance)) spawnPoint = hit.point;

        GameObject go = Instantiate(pi.species.prefab, spawnPoint, Quaternion.identity);
        var pcb = go.GetComponent<PlayerCreatureBehavior>() ?? go.AddComponent<PlayerCreatureBehavior>();
        pcb.playerTransform = transform; pcb.AssignPokemonInstance(pi);

        activeSummonedPokemon = pcb;
        activePokemonID = pi.UniqueID;

        itemSelector?.RefreshCapturedPokemon();
    }

    void HandleSummonedPokemonAction()
    {
        // Al hacer click cuando hay activo, si estamos mirando al propio Pokémon, lo retiramos.
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        if (Physics.Raycast(ray, out RaycastHit hit, maxThrowDistance, pokemonPlayerMask))
        {
            if (activeSummonedPokemon != null)
            {
                Destroy(activeSummonedPokemon.gameObject);
                ClearActiveSummonRef();
            }
            return;
        }

        // Aquí irían órdenes al Pokémon activo en el mundo si las implementas.
    }

    void ToggleSelectorMode()
    {
        if (itemSelector == null) return;
        var newMode = itemSelector.CurrentMode == SelectorMode.Pokeball ? SelectorMode.Pokemon : SelectorMode.Pokeball;
        itemSelector.SetMode(newMode);
    }

    public void EnableControls(bool enabled)
    {
        controlsEnabled = enabled;
        if (!controlsEnabled) SetAiming(false); // al entrar en UI/combate, soltar apuntado
    }

    public PokemonInstance GetActivePokemon()
    {
        if (string.IsNullOrEmpty(activePokemonID)) return null;
        var party = PokemonStorageManager.Instance.PlayerParty?.ToList();
        return party?.FirstOrDefault(p => p.UniqueID == activePokemonID);
    }

    // --- Helpers de saneamiento del activo ---
    private void ValidateActiveSummon()
    {
        if (activeSummonedPokemon == null)
        {
            var p = GetActivePokemon();
            if (p != null && p.currentHP <= 0) ClearActiveSummonRef();
            return;
        }

        if (activeSummonedPokemon.gameObject == null || !activeSummonedPokemon.gameObject.activeInHierarchy)
        {
            ClearActiveSummonRef();
            return;
        }

        var pi = GetActivePokemon();
        if (pi == null || pi.currentHP <= 0)
        {
            ClearActiveSummonRef();
        }
    }

    private void ClearActiveSummonRef()
    {
        activeSummonedPokemon = null;
        activePokemonID = null;
        itemSelector?.RefreshCapturedPokemon();
    }
}
