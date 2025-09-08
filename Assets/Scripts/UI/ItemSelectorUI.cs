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
    public Color colorAgotado = new Color(1f, 1f, 1f, 0.35f);
    public Color colorDebilitado = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Q;

    private SelectorMode currentMode = SelectorMode.Pokeball;

    private List<ItemEntry> pokeballInventory = new List<ItemEntry>();
    private int ballIndex = 0;

    private List<PokemonInstance> partyList = new List<PokemonInstance>();
    private int pokemonIndex = 0;

    private PlayerController playerController;

    private bool captureLock = false;
    public bool IsCaptureLocked => captureLock;

    private void Awake()
    {
        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
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
        else
        {
            UpdateUI();
        }
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

    // ---------- Data builders ----------
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
            if (txtNameBalls) txtNameBalls.text = "—";
            if (txtCountBalls) txtCountBalls.text = "x0";
            return;
        }

        var entry = pokeballInventory[ballIndex];
        var icon = IconFromItem(entry);
        string display = NameFromItem(entry);
        int qty = Mathf.Max(0, entry.quantity);

        if (imgActiveBall)
        {
            imgActiveBall.enabled = (icon != null);
            imgActiveBall.sprite = icon;
            imgActiveBall.color = qty > 0 ? colorDisponible : colorAgotado;
        }
        if (txtNameBalls) txtNameBalls.text = display;
        if (txtCountBalls) txtCountBalls.text = "x" + qty;

        if (imgPreviousBall)
        {
            if (ballIndex > 0)
            {
                var prevIcon = IconFromItem(pokeballInventory[ballIndex - 1]);
                imgPreviousBall.enabled = (prevIcon != null);
                imgPreviousBall.sprite = prevIcon;
                imgPreviousBall.color = colorDisponible;
            }
            else { imgPreviousBall.enabled = false; imgPreviousBall.sprite = null; }
        }

        if (imgNextBall)
        {
            if (ballIndex < pokeballInventory.Count - 1)
            {
                var nextIcon = IconFromItem(pokeballInventory[ballIndex + 1]);
                imgNextBall.enabled = (nextIcon != null);
                imgNextBall.sprite = nextIcon;
                imgNextBall.color = colorDisponible;
            }
            else { imgNextBall.enabled = false; imgNextBall.sprite = null; }
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

        if (isFainted) { centerTint = colorDebilitado; stateText = "Debilitado"; isActive = false; }
        else if (isActive) { centerTint = colorAgotado; stateText = "Activo"; }

        ShowImage(imgActivePokemon, selected.species.pokemonSprite, centerTint);
        if (txtNamePokemon) txtNamePokemon.text = selected.species.pokemonName;
        if (txtLevelPokemon) txtLevelPokemon.text = stateText;

        if (pokemonIndex > 0)
        {
            var prev = partyList[pokemonIndex - 1];
            bool prevIsActive = active != null && prev.UniqueID == active.UniqueID && prev.currentHP > 0;
            bool prevKO = prev.currentHP <= 0;
            Color tint = prevKO ? colorDebilitado : (prevIsActive ? colorAgotado : colorDisponible);
            ShowImage(imgPreviousPokemon, prev.species.pokemonSprite, tint);
        }
        else HideImage(imgPreviousPokemon);

        if (pokemonIndex < partyList.Count - 1)
        {
            var next = partyList[pokemonIndex + 1];
            bool nextIsActive = active != null && next.UniqueID == active.UniqueID && next.currentHP > 0;
            bool nextKO = next.currentHP <= 0;
            Color tint = nextKO ? colorDebilitado : (nextIsActive ? colorAgotado : colorDisponible);
            ShowImage(imgNextPokemon, next.species.pokemonSprite, tint);
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
