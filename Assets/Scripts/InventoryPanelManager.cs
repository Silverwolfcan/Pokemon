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

    // Cacheo ligero por reflexión para compatibilidad con distintas versiones de CombatService
    private static System.Reflection.PropertyInfo _propIsInBattle;
    private static System.Reflection.PropertyInfo _propIsInEncounter;
    private static System.Reflection.PropertyInfo _propActiveEncounter;
    private static bool _combatPropsCached = false;

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        // Estado inicial
        SetAllPanelsActive(false);
        SetCursorAndControls(uiActive: false);
    }

    void Update()
    {
        // Si estamos en combate, impedir abrir el inventario con Tab
        bool inCombat = IsCombatActive();

        // Si el inventario estaba abierto y entra combate (edge case), ciérralo sin liberar controles del combate
        if (isInventoryOpen && inCombat)
        {
            CloseInventory(dueToCombat: true);
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (inCombat)
            {
                // Ignorar el toggle durante combate
                return;
            }

            ToggleInventory();
        }
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

        // Estaba abierto: si hay subpanel activo, volver al principal; si no, cerrar inventario.
        if (IsAnyOtherPanelActive())
        {
            BackToMainPanel();
        }
        else
        {
            CloseInventory(dueToCombat: false);
        }
    }

    private void OpenInventoryMain()
    {
        // Seguridad: no abrir si (por cualquier llamada externa) hubiera combate activo
        if (IsCombatActive()) return;

        isInventoryOpen = true;

        // Solo menú principal visible
        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(true);

        SetCursorAndControls(uiActive: true);
    }

    private void CloseInventory(bool dueToCombat)
    {
        isInventoryOpen = false;
        SetAllPanelsActive(false);

        if (dueToCombat)
        {
            // No alteres el estado de combate: mantén controles desactivados y cursor libre para la UI de combate
            if (playerController) playerController.EnableControls(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Cierre normal (fuera de combate): vuelta a gameplay
            SetCursorAndControls(uiActive: false);
        }
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
            // Si por cualquier motivo nos llaman estando cerrado y NO hay combate, lo abrimos
            if (IsCombatActive()) return;
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

    // Detecta combate activo sin depender de una propiedad concreta (compatibilidad IsInBattle / IsInEncounter / ActiveEncounter)
    private static bool IsCombatActive()
    {
        if (CombatService.Instance == null) return false;
        var cs = CombatService.Instance;

        if (!_combatPropsCached)
        {
            var t = cs.GetType();
            _propIsInBattle = t.GetProperty("IsInBattle");
            _propIsInEncounter = t.GetProperty("IsInEncounter");
            _propActiveEncounter = t.GetProperty("ActiveEncounter");
            _combatPropsCached = true;
        }

        try
        {
            if (_propIsInBattle != null)
            {
                var v = _propIsInBattle.GetValue(cs);
                if (v is bool b && b) return true;
            }
        }
        catch { /* ignorar */ }

        try
        {
            if (_propIsInEncounter != null)
            {
                var v = _propIsInEncounter.GetValue(cs);
                if (v is bool b && b) return true;
            }
        }
        catch { /* ignorar */ }

        try
        {
            if (_propActiveEncounter != null)
            {
                var v = _propActiveEncounter.GetValue(cs);
                if (v != null) return true;
            }
        }
        catch { /* ignorar */ }

        return false;
    }
}
