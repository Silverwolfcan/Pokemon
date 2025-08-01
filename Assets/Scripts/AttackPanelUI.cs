using System.Collections.Generic;
using UnityEngine;

public class AttackPanelUI : MonoBehaviour
{
    [Header("Prefab de slot de ataque")]
    [SerializeField] private GameObject attackSlotPrefab;

    [Header("Contenedor de slots")]
    [SerializeField] private Transform attackSlotContainer;

    /// <summary>
    /// Limpia y vuelve a dibujar los slots de ataque para el Pokémon dado.
    /// </summary>
    public void RefreshAttacks(PokemonInstance pokemon)
    {
        // 1) Borrar cualquier slot previo
        foreach (Transform child in attackSlotContainer)
            Destroy(child.gameObject);

        // 2) Crear un slot por cada ataque aprendido
        for (int i = 0; i < pokemon.learnedAttacks.Count; i++)
        {
            var attackData = pokemon.learnedAttacks[i];
            var slotGO = Instantiate(attackSlotPrefab, attackSlotContainer);
            var slot = slotGO.GetComponent<AttackSlotUI>();
            // Ahora Setup recibe también la instancia de Pokémon para saber a quién pertenece
            slot.Setup(attackData, pokemon);
        }
    }
}
