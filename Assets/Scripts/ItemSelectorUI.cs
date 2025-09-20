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

    [Header("Fondos prev/next - Pokéballs")]
    public Image bgPreviousBall;    // opcional
    public Image bgNextBall;        // opcional

    [Header("UI - Pokémon")]
    public GameObject panelPokemon;
    public Image imgPreviousPokemon;
    public Image imgActivePokemon;
    public Image imgNextPokemon;
    public TextMeshProUGUI txtNamePokemon;
    public TextMeshProUGUI txtLevelPokemon;

    [Header("Fondos prev/next - Pokémon")]
    public Image bgPreviousPokemon; // opcional
    public Image bgNextPokemon;     // opcional

    [Header("Colores")]
    public Color colorDisponible = Color.white;                    // normal
    public Color colorAgotado = new Color(1f, 1f, 1f, 0.35f);      // sin stock o activo
    public Color colorDebilitado = new Color(1f, 0.2f, 0.2f, 1f);  // KO (rojo)

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

    // Bloqueo durante “modo Capturar”
    private bool captureLock = false;
    public bool IsCaptureLocked => captureLock;

    private void Awake()
    {
        playerController = FindObjectOfType<PlayerController>();
        RebuildBalls();
        RebuildParty();
        ClampIndices();
        UpdateUI();
    }

    private void OnEnable()
    {
        RebuildBalls();
        RebuildParty();
        ClampIndices();
        UpdateUI();
    }

    private void Start()
    {
        StartCoroutine(DeferredInitialRefresh());
    }

    private System.Collections.IEnumerator DeferredInitialRefresh()
    {
        yield return null;
        RefreshBalls();
        RefreshCapturedPokemon();
    }

    private void Update()
    {
        if (!captureLock && Input.GetKeyDown(toggleKey))
        {
            SetMode(currentMode == SelectorMode.Pokeball ? SelectorMode.Pokemon : SelectorMode.Pokeball);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (currentMode == SelectorMode.Pokeball)
            {
                if (pokeballInventory.Count > 0)
                {
                    if (scroll > 0f) ballIndex = Mathf.Max(0, ballIndex - 1);
                    else ballIndex = Mathf.Min(pokeballInventory.Count - 1, ballIndex + 1);
                    UpdateUI_Balls();
                }
            }
            else
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

    // ---------- API ----------
    public void SetMode(SelectorMode mode)
    {
        if (captureLock) mode = SelectorMode.Pokeball;
        currentMode = mode;
        UpdateUI();
    }

    public void SetCaptureLock(bool locked)
    {
        captureLock = locked;
        if (captureLock)
        {
            currentMode = SelectorMode.Pokeball;
            RefreshBalls();
        }
        else UpdateUI();
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
        else UpdateUI_Balls();
    }

    public void RefreshBalls()
    {
        RebuildBalls();
        ClampIndices();
        UpdateUI_Balls();
    }

    // ---------- Data ----------
    private void RebuildBalls()
    {
        pokeballInventory.Clear();

        var inv = InventoryManager.Instance;
        if (inv == null || inv.inventory == null) return;

        foreach (var entry in inv.inventory)
        {
            if (entry == null || entry.item == null) continue;
            if (entry.item.category != ItemCategory.Pokeball) continue;
            if (!entry.unlocked) continue;
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

        var slots = party.ToList();
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

    // ---------- UI ----------
    public void UpdateUI()
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
        ClampIndices();

        if (pokeballInventory.Count == 0)
        {
            if (imgActiveBall) { imgActiveBall.enabled = false; imgActiveBall.sprite = null; }
            if (imgPreviousBall) { imgPreviousBall.enabled = false; imgPreviousBall.sprite = null; }
            if (imgNextBall) { imgNextBall.enabled = false; imgNextBall.sprite = null; }
            if (bgPreviousBall) bgPreviousBall.enabled = false;
            if (bgNextBall) bgNextBall.enabled = false;
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
            imgActiveBall.color = qty > 0 ? colorDisponible : colorAgotado;
        }
        if (txtNameBalls) txtNameBalls.text = display;
        if (txtCountBalls) txtCountBalls.text = "x" + qty;

        // Previa
        if (imgPreviousBall)
        {
            if (ballIndex > 0)
            {
                var prevEntry = pokeballInventory[ballIndex - 1];
                var prevIcon = IconFromItem(prevEntry);
                imgPreviousBall.enabled = (prevIcon != null);
                imgPreviousBall.sprite = prevIcon;
                imgPreviousBall.color = (prevEntry.quantity > 0) ? colorDisponible : colorAgotado;
                if (bgPreviousBall) bgPreviousBall.enabled = true;
            }
            else
            {
                imgPreviousBall.enabled = false;
                imgPreviousBall.sprite = null;
                if (bgPreviousBall) bgPreviousBall.enabled = false;
            }
        }

        // Siguiente
        if (imgNextBall)
        {
            if (ballIndex < pokeballInventory.Count - 1)
            {
                var nextEntry = pokeballInventory[ballIndex + 1];
                var nextIcon = IconFromItem(nextEntry);
                imgNextBall.enabled = (nextIcon != null);
                imgNextBall.sprite = nextIcon;
                imgNextBall.color = (nextEntry.quantity > 0) ? colorDisponible : colorAgotado;
                if (bgNextBall) bgNextBall.enabled = true;
            }
            else
            {
                imgNextBall.enabled = false;
                imgNextBall.sprite = null;
                if (bgNextBall) bgNextBall.enabled = false;
            }
        }
    }

    private void UpdateUI_Pokemon()
    {
        ClampIndices();

        if (partyList.Count == 0)
        {
            HideImage(imgPreviousPokemon);
            HideImage(imgActivePokemon);
            HideImage(imgNextPokemon);
            if (bgPreviousPokemon) bgPreviousPokemon.enabled = false;
            if (bgNextPokemon) bgNextPokemon.enabled = false;
            if (txtNamePokemon) txtNamePokemon.text = "—";
            if (txtLevelPokemon) txtLevelPokemon.text = "—";
            return;
        }

        var selected = partyList[pokemonIndex];
        var active = playerController ? playerController.GetActivePokemon() : null;

        bool isActive = active != null && selected.UniqueID == active.UniqueID;
        bool isFainted = selected.currentHP <= 0;

        Color centerTint = colorDisponible;
        string stateText = $"Nv. {selected.level}";

        if (isFainted)
        {
            centerTint = colorDebilitado;
            stateText = "Debilitado";
            isActive = false;
        }
        else if (isActive)
        {
            centerTint = colorAgotado;
            stateText = "Activo";
        }

        ShowImage(imgActivePokemon, selected.species.pokemonSprite, centerTint);
        if (txtNamePokemon) txtNamePokemon.text = selected.species.pokemonName;
        if (txtLevelPokemon) txtLevelPokemon.text = stateText;

        // Prev
        if (pokemonIndex > 0)
        {
            var prev = partyList[pokemonIndex - 1];
            bool prevIsActive = active != null && prev.UniqueID == active.UniqueID && prev.currentHP > 0;
            bool prevKO = prev.currentHP <= 0;
            Color tint = prevKO ? colorDebilitado : (prevIsActive ? colorAgotado : colorDisponible);
            ShowImage(imgPreviousPokemon, prev.species.pokemonSprite, tint);
            if (bgPreviousPokemon) bgPreviousPokemon.enabled = true;
        }
        else
        {
            HideImage(imgPreviousPokemon);
            if (bgPreviousPokemon) bgPreviousPokemon.enabled = false;
        }

        // Next
        if (pokemonIndex < partyList.Count - 1)
        {
            var next = partyList[pokemonIndex + 1];
            bool nextIsActive = active != null && next.UniqueID == active.UniqueID && next.currentHP > 0;
            bool nextKO = next.currentHP <= 0;
            Color tint = nextKO ? colorDebilitado : (nextIsActive ? colorAgotado : colorDisponible);
            ShowImage(imgNextPokemon, next.species.pokemonSprite, tint);
            if (bgNextPokemon) bgNextPokemon.enabled = true;
        }
        else
        {
            HideImage(imgNextPokemon);
            if (bgNextPokemon) bgNextPokemon.enabled = false;
        }
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

    public bool FocusPokemonById(string uniqueID)
    {
        RebuildParty();
        ClampIndices();
        if (partyList.Count == 0)
        {
            SetMode(SelectorMode.Pokemon);
            UpdateUI_Pokemon();
            return false;
        }

        if (!string.IsNullOrEmpty(uniqueID))
        {
            int idx = partyList.FindIndex(p => p != null && p.UniqueID == uniqueID);
            if (idx >= 0) pokemonIndex = idx;
        }

        SetMode(SelectorMode.Pokemon);
        UpdateUI_Pokemon();
        return true;
    }
}
