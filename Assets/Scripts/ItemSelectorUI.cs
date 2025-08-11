using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public enum SelectorMode { Pokeball, Pokemon }

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

    private int pokeballIndex = 0;
    private int pokemonIndex = 0;

    // ---------------- lifecycle ----------------
    private void OnEnable()
    {
        // Suscribimos a los cambios de la party
        var sm = PokemonStorageManager.Instance;
        if (sm != null) sm.OnPartyChanged += HandlePartyChanged;
    }

    private void OnDisable()
    {
        var sm = PokemonStorageManager.Instance;
        if (sm != null) sm.OnPartyChanged -= HandlePartyChanged;
    }

    void Start()
    {
        StartCoroutine(InitializeSelectorUI());
    }

    private System.Collections.IEnumerator InitializeSelectorUI()
    {
        yield return null;
        SetMode(currentMode);
        UpdateUI();
    }

    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll < 0f) SelectNext();
        else if (scroll > 0f) SelectPrevious();
    }

    // Evento: la party ha cambiado (añadir, quitar, mover, swap…)
    private void HandlePartyChanged()
    {
        // Guardamos el seleccionado actual (si existía)
        string selectedId = GetCurrentPokemon()?.UniqueID;

        BuildPartyList();                 // reconstruye SOLO con pokémon reales
        // Intentamos mantener selección
        int newIndex = 0;
        if (!string.IsNullOrEmpty(selectedId))
        {
            for (int i = 0; i < capturedPokemon.Count; i++)
                if (capturedPokemon[i].UniqueID == selectedId) { newIndex = i; break; }
        }

        currentIndex = Mathf.Clamp(newIndex, 0, Mathf.Max(0, capturedPokemon.Count - 1));
        pokemonIndex = currentIndex;

        // Refrescamos la UI si estamos en modo Pokémon
        if (currentMode == SelectorMode.Pokemon) UpdateUI_Pokemon();
    }

    // -------------------- Modo --------------------
    public void SetMode(SelectorMode mode)
    {
        currentMode = mode;
        currentIndex = mode == SelectorMode.Pokeball ? pokeballIndex : pokemonIndex;

        panelBalls.SetActive(mode == SelectorMode.Pokeball);
        panelPokemon.SetActive(mode == SelectorMode.Pokemon);

        if (mode == SelectorMode.Pokeball)
        {
            BuildPokeballList();
            ClampCurrentIndex(pokeballInventory.Count);
            UpdateUI_Pokeball();
        }
        else
        {
            BuildPartyList(); // filtra huecos nulos
            ClampCurrentIndex(capturedPokemon.Count);
            UpdateUI_Pokemon();
        }
    }

    // -------------------- List builders --------------------
    private void BuildPokeballList()
    {
        pokeballInventory = InventoryManager.Instance.inventory
            .FindAll(e => e.item.category == ItemCategory.Pokeball && e.unlocked);
    }

    private void BuildPartyList()
    {
        capturedPokemon = new List<PokemonInstance>();

        var party = PokemonStorageManager.Instance?.PlayerParty;
        if (party == null) return;

        // Usa el ToList() que expone tu PokemonParty (6 slots con null en huecos)
        var slots = party.ToList();
        for (int i = 0; i < slots.Count; i++)
        {
            var p = slots[i];
            if (p != null && p.species != null)
                capturedPokemon.Add(p);
        }
    }

    // -------------------- Navegación --------------------
    void SelectNext()
    {
        int count = currentMode == SelectorMode.Pokeball ? pokeballInventory.Count : capturedPokemon.Count;
        if (count == 0) return;
        if (currentIndex < count - 1)
        {
            currentIndex++;
            SaveCurrentIndex();
            UpdateUI();
        }
    }

    void SelectPrevious()
    {
        int count = currentMode == SelectorMode.Pokeball ? pokeballInventory.Count : capturedPokemon.Count;
        if (count == 0) return;
        if (currentIndex > 0)
        {
            currentIndex--;
            SaveCurrentIndex();
            UpdateUI();
        }
    }

    void SaveCurrentIndex()
    {
        if (currentMode == SelectorMode.Pokeball) pokeballIndex = currentIndex;
        else pokemonIndex = currentIndex;
    }

    void ClampCurrentIndex(int count)
    {
        if (count <= 0) { currentIndex = 0; SaveCurrentIndex(); return; }
        currentIndex = Mathf.Clamp(currentIndex, 0, count - 1);
        SaveCurrentIndex();
    }

    public void UpdateUI()
    {
        if (currentMode == SelectorMode.Pokeball)
        {
            BuildPokeballList();
            ClampCurrentIndex(pokeballInventory.Count);
            UpdateUI_Pokeball();
        }
        else
        {
            BuildPartyList(); // re-filtra por si la party cambió
            ClampCurrentIndex(capturedPokemon.Count);
            UpdateUI_Pokemon();
        }
    }

    // -------------------- Pokéballs --------------------
    void UpdateUI_Pokeball()
    {
        if (pokeballInventory.Count == 0)
        {
            HideImage(imgActiveBall);
            txtNameBalls.text = "";
            txtNumberBalls.text = "";
            HideImage(imgPreviousBall);
            HideImage(imgNextBall);
            return;
        }

        var entry = pokeballInventory[currentIndex];
        ShowImage(imgActiveBall, entry.item.icon, entry.quantity > 0 ? colorDisponible : colorAgotado);
        txtNameBalls.text = entry.item.itemName;
        txtNumberBalls.text = "x" + entry.quantity;

        // Prev
        if (currentIndex > 0)
        {
            var e = pokeballInventory[currentIndex - 1];
            ShowImage(imgPreviousBall, e.item.icon, e.quantity > 0 ? colorDisponible : colorAgotado);
        }
        else HideImage(imgPreviousBall);

        // Next
        if (currentIndex < pokeballInventory.Count - 1)
        {
            var e = pokeballInventory[currentIndex + 1];
            ShowImage(imgNextBall, e.item.icon, e.quantity > 0 ? colorDisponible : colorAgotado);
        }
        else HideImage(imgNextBall);
    }

    // -------------------- Pokémon --------------------
    void UpdateUI_Pokemon()
    {
        if (capturedPokemon.Count == 0)
        {
            HideImage(imgActivePokemon);
            txtNamePokemon.text = "";
            txtLevelPokemon.text = "";
            HideImage(imgPreviousPokemon);
            HideImage(imgNextPokemon);
            return;
        }

        var selected = capturedPokemon[currentIndex];
        var active = playerController ? playerController.GetActivePokemon() : null;

        bool isActive = active != null && selected.UniqueID == active.UniqueID;
        ShowImage(imgActivePokemon, selected.species.pokemonSprite, isActive ? colorAgotado : colorDisponible);
        txtNamePokemon.text = selected.species.pokemonName;
        txtLevelPokemon.text = isActive ? "Activo" : $"Nv. {selected.level}";

        // Prev
        if (currentIndex > 0)
        {
            var prev = capturedPokemon[currentIndex - 1];
            var col = (active != null && prev.UniqueID == active.UniqueID) ? colorAgotado : colorDisponible;
            ShowImage(imgPreviousPokemon, prev.species.pokemonSprite, col);
        }
        else HideImage(imgPreviousPokemon);

        // Next
        if (currentIndex < capturedPokemon.Count - 1)
        {
            var next = capturedPokemon[currentIndex + 1];
            var col = (active != null && next.UniqueID == active.UniqueID) ? colorAgotado : colorDisponible;
            ShowImage(imgNextPokemon, next.species.pokemonSprite, col);
        }
        else HideImage(imgNextPokemon);
    }

    // -------------------- Helpers de imagen --------------------
    static void HideImage(Image img)
    {
        if (!img) return;
        img.sprite = null;
        img.color = new Color(1, 1, 1, 0); // alpha 0
        img.enabled = false;
        img.gameObject.SetActive(false);
    }

    static void ShowImage(Image img, Sprite sprite, Color color)
    {
        if (!img) return;
        if (sprite == null) { HideImage(img); return; }
        img.gameObject.SetActive(true);
        img.enabled = true;
        img.sprite = sprite;
        img.color = color; // alpha 1 en 'color'
    }

    // -------------------- API pública --------------------
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
        BuildPartyList();
        ClampCurrentIndex(capturedPokemon.Count);
        if (currentMode == SelectorMode.Pokemon) UpdateUI_Pokemon();
    }
}
