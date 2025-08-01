using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PokemonSlotUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgSprite;
    [SerializeField] private Slider sliderHealth;
    [SerializeField] private TextMeshProUGUI txtHealth;
    [SerializeField] private TextMeshProUGUI txtLevel;
    [SerializeField] private Image imgSex;

    [Header("Sprites de Sexo")]
    [SerializeField] private Sprite maleIcon;
    [SerializeField] private Sprite femaleIcon;

    // —— Campos para drag & drop ——
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalIndex;
    private GameObject placeholder;

    private PokemonInstance currentPokemon;
    private Button button;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        button = GetComponent<Button>();
    }

    private void Start()
    {
        // Siempre asignamos nuestro método al clic
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClickSlot);
    }

    /// <summary>
    /// Rellena el slot con datos del Pokémon.
    /// </summary>
    public void Setup(PokemonInstance pokemon)
    {
        currentPokemon = pokemon;
        if (pokemon == null || pokemon.baseData == null) return;

        // Pintar datos
        txtName.text = pokemon.baseData.pokemonName;
        imgSprite.sprite = pokemon.baseData.pokemonSprite;
        sliderHealth.maxValue = pokemon.stats.HP;
        sliderHealth.value = pokemon.currentHP;
        txtHealth.text = $"{pokemon.currentHP} / {pokemon.stats.HP}";
        txtLevel.text = $"Nv. {pokemon.level}";
        imgSex.preserveAspect = true;

        if (pokemon.gender == Gender.Male)
        {
            imgSex.sprite = maleIcon;
            imgSex.enabled = true;
        }
        else if (pokemon.gender == Gender.Female)
        {
            imgSex.sprite = femaleIcon;
            imgSex.enabled = true;
        }
        else
        {
            imgSex.enabled = false;
        }
    }

    private void OnClickSlot()
    {
        if (currentPokemon == null) return;
        Debug.Log($"[Slot] clicado {currentPokemon.baseData.pokemonName}");
        var ui = FindObjectOfType<PokemonTeamUIManager>();              // Obsoleto, pero funciona
        // var ui = FindFirstObjectByType<PokemonTeamUIManager>();      // Si quieres usar la nueva API
        if (ui != null)
            ui.ShowAttackList(currentPokemon);
        else
            Debug.LogWarning("No se encuentra PokemonTeamUIManager en escena.");
    }

}
