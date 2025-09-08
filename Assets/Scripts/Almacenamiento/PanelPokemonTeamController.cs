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

    // Estado de preview de PC (no altera la selección global)
    private PokemonInstance _pcPreview;

    private void OnEnable()
    {
        if (partyGrid) partyGrid.SetMode(StorageGridUI.GridMode.Party);
        if (pcGrid) pcGrid.SetMode(StorageGridUI.GridMode.PCBox);
        if (panelPCBox) panelPCBox.SetActive(true);

        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged += OnPartyChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged += OnPcBoxChanged;
        }

        if (btnPrev) btnPrev.onClick.AddListener(PrevBox);
        if (btnNext) btnNext.onClick.AddListener(NextBox);

        if (partyGrid) partyGrid.onPokemonClicked.AddListener(OnPartyClicked);
        if (pcGrid) pcGrid.onPokemonClicked.AddListener(OnPcClicked);

        var roster = ServiceLocator.Get<PokemonRosterService>();
        if (roster != null)
            roster.OnSelectedChanged += HandleRosterSelectedChanged;

        partyGrid?.Refresh();
        pcGrid?.Refresh();
        RefreshTitle();

        if (roster != null)
            HandleRosterSelectedChanged(roster.GetSelectedIndex(), roster.Selected);
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

        var roster = ServiceLocator.Get<PokemonRosterService>();
        if (roster != null)
            roster.OnSelectedChanged -= HandleRosterSelectedChanged;

        _pcPreview = null;
    }

    // ---------- Navegación PC (públicos para PCBoxAutoPager) ----------
    public void PrevBox()
    {
        var pc = PokemonStorageManager.Instance?.PcStorage;
        if (pc == null) return;
        pc.SetActiveBox(Mathf.Max(0, pc.ActiveBoxIndex - 1));
        pcGrid?.Refresh();
        RefreshTitle();
        ClearPcPreviewIfInvalid();
        ApplyCurrentDisplay();
    }

    public void NextBox()
    {
        var pc = PokemonStorageManager.Instance?.PcStorage;
        if (pc == null) return;
        pc.SetActiveBox(Mathf.Min(pc.UnlockedBoxCount - 1, pc.ActiveBoxIndex + 1));
        pcGrid?.Refresh();
        RefreshTitle();
        ClearPcPreviewIfInvalid();
        ApplyCurrentDisplay();
    }

    // ---------- Callbacks ----------
    private void OnPartyChanged()
    {
        partyGrid?.Refresh();
        if (_pcPreview == null) ApplyCurrentDisplay();
    }

    private void OnPcBoxChanged()
    {
        pcGrid?.Refresh();
        ClearPcPreviewIfInvalid();
        ApplyCurrentDisplay();
    }

    private void OnPartyClicked(PokemonInstance p)
    {
        _pcPreview = null;
        ApplyCurrentDisplay();
    }

    private void OnPcClicked(PokemonInstance p)
    {
        if (p == null) return;
        _pcPreview = p;
        ApplyCurrentDisplay();
    }

    private void HandleRosterSelectedChanged(int index, PokemonInstance p)
    {
        if (_pcPreview != null) { ApplyCurrentDisplay(); return; }
        ApplyCurrentDisplay();
    }

    // ---------- Presentación ----------
    private void ApplyCurrentDisplay()
    {
        var toShow = _pcPreview;
        if (toShow == null)
        {
            var roster = ServiceLocator.Get<PokemonRosterService>();
            toShow = roster != null ? roster.Selected : null;
        }

        if (statsPanel != null) statsPanel.SetPokemon(toShow);
        if (moveGrid != null) moveGrid.SetPokemon(toShow);
    }

    private void ClearPcPreviewIfInvalid()
    {
        if (_pcPreview == null) return;
        var pc = PokemonStorageManager.Instance?.PcStorage?.ActiveBox;
        bool found = false;
        if (pc != null)
        {
            for (int i = 0; i < pc.MaxCapacity; i++)
                if (pc.GetAt(i) == _pcPreview) { found = true; break; }
        }
        if (!found) _pcPreview = null;
    }

    private void RefreshTitle()
    {
        var pc = PokemonStorageManager.Instance?.PcStorage;
        if (pc == null || title == null) return;
        title.text = $"Caja {pc.ActiveBoxIndex + 1} / {pc.UnlockedBoxCount}";
    }
}
