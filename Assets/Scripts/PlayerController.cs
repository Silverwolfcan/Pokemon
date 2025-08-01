using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
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
    public float normalFOV = 60f;
    public float zoomFOV = 30f;
    public float zoomSpeed = 10f;
    public Image crosshair;

    [Header("Acción (click izquierdo mientras apuntas)")]
    [Range(1, 50)]
    public float maxThrowDistance = 15f;
    public float actionCooldown = 0.2f;
    private float lastActionTime = -Mathf.Infinity;

    [Header("Pokéball")]
    public GameObject pokeballPrefab;
    public Transform throwOrigin;
    public float throwCooldown = 2f;        // diferenciado si quieres un cooldown distinto para lanzar bolas
    private float lastThrowTime = -Mathf.Infinity;

    [Header("UI de Pokéballs y Pokémon")]
    public ItemSelectorUI itemSelector;

    [Header("Raycast Layers")]
    [Tooltip("Sólo la capa PokémonPlayer para detectar devoluciones")]
    public LayerMask pokemonPlayerMask;

    [Tooltip("Todas las demás capas (suelo, obstáculos…) para mover al Pokémon")]
    public LayerMask movementMask;

    // Estado de invocación
    private PlayerCreatureBehavior activeSummonedPokemon;
    private string activePokemonID;

    // Componentes internos
    private CharacterController controller;
    private Camera cam;
    private float pitch;

    // ---- NUEVO: Flag para habilitar/deshabilitar controles ----
    private bool controlsEnabled = true;

    public void EnableControls(bool enabled)
    {
        controlsEnabled = enabled;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = cameraTransform.GetComponent<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cam.fieldOfView = normalFOV;

        if (crosshair != null)
            crosshair.enabled = false;
    }

    void Update()
    {
        // 1) Gestionamos siempre el zoom/crosshair en función de si estamos apuntando Y controles activos
        bool isAiming = controlsEnabled && Input.GetMouseButton(1);
        HandleZoom(isAiming);

        // 2) Si los controles están desactivados, paramos aquí cualquier input de jugador
        if (!controlsEnabled)
            return;

        // 3) Movimiento y rotación solo con controles activados
        HandleCameraRotation();
        MovePlayer();

        // 4) Cambio de modo (Q)
        if (Input.GetKeyDown(KeyCode.Q))
            ToggleSelectorMode();

        // 5) Click izquierdo mientras apuntas → acción principal
        if (isAiming && Input.GetMouseButtonDown(0) && Time.time - lastActionTime >= actionCooldown)
        {
            lastActionTime = Time.time;
            ExecuteAction();
        }
    }

    // ---------------------
    //  FLUJO PRINCIPAL
    // ---------------------
    void ExecuteAction()
    {
        // 1) MODO Pokéball → lanzas bola
        if (itemSelector.CurrentMode == SelectorMode.Pokeball)
        {
            TryThrowPokeball();
        }
        // 2) MODO Pokémon
        else if (itemSelector.CurrentMode == SelectorMode.Pokemon)
        {
            // a) Sin Pokémon invocado → invocas
            if (activeSummonedPokemon == null)
            {
                SummonPokemon();
            }
            // b) Ya hay uno → chequeas si devuelves o mandas mover
            else
            {
                HandleSummonedPokemonAction();
            }
        }
    }

    // ---------------------
    //  LÓGICA DE CAPTURA
    // ---------------------
    void TryThrowPokeball()
    {
        if (Time.time - lastThrowTime < throwCooldown) return;
        lastThrowTime = Time.time;

        var item = itemSelector.GetCurrentEntry().item;
        int qty = InventoryManager.Instance.GetQuantity(item);
        if (qty <= 0 || !InventoryManager.Instance.UseItem(item)) return;

        // calculas destino del arco
        Vector3 rayOrigin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        Vector3 target = rayOrigin + dir * maxThrowDistance;
        if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, maxThrowDistance))
            target = hit.point;

        // lanzas la bola
        var pokeballGO = Instantiate(pokeballPrefab, throwOrigin.position, Quaternion.identity);
        if (pokeballGO.TryGetComponent<Ball>(out var ball))
            ball.Initialize(throwOrigin.position, target, 2f, 1f, (PokeballData)item);

        itemSelector.UpdateUI();
    }

    // ---------------------
    //  LÓGICA DE INVOCACIÓN
    // ---------------------
    void SummonPokemon()
    {
        var pi = itemSelector.GetCurrentPokemon();
        if (pi == null)
        {
            //Debug.LogWarning("No hay Pokémon seleccionado para invocar.");
            return;
        }

        // Calcula punto de spawn con raycast
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        Vector3 spawnPoint = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance))
            spawnPoint = hit.point;

        // Instancia el prefab
        GameObject go = Instantiate(pi.baseData.prefab, spawnPoint, Quaternion.identity);

        // Asegura que tenga PlayerCreatureBehavior (que a su vez añadirá su NavMeshAgent)
        var pcb = go.GetComponent<PlayerCreatureBehavior>()
                  ?? go.AddComponent<PlayerCreatureBehavior>();
        pcb.playerTransform = transform;
        pcb.AssignPokemonInstance(pi);

        activeSummonedPokemon = pcb;
        activePokemonID = pi.uniqueID;

        itemSelector.RefreshCapturedPokemon();
    }


    // ---------------------
    //  LÓGICA DE CONTROLAR AL INVOCADO
    // ---------------------
    void HandleSummonedPokemonAction()
    {
        // Creamos el rayo desde el centro de la pantalla
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        RaycastHit hit;

        // 1) Primero comprobamos solo contra la capa "PokemonPlayer" para devolverlo
        if (Physics.Raycast(ray, out hit, maxThrowDistance, pokemonPlayerMask))
        {
            // Si el hit es nuestro Pokémon invocado, lo devolvemos
            Destroy(activeSummonedPokemon.gameObject);
            activeSummonedPokemon = null;
            activePokemonID = null;
            itemSelector.RefreshCapturedPokemon();
            //Debug.Log("✅ Pokémon devuelto a la Pokéball.");
            return;
        }

        // 2) Si no hemos devuelto, comprobamos ahora contra el suelo/objetos de movimiento
        if (Physics.Raycast(ray, out hit, maxThrowDistance, movementMask))
        {
            // Enviamos al Pokémon a moverse al punto impactado
            activeSummonedPokemon.MoveToPoint(hit.point);
            //Debug.Log($"🐾 Pokémon moviéndose a {hit.point}");
        }
    }


    // ---------------------
    //  UTILIDADES
    // ---------------------
    void MovePlayer()
    {
        Vector3 inV = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        Vector3 dir = (cameraTransform.forward * inV.z + cameraTransform.right * inV.x).normalized;
        dir.y = 0;
        controller.Move(dir * moveSpeed * Time.deltaTime);
    }

    void HandleCameraRotation()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - my, minY, maxY);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0, 0);
        transform.Rotate(Vector3.up * mx);
    }

    void HandleZoom(bool isAiming)
    {
        float targetFOV = isAiming ? zoomFOV : normalFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        if (crosshair != null)
            crosshair.enabled = isAiming;
    }

    void ToggleSelectorMode()
    {
        var newMode = itemSelector.CurrentMode == SelectorMode.Pokeball
                      ? SelectorMode.Pokemon
                      : SelectorMode.Pokeball;
        itemSelector.SetMode(newMode);
    }

    // ( opcional, si lo necesitas en otro sitio )
    public PokemonInstance GetActivePokemon()
    {
        if (string.IsNullOrEmpty(activePokemonID)) return null;
        return GameManager.Instance.playerTeam
                     .FirstOrDefault(p => p.uniqueID == activePokemonID);
    }
}
