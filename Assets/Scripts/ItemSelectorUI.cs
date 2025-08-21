using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public enum SelectorMode { Pokeball, Pokemon }

public class ItemSelectorUI : MonoBehaviour
{
    public SelectorMode CurrentMode => currentMode;

    [Header("UI - Pokéballs")]
    public GameObject panelBalls;
    public Image imgPreviousBall;   // opcional
    public Image imgActiveBall;
    public Image imgNextBall;       // opcional
    public TextMeshProUGUI txtNameBalls;
    public TextMeshProUGUI txtCountBalls;     // "xN"

    [Header("UI - Pokémon")]
    public GameObject panelPokemon;
    public Image imgPreviousPokemon;
    public Image imgActivePokemon;
    public Image imgNextPokemon;
    public TextMeshProUGUI txtNamePokemon;
    public TextMeshProUGUI txtLevelPokemon;

    [Header("Colores")]
    public Color colorDisponible = Color.white;
    public Color colorAgotado = new Color(1f, 1f, 1f, 0.35f); // grisear cuando no usable

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Q; // cambiar modo

    private SelectorMode currentMode = SelectorMode.Pokeball;

    // Inventario (usa tu tipo ItemEntry definido en InventoryManager)
    private List<ItemEntry> pokeballInventory = new List<ItemEntry>();
    private int ballIndex = 0;

    // Party
    private List<PokemonInstance> partyList = new List<PokemonInstance>();
    private int pokemonIndex = 0;

    private PlayerController playerController;

    // --------------- Ciclo de vida ---------------
    private void Awake()
    {
        playerController = FindObjectOfType<PlayerController>();
        RebuildBalls();
        RebuildParty();
        ClampIndices();
        UpdateUI();               // primer pintado
    }

    private void OnEnable()
    {
        // Si este panel se activa después de que el inventario ya esté cargado, refresca
        RebuildBalls();
        ClampIndices();
        UpdateUI();
    }

    private void Start()
    {
        // Refresco diferido un frame por si InventoryManager popula en Start
        StartCoroutine(DeferredInitialRefresh());
    }

    private System.Collections.IEnumerator DeferredInitialRefresh()
    {
        yield return null; // siguiente frame
        RefreshBalls();
        RefreshCapturedPokemon();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetMode(currentMode == SelectorMode.Pokeball ? SelectorMode.Pokemon : SelectorMode.Pokeball);
        }

