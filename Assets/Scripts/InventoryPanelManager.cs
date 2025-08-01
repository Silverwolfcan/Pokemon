using UnityEngine;

public class InventoryPanelManager : MonoBehaviour
{
    [Header("Panel principal")]
    public GameObject mainPanel;            // Panel principal del inventario (contenedor)

    [Header("Paneles secundarios")]
    public GameObject[] otherPanels;        // Otros paneles como Pokémon, Bolsa, etc.

    public PokemonTeamUIManager teamUIManager;

    private bool isInventoryOpen = false;

    private PlayerController playerController;

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        mainPanel.SetActive(isInventoryOpen);

        if (isInventoryOpen)
        {
            ShowOnlyMainPanel();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerController != null)
                playerController.EnableControls(false);  // 🔒 Desactivar movimiento/rotación
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (playerController != null)
                playerController.EnableControls(true);   // 🔓 Activar movimiento/rotación
        }
    }


    public void OpenPanel(GameObject panelToOpen)
    {
        foreach (var panel in otherPanels)
        {
            panel.SetActive(false);
        }

        panelToOpen.SetActive(true);
    }

    public void OpenPokemonPanel(GameObject panelToOpen)
    {
        OpenPanel(panelToOpen);
        teamUIManager.RefreshUI();
    }

    public void BackToMainPanel()
    {
        foreach (var panel in otherPanels)
        {
            panel.SetActive(false);
        }

        mainPanel.SetActive(true);
    }

    private void ShowOnlyMainPanel()
    {
        foreach (var panel in otherPanels)
        {
            panel.SetActive(false);
        }

        mainPanel.SetActive(true);
    }
}
