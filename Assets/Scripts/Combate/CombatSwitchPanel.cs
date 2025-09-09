using System.Reflection;
using UnityEngine;

public class CombatSwitchPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private StorageGridUI partyGrid;
    [SerializeField] private TurnController turn; // opcional

    private System.Action<PokemonInstance> onSelected;

    private void Awake()
    {
        if (partyGrid)
        {
            partyGrid.SetMode(StorageGridUI.GridMode.Party);
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(OnClickPokemon);
        }
        ResolveTurnRef();
    }

    private void OnEnable()
    {
        ResolveTurnRef();
        if (partyGrid)
        {
            partyGrid.SetMode(StorageGridUI.GridMode.Party);
            partyGrid.Refresh();
        }
    }

    private void ResolveTurnRef()
    {
        if (turn) return;
        var cs = CombatService.Instance;
        turn = cs ? cs.CurrentTurn : FindFirstObjectByType<TurnController>(FindObjectsInactive.Exclude);
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

        var active = TryGetActivePlayerModel();
        if (active != null && ReferenceEquals(active, p)) return; // evita perder turno

        if (onSelected != null) onSelected.Invoke(p);
        else
        {
            if (turn) turn.QueueSwitch(p);
            else Debug.LogWarning("[Turn] no hay TurnController");
        }

        Close();
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        onSelected = null;
    }

    private static PokemonInstance TryGetActivePlayerModel()
    {
        var list = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            var mb = list[i]; if (!mb) continue;
            var t = mb.GetType();
            if (t.Name != "CombatantController") continue;

            bool isPlayer = false;
            var piP = t.GetProperty("IsPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piP != null) { try { isPlayer = (bool)piP.GetValue(mb); } catch { isPlayer = false; } }
            if (!isPlayer) continue;

            var piM = t.GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piM != null) { try { return piM.GetValue(mb) as PokemonInstance; } catch { } }
        }
        return null;
    }
}
