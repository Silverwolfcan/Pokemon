using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class AttackSlotUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI ppText;


    // La instancia de Pokémon a la que pertenecen estos ataques
    private PokemonInstance currentPokemon;

    /// <summary>
    /// Inicializa el slot mostrando los datos del ataque y guardando el propietario.
    /// </summary>
    public void Setup(AttackData attack, PokemonInstance owner)
    {
        currentPokemon = owner;

        // Pintar datos en pantalla
        nameText.text = attack.attackName;
        typeText.text = attack.type.ToString();
        ppText.text = $"{attack.pp}/{attack.pp}";
    }
}
