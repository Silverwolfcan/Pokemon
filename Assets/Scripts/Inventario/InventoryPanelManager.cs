using UnityEngine;

public class InventoryPanelManager : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private GameObject mainPanel;

    [Header("Paneles secundarios (Party, PC, Bolsa, etc.)")]
    [SerializeField] private GameObject[] otherPanels;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private bool isInventoryOpen = false;
    private PlayerController playerController;

    private static System.Reflection.PropertyInfo _propIsInBattle;
    private static System.Reflection.PropertyInfo _propIsInEncounter;
    private static System.Reflection.PropertyInfo _propActiveEncounter;
    private static bool _combatPropsCached = false;

    void Start()
    {
        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        SetAllPanelsActive(false);
        SetCursorAndControls(uiActive: false);
    }

    void Update()
    {
        bool inCombat = IsCombatActive();

        if (isInventoryOpen && inCombat)
            CloseInventory(dueToCombat: true);

        if (Input.GetKeyDown(toggleKey))
        {
            if (inCombat) return;
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        if (!isInventoryOpen) { OpenInventoryMain(); return; }

        if (IsAnyOtherPanelActive()) BackToMainPanel();
        else CloseInventory(dueToCombat: false);
    }

    private void OpenInventoryMain()
    {
        if (IsCombatActive()) return;

        isInventoryOpen = true;

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
            if (playerController) playerController.EnableControls(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            SetCursorAndControls(uiActive: false);
        }
    }

    public void BackToMainPanel()
    {
        if (!isInventoryOpen) return;

        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(true);
        SetCursorAndControls(uiActive: true);
    }

    public void OpenPanel(GameObject panelToOpen)
    {
        if (!isInventoryOpen)
        {
            if (IsCombatActive()) return;
            OpenInventoryMain();
        }

        if (otherPanels != null)
            foreach (var p in otherPanels) if (p) p.SetActive(false);

        if (mainPanel) mainPanel.SetActive(false);
        if (panelToOpen) panelToOpen.SetActive(true);
        SetCursorAndControls(uiActive: true);
    }

    public void OpenPokemonPanel(GameObject panelToOpen)
    {
        OpenPanel(panelToOpen);
        if (!panelToOpen) return;

        var grids = panelToOpen.GetComponentsInChildren<StorageGridUI>(true);
        foreach (var g in grids) g.Refresh();
    }

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

        try { if (_propIsInBattle != null && _propIsInBattle.GetValue(cs) is bool b1 && b1) return true; } catch { }
        try { if (_propIsInEncounter != null && _propIsInEncounter.GetValue(cs) is bool b2 && b2) return true; } catch { }
        try { if (_propActiveEncounter != null && _propActiveEncounter.GetValue(cs) != null) return true; } catch { }

        return false;
    }
}
