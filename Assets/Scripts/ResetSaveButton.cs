using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SaveClearButton : MonoBehaviour
{
    public enum ButtonAction
    {
        Save,
        Clear
    }

    [Header("Selecciona la acción que realizará este botón")]
    [SerializeField] private ButtonAction actionType;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void Start()
    {
        button.onClick.RemoveAllListeners();
        switch (actionType)
        {
            case ButtonAction.Save:
                button.onClick.AddListener(OnSaveClicked);
                break;
            case ButtonAction.Clear:
                button.onClick.AddListener(OnClearClicked);
                break;
        }
    }

    private void OnSaveClicked()
    {
        // Guarda el equipo actual
        SaveSystem.SaveTeam(GameManager.Instance.playerTeam);
        Debug.Log("[SaveClearButton] Equipo guardado manualmente.");
    }

    private void OnClearClicked()
    {
        // Borra el fichero de guardado y reinicia el equipo
        SaveSystem.ClearData();
        GameManager.Instance.playerTeam.Clear();

        // Refresca UI de equipo
        if (GameManager.Instance.teamUIManager != null)
        {
            GameManager.Instance.teamUIManager.currentTeam = GameManager.Instance.playerTeam;
            GameManager.Instance.teamUIManager.RefreshUI();
        }

        // Refresca selector de Pokéballs/Pokémon si existe
        if (FindObjectOfType<ItemSelectorUI>() is ItemSelectorUI selector)
            selector.UpdateUI();

        Debug.Log("[SaveClearButton] Datos de guardado borrados y equipo reiniciado.");
    }
}
