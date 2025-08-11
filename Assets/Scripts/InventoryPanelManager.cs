using UnityEngine;

public class InventoryPanelManager : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject mainPanel;      // Contenedor del menú principal del inventario

    [Header("Paneles secundarios (Party, PC, Bolsa, etc.)")]
    [SerializeField] private GameObject[] otherPanels;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private bool isInventoryOpen = false;
    private PlayerController playerController;

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        // Asegura estado inicial
        SetAllPanelsActive(false);
        SetCursorAndControls(uiActive: false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleInventory();
    }

    // --------------------------------------------------
    // Estados
    // --------------------------------------------------
    public void ToggleInventory()
    {
        if (!isInventoryOpen)
        {
            // Estaba cerrado -> abrir inventario (solo menú principal)
            OpenInventoryMain();
            return;
        }

        // Estaba abierto: si hay subpanel activo, volver al principal;
        // si no, cerrar inventario.
        if (IsAnyOtherPanelActive())
        {
            BackToMainPanel();
        }
        else
        {
            CloseInventory();
        }
    }

    private void OpenInventoryMain()
    {
        isInventoryOpen = true;
        // Solo menú principal visible
        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(true);

        SetCursorAndControls(uiActive: true);
    }

    private void CloseInventory()
    {
        isInventoryOpen = false;
        SetAllPanelsActive(false);
        SetCursorAndControls(uiActive: false);
    }

    public void BackToMainPanel()
    {
        if (!isInventoryOpen) return;

        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(true);

        // Seguimos en inventario -> controles desactivados
        SetCursorAndControls(uiActive: true);
    }

    // Abre un subpanel (llámalo desde los botones del menú principal)
    public void OpenPanel(GameObject panelToOpen)
    {
        if (!isInventoryOpen)
        {
            // Si por cualquier motivo nos llaman estando cerrado, lo abrimos
            OpenInventoryMain();
        }

        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(false);
        if (panelToOpen) panelToOpen.SetActive(true);

        SetCursorAndControls(uiActive: true);
    }

    // Versión específica que además refresca grids dentro del panel
    public void OpenPokemonPanel(GameObject panelToOpen)
    {
        OpenPanel(panelToOpen);
        if (!panelToOpen) return;

        var grids = panelToOpen.GetComponentsInChildren<StorageGridUI>(true);
        foreach (var g in grids) g.Refresh();
    }

    // --------------------------------------------------
    // Helpers
    // --------------------------------------------------
    private bool IsAnyOtherPanelActive()
    {
        if (otherPanels == null) return false;
        foreach (var p in otherPanels)
            if (p && p.activeSelf) return true;
        return false;
    }

    private void SetAllPanelsActive(bool active)
    {
        if (mainPanel) mainPanel.SetActive(active);
        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(active);
    }

    private void SetCursorAndControls(bool uiActive)
    {
        if (uiActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerController) playerController.EnableControls(false);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (playerController) playerController.EnableControls(true);
        }
    }
}
