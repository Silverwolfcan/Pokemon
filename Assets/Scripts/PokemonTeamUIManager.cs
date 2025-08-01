using UnityEngine;
using System.Collections.Generic;

public class PokemonTeamUIManager : MonoBehaviour
{
    [Header("Prefab del slot de Pokémon")]
    public GameObject pokemonSlotPrefab;

    [Header("Contenedor de los slots")]
    public Transform pokemonListContainer;

    [HideInInspector]
    public List<PokemonInstance> currentTeam = new List<PokemonInstance>();

    [Header("Panel de ataques")]
    [SerializeField] private AttackPanelUI attackPanelUI;

    public void RefreshUI()
    {
        // 1) Limpiar slots anteriores
        foreach (Transform child in pokemonListContainer)
            Destroy(child.gameObject);

        // 2) Instanciar y configurar cada slot
        foreach (var pokemon in currentTeam)
        {
            var slotGO = Instantiate(pokemonSlotPrefab, pokemonListContainer);
            var slotUI = slotGO.GetComponent<PokemonSlotUI>();
            if (slotUI == null) continue;

            slotUI.Setup(pokemon);
        }
    }

    // Ahora público para que PokemonSlotUI pueda invocarlo
    public void ShowAttackList(PokemonInstance pokemon)
    {
        if (attackPanelUI == null) return;

        attackPanelUI.gameObject.SetActive(true);
        attackPanelUI.transform.SetAsLastSibling();
        attackPanelUI.RefreshAttacks(pokemon);
    }
}
