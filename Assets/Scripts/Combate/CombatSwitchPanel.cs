using UnityEngine;

/// Panel para elegir un Pokémon del equipo. Usa tu StorageGridUI de party.
/// Sin botón de retroceso; se cierra con ESC desde CombatUIController.
public class CombatSwitchPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private StorageGridUI partyGrid;

    private System.Action<PokemonInstance> onSelected;

    private void Awake()
    {
        if (partyGrid)
        {
            partyGrid.SetMode(StorageGridUI.GridMode.Party);
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(OnClickPokemon);
        }
        // Importante: NO cerrar aquí. Dejar el estado inicial al del Inspector.
    }

    public void OpenVoluntary(System.Action<PokemonInstance> onChosen)
    {
        onSelected = onChosen;
        partyGrid?.Refresh();
        if (root) root.SetActive(true);
    }

    public void OpenForced(System.Action<PokemonInstance> onChosen)
    {
        onSelected = onChosen;
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
