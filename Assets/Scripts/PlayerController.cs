using UnityEngine;
using UnityEngine.UI;
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
    [Range(1, 50)] public float maxThrowDistance = 15f;
    public float actionCooldown = 0.2f; private float lastActionTime = -Mathf.Infinity;

    [Header("Pokéball")]
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

    private CharacterController controller;
    private Camera cam;
    private float pitch;
    private bool controlsEnabled = true;

    public void EnableControls(bool enabled) { controlsEnabled = enabled; }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = cameraTransform.GetComponent<Camera>();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; cam.fieldOfView = normalFOV;
        if (crosshair != null) crosshair.enabled = false;
    }

    void Update()
    {
        bool isAiming = controlsEnabled && Input.GetMouseButton(1);
        HandleZoom(isAiming);
        if (!controlsEnabled) return;

        HandleCameraRotation();
        MovePlayer();

        if (Input.GetKeyDown(KeyCode.Q)) ToggleSelectorMode();

        if (isAiming && Input.GetMouseButtonDown(0) && Time.time - lastActionTime >= actionCooldown)
        { lastActionTime = Time.time; ExecuteAction(); }
    }

    void ExecuteAction()
    {
        if (itemSelector.CurrentMode == SelectorMode.Pokeball) TryThrowPokeball();
        else if (itemSelector.CurrentMode == SelectorMode.Pokemon)
        {
            if (activeSummonedPokemon == null) SummonPokemon();
            else HandleSummonedPokemonAction();
        }
    }

    void TryThrowPokeball()
    {
        if (Time.time - lastThrowTime < throwCooldown) return;
        lastThrowTime = Time.time;

        var item = itemSelector.GetCurrentEntry().item;
        int qty = InventoryManager.Instance.GetQuantity(item);
        if (qty <= 0 || !InventoryManager.Instance.UseItem(item)) return;

        Vector3 rayOrigin = cam.transform.position; Vector3 dir = cam.transform.forward;
        Vector3 target = rayOrigin + dir * maxThrowDistance;
        if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, maxThrowDistance)) target = hit.point;

        var pokeballGO = Instantiate(pokeballPrefab, throwOrigin.position, Quaternion.identity);
        if (pokeballGO.TryGetComponent<Ball>(out var ball))
            ball.Initialize(throwOrigin.position, target, 2f, 1f, (PokeballData)item);

        itemSelector.UpdateUI();
    }

    void SummonPokemon()
    {
        var pi = itemSelector.GetCurrentPokemon(); if (pi == null) return;

        Vector3 origin = cam.transform.position; Vector3 dir = cam.transform.forward;
        Vector3 spawnPoint = origin + dir * maxThrowDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxThrowDistance)) spawnPoint = hit.point;

        GameObject go = Instantiate(pi.species.prefab, spawnPoint, Quaternion.identity);
        var pcb = go.GetComponent<PlayerCreatureBehavior>() ?? go.AddComponent<PlayerCreatureBehavior>();
        pcb.playerTransform = transform; pcb.AssignPokemonInstance(pi);

        activeSummonedPokemon = pcb;
        activePokemonID = pi.UniqueID;

        itemSelector.RefreshCapturedPokemon();
    }

    void HandleSummonedPokemonAction()
    {
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2)); RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxThrowDistance, pokemonPlayerMask))
        {
            Destroy(activeSummonedPokemon.gameObject);
            activeSummonedPokemon = null; activePokemonID = null;
            itemSelector.RefreshCapturedPokemon(); return;
        }

        if (Physics.Raycast(ray, out hit, maxThrowDistance, movementMask))
            activeSummonedPokemon.MoveToPoint(hit.point);
    }

    void MovePlayer()
    {
        Vector3 inV = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        Vector3 dir = (cameraTransform.forward * inV.z + cameraTransform.right * inV.x).normalized; dir.y = 0;
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
        if (crosshair != null) crosshair.enabled = isAiming;
    }

    void ToggleSelectorMode()
    {
        var newMode = itemSelector.CurrentMode == SelectorMode.Pokeball ? SelectorMode.Pokemon : SelectorMode.Pokeball;
        itemSelector.SetMode(newMode);
    }

    public PokemonInstance GetActivePokemon()
    {
        if (string.IsNullOrEmpty(activePokemonID)) return null;
        var party = PokemonStorageManager.Instance.PlayerParty?.ToList();
        return party?.FirstOrDefault(p => p.UniqueID == activePokemonID);
    }
}
