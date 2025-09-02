using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class PanelPokemonTeamController : MonoBehaviour
{
    [Header("Party")]
    [SerializeField] private StorageGridUI partyGrid;
    [SerializeField] private MoveGridUI moveGrid;

    [Header("PC Box (siempre visible)")]
    [SerializeField] private GameObject panelPCBox;
    [SerializeField] private StorageGridUI pcGrid;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;

    [Header("Panel de estadísticas (derecha)")]
    [SerializeField] private PokemonStatsPanelController statsPanel;

    private void OnEnable()
    {
        if (partyGrid) partyGrid.SetMode(StorageGridUI.GridMode.Party);
        if (pcGrid) pcGrid.SetMode(StorageGridUI.GridMode.PCBox);

        if (panelPCBox) panelPCBox.SetActive(true);

        // Refrescar
        partyGrid?.Refresh();
        pcGrid?.Refresh();
        RefreshTitle();

        // Suscripciones
        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged += OnPartyChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged += OnPcBoxChanged;
        }

        if (btnPrev) { btnPrev.onClick.RemoveAllListeners(); btnPrev.onClick.AddListener(PrevBox); }
        if (btnNext) { btnNext.onClick.RemoveAllListeners(); btnNext.onClick.AddListener(NextBox); }

        if (partyGrid != null)
        {
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(OnPokemonClicked);
        }
        if (pcGrid != null)
        {
            pcGrid.onPokemonClicked.RemoveAllListeners();
            pcGrid.onPokemonClicked.AddListener(OnPokemonClicked);
        }

        // *** IMPORTANTE: limpiar cualquier selección previa y seleccionar el primero de la party ***
        StorageSlotUI.ClearGlobalSelectionVisuals();
        SelectFirstPartyPokemon();
    }

    private void OnDisable()
    {
        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged -= OnPartyChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged -= OnPcBoxChanged;
        }
        if (btnPrev) btnPrev.onClick.RemoveAllListeners();
        if (btnNext) btnNext.onClick.RemoveAllListeners();
        if (partyGrid) partyGrid.onPokemonClicked.RemoveAllListeners();
        if (pcGrid) pcGrid.onPokemonClicked.RemoveAllListeners();
    }

    private void OnPartyChanged()
    {
        partyGrid?.Refresh();
        // Mantener siempre una selección válida
        StorageSlotUI.ClearGlobalSelectionVisuals();
        SelectFirstPartyPokemon();
    }

    private void OnPcBoxChanged()
    {
        pcGrid?.Refresh();
        RefreshTitle();
    }

    public void PrevBox()
    {
        var pc = PokemonStorageManager.Instance?.PcStorage; if (pc == null) return;
        pc.SetActiveBox(Mathf.Clamp(pc.ActiveBoxIndex - 1, 0, pc.UnlockedBoxCount - 1));
        pcGrid?.Refresh(); RefreshTitle();
    }

    public void NextBox()
    {
        var pc = PokemonStorageManager.Instance?.PcStorage; if (pc == null) return;
        pc.SetActiveBox(Mathf.Clamp(pc.ActiveBoxIndex + 1, 0, pc.UnlockedBoxCount - 1));
        pcGrid?.Refresh(); RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (!title || PokemonStorageManager.Instance == null) return;
        var pc = PokemonStorageManager.Instance.PcStorage;
        title.text = $"Caja {pc.ActiveBoxIndex + 1} / {pc.UnlockedBoxCount}";
    }

    private void OnPokemonClicked(PokemonInstance p)
    {
        if (statsPanel != null) statsPanel.SetPokemon(p);
        if (moveGrid != null && partyGrid != null && partyGrid.mode == StorageGridUI.GridMode.Party)
            moveGrid.SetPokemon(p);
    }

    private void SelectFirstPartyPokemon()
    {
        var party = PokemonStorageManager.Instance?.PlayerParty;
        if (party == null) { statsPanel?.SetPokemon(null); moveGrid?.SetPokemon(null); return; }

        PokemonInstance first = null;
        int firstIndex = -1;
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            if (p != null) { first = p; firstIndex = i; break; }
        }

        if (first == null) { statsPanel?.SetPokemon(null); moveGrid?.SetPokemon(null); return; }

        statsPanel?.SetPokemon(first);
        moveGrid?.SetPokemon(first);

        if (partyGrid != null)
        {
            var slots = partyGrid.GetComponentsInChildren<StorageSlotUI>(true);
            if (firstIndex >= 0 && firstIndex < slots.Length && slots[firstIndex] != null)
            {
                // Simula un click para marcar visualmente
                var ev = new PointerEventData(EventSystem.current);
                slots[firstIndex].OnPointerClick(ev);
            }
        }
    }
}
