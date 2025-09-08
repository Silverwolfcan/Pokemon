using System.Collections;
using UnityEngine;

/// Coordina selección y visuals al entrar/salir de un menú que muestra party/PC.
/// Sustituye a RosterSelectionOnEnable + SelectionVisualResetter.
public sealed class MenuSelectionCoordinator : MonoBehaviour
{
    public enum Policy { FirstSlot, FirstAlive }

    [Header("Política al ABRIR el menú")]
    [Tooltip("FirstSlot = índice 0 de la party. FirstAlive = primer Pokémon con HP>0.")]
    public Policy selectionPolicy = Policy.FirstSlot;

    [Tooltip("Espera 1 frame antes de aplicar la selección para dejar que las vistas se construyan.")]
    public bool waitOneFrameOnEnable = true;

    [Header("Limpieza al CERRAR el menú")]
    [Tooltip("Limpia TODAS las marcas visuales de selección al cerrar este menú.")]
    public bool clearVisualsOnDisable = true;

    private void OnEnable()
    {
        if (waitOneFrameOnEnable) StartCoroutine(ApplySelectionNextFrame());
        else ApplySelectionNow();
    }

    private void OnDisable()
    {
        if (clearVisualsOnDisable)
            StorageSlotUI.ClearGlobalSelectionVisuals();
    }

    private IEnumerator ApplySelectionNextFrame()
    {
        yield return null; // asegura que StorageGridUI/StorageSlotUI ya hicieron OnEnable/Refresh
        ApplySelectionNow();
    }

    private void ApplySelectionNow()
    {
        var roster = ServiceLocator.Get<PokemonRosterService>();
        if (roster == null) return;

        // Compacta y fuerza selección determinística
        switch (selectionPolicy)
        {
            case Policy.FirstSlot:
                roster.SelectFirstSlot();
                break;
            case Policy.FirstAlive:
            default:
                roster.SelectFirstAlive();
                break;
        }
        // StorageGridUI y SelectedPokemonBinder reaccionan vía eventos.
    }
}
