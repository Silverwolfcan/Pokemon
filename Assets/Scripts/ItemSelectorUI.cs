using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public enum SelectorMode
{
    Pokeball,
    Pokemon
}

public class ItemSelectorUI : MonoBehaviour
{
    public SelectorMode CurrentMode => currentMode;

    [Header("Referencias de Pokéballs")]
    public GameObject panelBalls;
    public Image imgActiveBall;
    public TextMeshProUGUI txtNameBalls;
    public TextMeshProUGUI txtNumberBalls;
    public Image imgPreviousBall;
    public Image imgNextBall;

    [Header("Referencias de Pokémon")]
    public GameObject panelPokemon;
    public Image imgActivePokemon;
    public TextMeshProUGUI txtNamePokemon;
    public TextMeshProUGUI txtLevelPokemon;
    public Image imgPreviousPokemon;
    public Image imgNextPokemon;

    [Header("Colores de estado")]
    public Color colorDisponible = Color.white;
    public Color colorAgotado = Color.gray;

    [SerializeField] private SelectorMode currentMode = SelectorMode.Pokeball;
    private int currentIndex = 0;

    private List<ItemEntry> pokeballInventory = new();
    private List<PokemonInstance> capturedPokemon = new();

    public PlayerController playerController;


    //Variables de indice para guardar la posicion que estamos del menu
    private int pokeballIndex = 0;
    private int pokemonIndex = 0;


    void Start()
    {
        StartCoroutine(InitializeSelectorUI());
    }

    private System.Collections.IEnumerator InitializeSelectorUI()
    {
        yield return null; // Espera 1 frame

        SetMode(currentMode);
        UpdateUI();
    }


    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll < 0f)
            SelectNext();
        else if (scroll > 0f)
            SelectPrevious();
    }


    public void SetMode(SelectorMode mode)
    {
        currentMode = mode;
        currentIndex = mode == SelectorMode.Pokeball ? pokeballIndex : pokemonIndex;

        panelBalls.SetActive(mode == SelectorMode.Pokeball);
        panelPokemon.SetActive(mode == SelectorMode.Pokemon);

        if (mode == SelectorMode.Pokeball)
        {
            pokeballInventory = InventoryManager.Instance.inventory.FindAll(e => e.item.category == ItemCategory.Pokeball && e.unlocked);
            UpdateUI_Pokeball();
        }
        else
        {
            capturedPokemon = GameManager.Instance.playerTeam;
            UpdateUI_Pokemon();
        }
    }

    void SelectNext()
    {
        int count = currentMode == SelectorMode.Pokeball ? pokeballInventory.Count : capturedPokemon.Count;
        if (currentIndex < count - 1)
        {
            currentIndex++;
            SaveCurrentIndex();
            UpdateUI();
        }
    }


    void SelectPrevious()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            SaveCurrentIndex();
            UpdateUI();
        }
    }

    void SaveCurrentIndex()
    {
        if (currentMode == SelectorMode.Pokeball)
            pokeballIndex = currentIndex;
        else
            pokemonIndex = currentIndex;
    }


    public void UpdateUI()
    {
        SaveCurrentIndex();

        if (currentMode == SelectorMode.Pokeball)
            UpdateUI_Pokeball();
        else
            UpdateUI_Pokemon();
    }


    void UpdateUI_Pokeball()
    {
        if (pokeballInventory.Count == 0)
        {
            imgActiveBall.sprite = null;
            imgActiveBall.color = new Color(1, 1, 1, 0);
            txtNameBalls.text = "";
            txtNumberBalls.text = "";
            imgPreviousBall.enabled = false;
            imgNextBall.enabled = false;
            return;
        }

        var entry = pokeballInventory[currentIndex];
        imgActiveBall.sprite = entry.item.icon;
        imgActiveBall.color = entry.quantity > 0 ? colorDisponible : colorAgotado;
        txtNameBalls.text = entry.item.itemName;
        txtNumberBalls.text = "x" + entry.quantity;

        UpdatePreviewImages_Pokeball();
    }

    void UpdateUI_Pokemon()
    {
        if (capturedPokemon.Count == 0)
        {
            imgActivePokemon.sprite = null;
            imgActivePokemon.color = new Color(1, 1, 1, 0);
            txtNamePokemon.text = "";
            txtLevelPokemon.text = "";
            imgPreviousPokemon.enabled = false;
            imgNextPokemon.enabled = false;
            return;
        }

        PokemonInstance selectedPokemon = GetCurrentPokemon();
        PokemonInstance active = playerController?.GetActivePokemon();

        imgActivePokemon.sprite = selectedPokemon.baseData.pokemonSprite;

        bool isActive = active != null && selectedPokemon.uniqueID == active.uniqueID;
        imgActivePokemon.color = isActive ? colorAgotado : colorDisponible;
        txtLevelPokemon.text = isActive ? "Activo" : $"Nv. {selectedPokemon.level}";
        txtNamePokemon.text = selectedPokemon.baseData.pokemonName;

        UpdatePreviewImages_Pokemon();
    }


    void UpdatePreviewImages_Pokeball()
    {
        imgPreviousBall.enabled = currentIndex > 0;
        imgNextBall.enabled = currentIndex < pokeballInventory.Count - 1;

        if (imgPreviousBall.enabled)
        {
            var prev = currentIndex - 1;
            imgPreviousBall.sprite = pokeballInventory[prev].item.icon;
            imgPreviousBall.color = pokeballInventory[prev].quantity > 0 ? colorDisponible : colorAgotado;
        }

        if (imgNextBall.enabled)
        {
            var next = currentIndex + 1;
            imgNextBall.sprite = pokeballInventory[next].item.icon;
            imgNextBall.color = pokeballInventory[next].quantity > 0 ? colorDisponible : colorAgotado;
        }
    }


    void UpdatePreviewImages_Pokemon()
    {
        PokemonInstance active = playerController?.GetActivePokemon();

        imgPreviousPokemon.enabled = currentIndex > 0;
        imgNextPokemon.enabled = currentIndex < capturedPokemon.Count - 1;

        if (imgPreviousPokemon.enabled)
        {
            int prev = currentIndex - 1;
            imgPreviousPokemon.sprite = capturedPokemon[prev].baseData.pokemonSprite;
            imgPreviousPokemon.color = (active != null && capturedPokemon[prev].uniqueID == active.uniqueID) ? colorAgotado : colorDisponible;
        }

        if (imgNextPokemon.enabled)
        {
            int next = currentIndex + 1;
            imgNextPokemon.sprite = capturedPokemon[next].baseData.pokemonSprite;
            imgNextPokemon.color = (active != null && capturedPokemon[next].uniqueID == active.uniqueID) ? colorAgotado : colorDisponible;
        }
    }




    public ItemEntry GetCurrentEntry()
    {
        var item = pokeballInventory[currentIndex].item;
        int quantity = InventoryManager.Instance.GetQuantity(item);
        return new ItemEntry(item, quantity);
    }

    public PokemonInstance GetCurrentPokemon()
    {
        if (currentMode != SelectorMode.Pokemon || capturedPokemon.Count == 0) return null;
        return capturedPokemon[currentIndex];
    }

    public void RefreshCapturedPokemon()
    {
        if (currentMode == SelectorMode.Pokemon)
        {
            capturedPokemon = GameManager.Instance.playerTeam;
            UpdateUI_Pokemon();
        }
    }
}
