using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PokemonTeamPanelController : MonoBehaviour
{
    [Header("Party")]
    [SerializeField] private StorageGridUI partyGrid;     // Grid del equipo (mode = Party)
    [SerializeField] private MoveGridUI moveGrid;         // <<< Nuevo: panel de movimientos

    [Header("PC Box")]
    [SerializeField] private GameObject panelPCBox;       // Panel_PCBox (se muestra/oculta con R)
    [SerializeField] private StorageGridUI pcGrid;        // Grid del PC (mode = PCBox)
    [SerializeField] private TextMeshProUGUI title;       // "Caja X / Y"
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.R;
    [SerializeField] private bool startWithPCClosed = true;

    private void OnEnable()
    {
        if (partyGrid) partyGrid.SetMode(StorageGridUI.GridMode.Party);
        if (pcGrid) pcGrid.SetMode(StorageGridUI.GridMode.PCBox);

        if (panelPCBox) panelPCBox.SetActive(!startWithPCClosed);

        partyGrid?.Refresh();
        pcGrid?.Refresh();
        RefreshTitle();

        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged += OnPartyChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged += OnPcBoxChanged;
        }

        if (btnPrev) { btnPrev.onClick.RemoveAllListeners(); btnPrev.onClick.AddListener(PrevBox); }
        if (btnNext) { btnNext.onClick.RemoveAllListeners(); btnNext.onClick.AddListener(NextBox); }

        // >>> Al hacer clic en un Pokémon del equipo, mostramos sus movimientos
        if (partyGrid != null)
        {
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(OnPartyPokemonClicked);
        }
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
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) TogglePC();
    }

    private void OnPartyChanged()
    {
        partyGrid?.Refresh();
        // Opcional: si quieres que, si desaparece el seleccionado, limpiemos los movimientos:
        // moveGrid?.SetPokemon(null);
    }

    private void OnPcBoxChanged()
    {
        pcGrid?.Refresh();
        RefreshTitle();
    }

    // --------- UI actions ----------
    public void TogglePC()
    {
        if (!panelPCBox) return;
        bool newActive = !panelPCBox.activeSelf;
        panelPCBox.SetActive(newActive);
        if (newActive) { pcGrid?.Refresh(); RefreshTitle(); }
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

    // >>> Handler del click de un Pokémon de la party
    private void OnPartyPokemonClicked(PokemonInstance p)
    {
        if (moveGrid == null) return;
        moveGrid.SetPokemon(p);   // pinta los 4 slots de movimientos del Pokémon pulsado
    }
}