        // --- Navegación por rueda del ratón (no cíclica) ---
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (currentMode == SelectorMode.Pokeball)
            {
                if (pokeballInventory.Count > 0)
                {
                    if (scroll > 0f) ballIndex = Mathf.Max(0, ballIndex - 1);                                  // arriba → anterior
                    else ballIndex = Mathf.Min(pokeballInventory.Count - 1, ballIndex + 1);       // abajo → siguiente
                    UpdateUI_Balls();
                }
            }
            else // Pokémon
            {
                if (partyList.Count > 0)
                {
                    if (scroll > 0f) pokemonIndex = Mathf.Max(0, pokemonIndex - 1);
                    else pokemonIndex = Mathf.Min(partyList.Count - 1, pokemonIndex + 1);
                    UpdateUI_Pokemon();
                }
            }
        }
    }

    // --------------- API pública ---------------
    public void SetMode(SelectorMode mode)
    {
        currentMode = mode;
        UpdateUI();
    }

    public PokeballData GetSelectedBallData()
    {
        var e = GetSelectedBallEntry();
        return e != null ? e.item as PokeballData : null;
    }

    public ItemEntry GetSelectedBallEntry()
    {
        if (pokeballInventory.Count == 0) return null;
        return pokeballInventory[ballIndex];
    }

    /// Consume 1 unidad de la ball seleccionada si hay cantidad>0. Actualiza UI. Devuelve true si consumió.
    public bool TryConsumeSelectedBall()
    {
        var entry = GetSelectedBallEntry();
        if (entry == null) return false;
        if (!entry.unlocked || entry.quantity <= 0) { UpdateUI_Balls(); return false; }

        entry.quantity = Mathf.Max(0, entry.quantity - 1);
        UpdateUI_Balls();
        return true;
    }

    public PokemonInstance GetCurrentPokemon()
    {
        if (partyList.Count == 0) return null;
        return partyList[pokemonIndex];
    }

    public void RefreshCapturedPokemon()
    {
        RebuildParty();
        ClampIndices();
        if (currentMode == SelectorMode.Pokemon) UpdateUI_Pokemon();
    }

    public void RefreshBalls()
    {
        RebuildBalls();
        ClampIndices();
        if (currentMode == SelectorMode.Pokeball) UpdateUI_Balls();
    }

    // --------------- Data builders ---------------
    private void RebuildBalls()
    {
        pokeballInventory.Clear();

        var inv = InventoryManager.Instance;
        if (inv == null || inv.inventory == null) return;

        foreach (var entry in inv.inventory)
        {
            if (entry == null || entry.item == null) continue;
            if (entry.item.category != ItemCategory.Pokeball) continue;
            if (!entry.unlocked) continue; // mostramos solo las desbloqueadas
            // ⛔ YA NO filtramos por cantidad>0: queremos verlas aunque estén a 0 (se grisearán)
            if (entry.item is PokeballData)
                pokeballInventory.Add(entry);
        }

        ballIndex = Mathf.Clamp(ballIndex, 0, Mathf.Max(0, pokeballInventory.Count - 1));
    }

    private void RebuildParty()
    {
        partyList.Clear();

        var party = PokemonStorageManager.Instance?.PlayerParty;
        if (party == null) return;

        var slots = party.ToList(); // 6 slots con null en huecos
        foreach (var p in slots)
        {
            if (p == null || p.species == null) continue;
            partyList.Add(p);
        }

        pokemonIndex = Mathf.Clamp(pokemonIndex, 0, Mathf.Max(0, partyList.Count - 1));
    }

    private void ClampIndices()
    {
        ballIndex = Mathf.Clamp(ballIndex, 0, Mathf.Max(0, pokeballInventory.Count - 1));
        pokemonIndex = Mathf.Clamp(pokemonIndex, 0, Mathf.Max(0, partyList.Count - 1));
    }

    // --------------- UI ---------------
    private void UpdateUI()
    {
        if (panelBalls) panelBalls.SetActive(currentMode == SelectorMode.Pokeball);
        if (panelPokemon) panelPokemon.SetActive(currentMode == SelectorMode.Pokemon);

        if (currentMode == SelectorMode.Pokeball) UpdateUI_Balls();
        else UpdateUI_Pokemon();
    }

    private Sprite IconFromItem(ItemEntry entry)
    {
        if (entry == null || entry.item == null) return null;
        var data = entry.item as ItemData;
        return data != null ? data.icon : null;
    }

    private string NameFromItem(ItemEntry entry)
    {
        if (entry == null || entry.item == null) return "—";
        if (entry.item is ItemData id && !string.IsNullOrEmpty(id.itemName)) return id.itemName;
        return entry.item.name;
    }

    private void UpdateUI_Balls()
    {
        // No reconstruyo aquí para no alterar índices durante el scroll
        ClampIndices();

        if (pokeballInventory.Count == 0)
        {
            // Oculta todas las imágenes si no hay balls
            if (imgActiveBall) { imgActiveBall.enabled = false; imgActiveBall.sprite = null; }
            if (imgPreviousBall) { imgPreviousBall.enabled = false; imgPreviousBall.sprite = null; }
            if (imgNextBall) { imgNextBall.enabled = false; imgNextBall.sprite = null; }
            if (txtNameBalls) txtNameBalls.text = "—";
            if (txtCountBalls) txtCountBalls.text = "x0";
            return;
        }

        var entry = pokeballInventory[ballIndex];
        var icon = IconFromItem(entry);
        string display = NameFromItem(entry);
        int qty = Mathf.Max(0, entry.quantity);

        // Centro
        if (imgActiveBall)
        {
            imgActiveBall.enabled = (icon != null);
            imgActiveBall.sprite = icon;
            imgActiveBall.color = qty > 0 ? colorDisponible : colorAgotado; // gris si 0
        }
        if (txtNameBalls) txtNameBalls.text = display;
        if (txtCountBalls) txtCountBalls.text = "x" + qty;

        // Previa
        if (imgPreviousBall)
        {
            if (ballIndex > 0)
            {
                var prevIcon = IconFromItem(pokeballInventory[ballIndex - 1]);
                imgPreviousBall.enabled = (prevIcon != null);
                imgPreviousBall.sprite = prevIcon;
                // (sin grisear; es solo preview)
            }
            else
            {
                imgPreviousBall.enabled = false;
                imgPreviousBall.sprite = null;
            }
        }

        // Siguiente
        if (imgNextBall)
        {
            if (ballIndex < pokeballInventory.Count - 1)
            {
                var nextIcon = IconFromItem(pokeballInventory[ballIndex + 1]);
                imgNextBall.enabled = (nextIcon != null);
                imgNextBall.sprite = nextIcon;
            }
            else
            {
                imgNextBall.enabled = false;
                imgNextBall.sprite = null;
            }
        }
    }

    private void UpdateUI_Pokemon()
    {
        // Igual que balls: no reconstruyo aquí para mantener navegación fluida
        ClampIndices();

        if (partyList.Count == 0)
        {
            HideImage(imgPreviousPokemon);
            HideImage(imgActivePokemon);
            HideImage(imgNextPokemon);
            if (txtNamePokemon) txtNamePokemon.text = "—";
            if (txtLevelPokemon) txtLevelPokemon.text = "—";
            return;
        }

        var selected = partyList[pokemonIndex];
        var active = playerController ? playerController.GetActivePokemon() : null;

        bool isActive = active != null && selected.UniqueID == active.UniqueID;
        bool isFainted = selected.currentHP <= 0;

        // Centro
        ShowImage(imgActivePokemon, selected.species.pokemonSprite, (isActive || isFainted) ? colorAgotado : colorDisponible);
        if (txtNamePokemon) txtNamePokemon.text = selected.species.pokemonName;
        if (txtLevelPokemon) txtLevelPokemon.text = isActive ? "Activo" : (isFainted ? "Debilitado" : $"Nv. {selected.level}");

        // Prev (solo si existe)
        if (pokemonIndex > 0)
        {
            var prev = partyList[pokemonIndex - 1];
            bool prevActive = active != null && prev.UniqueID == active.UniqueID;
            bool prevFainted = prev.currentHP <= 0;
            ShowImage(imgPreviousPokemon, prev.species.pokemonSprite, (prevActive || prevFainted) ? colorAgotado : colorDisponible);
        }
        else HideImage(imgPreviousPokemon);

        // Next (solo si existe)
        if (pokemonIndex < partyList.Count - 1)
        {
            var next = partyList[pokemonIndex + 1];
            bool nextActive = active != null && next.UniqueID == active.UniqueID;
            bool nextFainted = next.currentHP <= 0;
            ShowImage(imgNextPokemon, next.species.pokemonSprite, (nextActive || nextFainted) ? colorAgotado : colorDisponible);
        }
        else HideImage(imgNextPokemon);
    }

    private static void ShowImage(Image img, Sprite s, Color tint)
    {
        if (!img) return;
        img.enabled = (s != null);
        img.sprite = s;
        img.color = tint;
    }

    private static void HideImage(Image img)
    {
        if (!img) return;
        img.enabled = false;
        img.sprite = null;
    }
}
