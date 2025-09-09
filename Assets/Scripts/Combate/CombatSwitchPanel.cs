using UnityEngine;
using UnityEngine.UI;

/// Panel simple para elegir un Pokémon del equipo. Usa tu StorageGridUI de party.
public class CombatSwitchPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private StorageGridUI partyGrid;
    [SerializeField] private Button btnCancel;

    private System.Action<PokemonInstance> onSelected;

    private void Awake()
    {
        if (partyGrid)
        {
            partyGrid.SetMode(StorageGridUI.GridMode.Party);
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(OnClickPokemon);
        }
        if (btnCancel) btnCancel.onClick.AddListener(Close);
        Close();
    }

    public void OpenVoluntary(System.Action<PokemonInstance> onChosen, System.Action onCancel = null)
    {
        onSelected = onChosen;
        if (btnCancel) btnCancel.gameObject.SetActive(true);
        partyGrid?.Refresh();
        if (root) root.SetActive(true);
        // cancelar → volver al menú principal
        if (btnCancel && onCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(() => { Close(); onCancel(); });
        }
    }

    public void OpenForced(System.Action<PokemonInstance> onChosen)
    {
        onSelected = onChosen;
        if (btnCancel) btnCancel.gameObject.SetActive(false);
        partyGrid?.Refresh();
        if (root) root.SetActive(true);
    }

    private void OnClickPokemon(PokemonInstance p)
    {
        if (p == null || p.currentHP <= 0) return;
        onSelected?.Invoke(p);
        Close();
    }

    public void Close()
    {
        if (root) root.SetActive(false);
    }
}
