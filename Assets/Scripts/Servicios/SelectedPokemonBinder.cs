using UnityEngine;

/// Vincula la selección global (PokemonRosterService) a paneles de Stats y/o MoveGrid.
/// Ahora también refresca cuando cambian los valores del Pokémon mostrado.
public sealed class SelectedPokemonBinder : MonoBehaviour
{
    [Header("Destinos (opcionales)")]
    [SerializeField] private PokemonStatsPanelController statsPanel;
    [SerializeField] private MoveGridUI moveGrid;

    private PokemonRosterService _roster;

    private void OnEnable()
    {
        _roster = ServiceLocator.Get<PokemonRosterService>();
        if (_roster != null)
        {
            _roster.OnSelectedChanged += HandleSelectedChanged;
            HandleSelectedChanged(_roster.GetSelectedIndex(), _roster.Selected);
        }
        GameEventBus.PartyChanged += OnPartyChanged;
        GameEventBus.PokemonChanged += OnPokemonChanged;
    }

    private void OnDisable()
    {
        if (_roster != null)
            _roster.OnSelectedChanged -= HandleSelectedChanged;
        _roster = null;

        GameEventBus.PartyChanged -= OnPartyChanged;
        GameEventBus.PokemonChanged -= OnPokemonChanged;
    }

    private void OnPartyChanged()
    {
        if (_roster == null) return;
        HandleSelectedChanged(_roster.GetSelectedIndex(), _roster.Selected);
    }

    private void OnPokemonChanged(PokemonInstance changed)
    {
        // Si el mostrado actual cambia de valores, repintamos.
        if (_roster == null) return;
        var current = _roster.Selected;
        if (current == null) return;
        if (!ReferenceEquals(current, changed)) return;
        HandleSelectedChanged(_roster.GetSelectedIndex(), current);
    }

    private void HandleSelectedChanged(int index, PokemonInstance p)
    {
        if (statsPanel != null) statsPanel.SetPokemon(p);
        if (moveGrid != null) moveGrid.SetPokemon(p);
    }

    public void ForceRefresh()
    {
        if (_roster == null) _roster = ServiceLocator.Get<PokemonRosterService>();
        if (_roster != null) HandleSelectedChanged(_roster.GetSelectedIndex(), _roster.Selected);
    }
}
