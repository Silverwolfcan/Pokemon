// UI/PokemonTeamPanel.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PokemonTeamPanel : MonoBehaviour
{
    [Header("Grids")]
    [SerializeField] private StorageGridUI partyGrid;
    [SerializeField] private StorageGridUI pcGrid;

    [Header("PC Box")]
    [SerializeField] private TMP_Text txtBoxTitle;
    [SerializeField] private Button btnPrevBox;
    [SerializeField] private Button btnNextBox;

    [Header("Movimientos actuales")]
    [SerializeField] private MoveGridUI moveGrid; // grid de 4

    [Header("Aprendibles")]
    [SerializeField] private Button btnToggleLearnset;
    [SerializeField] private GameObject learnsetRoot;   // contenedor visible
    [SerializeField] private MoveGridUI learnsetGrid;   // grid de aprendibles

    [Header("Stats")]
    [SerializeField] private UIAssetsRegistry assets;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtLevel;
    [SerializeField] private Image imgGender;
    [SerializeField] private Image imgType1;
    [SerializeField] private Image imgType2;
    [SerializeField] private TMP_Text txtAbility;
    [SerializeField] private TMP_Text txtHeldItem;
    [SerializeField] private PokemonRadarChart radar;

    [Header("Opciones")]
    [SerializeField] private bool autoSelectFirstOnOpen = true;
    [SerializeField, Min(0.05f)] private float hoverFirstDelay = 0.40f;
    [SerializeField, Min(0.05f)] private float hoverRepeatDelay = 0.25f;

    private PokemonInstance current;
    private PokemonStorageManager SM => PokemonStorageManager.Instance;

    private enum HoverZone { None, Prev, Next }
    private HoverZone hoverZone = HoverZone.None;
    private float hoverTimer;
    private Canvas rootCanvas;
    private Camera uiCamera;

    private void OnEnable()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        uiCamera = rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

        if (btnPrevBox) { btnPrevBox.onClick.RemoveAllListeners(); btnPrevBox.onClick.AddListener(PrevBox); }
        if (btnNextBox) { btnNextBox.onClick.RemoveAllListeners(); btnNextBox.onClick.AddListener(NextBox); }

        if (partyGrid) { partyGrid.SetMode(StorageGridUI.GridMode.Party); partyGrid.onPokemonClicked.RemoveAllListeners(); partyGrid.onPokemonClicked.AddListener(OnSelectPokemon); }
        if (pcGrid) { pcGrid.SetMode(StorageGridUI.GridMode.PCBox); pcGrid.onPokemonClicked.RemoveAllListeners(); pcGrid.onPokemonClicked.AddListener(OnSelectPokemon); }

        if (btnToggleLearnset)
        {
            btnToggleLearnset.onClick.RemoveAllListeners();
            btnToggleLearnset.onClick.AddListener(ToggleLearnsetPanel);
        }

        if (SM != null)
        {
            SM.OnPartyChanged += HandlePartyChanged;
            SM.OnPcBoxChanged += HandlePcChanged;
        }

        HideLearnset();
        RefreshAll();
        if (autoSelectFirstOnOpen) SelectFirstPartyPokemon();
        UpdateBoxButtons();
        hoverZone = HoverZone.None;
        hoverTimer = 0f;
    }

    private void OnDisable()
    {
        if (SM != null)
        {
            SM.OnPartyChanged -= HandlePartyChanged;
            SM.OnPcBoxChanged -= HandlePcChanged;
        }
        if (partyGrid) partyGrid.onPokemonClicked.RemoveAllListeners();
        if (pcGrid) pcGrid.onPokemonClicked.RemoveAllListeners();
        if (btnToggleLearnset) btnToggleLearnset.onClick.RemoveAllListeners();

        ClearSelectionAndUI();
        HideLearnset();
        hoverZone = HoverZone.None;
        hoverTimer = 0f;
    }

    private void Update()
    {
        // Auto-paginación PCBox al arrastrar
        var dd = DragDropController.Instance;
        bool dragging = dd != null && dd.IsDraggingAny;
        if (!dragging || (btnPrevBox == null && btnNextBox == null))
        {
            hoverZone = HoverZone.None;
            hoverTimer = 0f;
        }
        else
        {
            Vector2 mp = Input.mousePosition;
            bool overPrev = btnPrevBox && btnPrevBox.gameObject.activeInHierarchy &&
                            RectTransformUtility.RectangleContainsScreenPoint(btnPrevBox.GetComponent<RectTransform>(), mp, uiCamera);
            bool overNext = btnNextBox && btnNextBox.gameObject.activeInHierarchy &&
                            RectTransformUtility.RectangleContainsScreenPoint(btnNextBox.GetComponent<RectTransform>(), mp, uiCamera);

            var newZone = overPrev ? HoverZone.Prev : (overNext ? HoverZone.Next : HoverZone.None);
            if (newZone != hoverZone)
            {
                hoverZone = newZone;
                hoverTimer = hoverZone == HoverZone.None ? 0f : hoverFirstDelay;
            }
            if (hoverZone != HoverZone.None)
            {
                hoverTimer -= Time.unscaledDeltaTime;
                if (hoverTimer <= 0f)
                {
                    if (hoverZone == HoverZone.Prev) PrevBox(); else NextBox();
                    hoverTimer = hoverRepeatDelay;
                }
            }
        }

        // Cierre por clic fuera: no cerrar si el clic cae en learnsetRoot o en el botón toggle
        if (learnsetRoot && learnsetRoot.activeSelf && Input.GetMouseButtonDown(0))
        {
            bool overLearnset = IsPointerOverHierarchy(learnsetRoot);
            bool overToggle = btnToggleLearnset && IsPointerOverHierarchy(btnToggleLearnset.gameObject);
            if (!overLearnset && !overToggle) HideLearnset();
        }
    }

    // API
    public void PrevBox() => OnPrevBox();
    public void NextBox() => OnNextBox();

    public void ClearSelectionAndUI()
    {
        StorageSlotUI.ClearGlobalSelectionVisuals();
        current = null;
        moveGrid?.SetPokemon(null);
        ClearStats();
    }

    // Callbacks
    private void HandlePartyChanged()
    {
        partyGrid?.Refresh();
        if (current == null) SelectFirstPartyPokemon();
        RefreshStats(current);
        moveGrid?.Refresh();
        RefreshLearnsetList(false);
    }

    private void HandlePcChanged()
    {
        pcGrid?.Refresh();
        UpdateBoxTitle();
        UpdateBoxButtons();
    }

    // Paginación
    private void OnPrevBox()
    {
        if (SM == null || SM.PcStorage == null) return;
        int i = Mathf.Max(0, SM.PcStorage.ActiveBoxIndex - 1);
        if (i == SM.PcStorage.ActiveBoxIndex) return;
        SM.PcStorage.SetActiveBox(i);
        HandlePcChanged();
    }

    private void OnNextBox()
    {
        if (SM == null || SM.PcStorage == null) return;
        int i = Mathf.Min(SM.PcStorage.UnlockedBoxCount - 1, SM.PcStorage.ActiveBoxIndex + 1);
        if (i == SM.PcStorage.ActiveBoxIndex) return;
        SM.PcStorage.SetActiveBox(i);
        HandlePcChanged();
    }

    private void UpdateBoxButtons()
    {
        if (!btnPrevBox || !btnNextBox || SM == null || SM.PcStorage == null) return;
        int idx = Mathf.Max(0, SM.PcStorage.ActiveBoxIndex);
        int total = Mathf.Max(1, SM.PcStorage.UnlockedBoxCount);
        bool prevOn = idx > 0;
        bool nextOn = idx < total - 1;
        if (btnPrevBox.gameObject.activeSelf != prevOn) btnPrevBox.gameObject.SetActive(prevOn);
        if (btnNextBox.gameObject.activeSelf != nextOn) btnNextBox.gameObject.SetActive(nextOn);
        if (!prevOn && hoverZone == HoverZone.Prev) { hoverZone = HoverZone.None; hoverTimer = 0f; }
        if (!nextOn && hoverZone == HoverZone.Next) { hoverZone = HoverZone.None; hoverTimer = 0f; }
    }

    // Selección
    private void OnSelectPokemon(PokemonInstance p)
    {
        if (p == null) return;
        current = p;
        moveGrid?.SetPokemon(current);
        RefreshStats(current);
        RefreshLearnsetList(learnsetRoot && learnsetRoot.activeSelf);
    }

    private void SelectFirstPartyPokemon()
    {
        if (SM == null || SM.PlayerParty == null) { ClearStats(); return; }
        int firstIndex = -1; PokemonInstance first = null;
        var party = SM.PlayerParty;
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            if (p != null) { first = p; firstIndex = i; break; }
        }
        current = first;
        moveGrid?.SetPokemon(first);
        RefreshStats(first);
        if (partyGrid != null && firstIndex >= 0)
        {
            var slots = partyGrid.GetComponentsInChildren<StorageSlotUI>(true);
            if (firstIndex < slots.Length && slots[firstIndex] != null)
            {
                var ev = new PointerEventData(EventSystem.current);
                slots[firstIndex].OnPointerClick(ev);
            }
        }
    }

    // Render
    private void RefreshAll()
    {
        partyGrid?.Refresh();
        pcGrid?.Refresh();
        UpdateBoxTitle();
        UpdateBoxButtons();
    }

    private void UpdateBoxTitle()
    {
        if (!txtBoxTitle || SM == null || SM.PcStorage == null) return;
        int idx = Mathf.Max(0, SM.PcStorage.ActiveBoxIndex);
        int total = Mathf.Max(1, SM.PcStorage.UnlockedBoxCount);
        txtBoxTitle.text = $"Caja {idx + 1} / {total}";
    }

    private void RefreshStats(PokemonInstance p)
    {
        if (p == null)
        {
            ClearStats();
            return;
        }

        if (txtName) txtName.text = p.DisplayName;
        if (txtLevel) txtLevel.text = p.level.ToString();

        var t1 = p.species ? p.species.primaryType : PokemonType.None;
        var t2 = p.species ? p.species.secondaryType : PokemonType.None;

        if (imgType1)
        {
            var sp1 = assets ? assets.GetTypeSprite(t1) : null;
            imgType1.enabled = sp1 != null; imgType1.sprite = sp1;
        }
        if (imgType2)
        {
            var sp2 = (t2 != PokemonType.None && assets) ? assets.GetTypeSprite(t2) : null;
            imgType2.enabled = sp2 != null; imgType2.sprite = sp2;
        }

        if (imgGender)
        {
            var sg = assets ? assets.GetGenderSprite(p.gender) : null;
            imgGender.enabled = sg != null; imgGender.sprite = sg;
        }

        if (txtAbility) txtAbility.text = ResolveAbilityName(p);
        if (txtHeldItem) txtHeldItem.text = p.HasHeldItem ? SafeItemName(p.HeldItem) : "Ninguno";

        if (radar)
        {
            radar.HP = p.stats.MaxHP;
            radar.Attack = p.stats.Attack;
            radar.Defense = p.stats.Defense;
            radar.SpAttack = p.stats.SpAttack;
            radar.SpDefense = p.stats.SpDefense;
            radar.Speed = p.stats.Speed;
            radar.SetVerticesDirty();
        }
    }

    private void ClearStats()
    {
        if (txtName) txtName.text = "";
        if (txtLevel) txtLevel.text = "";
        if (imgGender) { imgGender.enabled = false; imgGender.sprite = null; }
        if (imgType1) { imgType1.enabled = false; imgType1.sprite = null; }
        if (imgType2) { imgType2.enabled = false; imgType2.sprite = null; }
        if (txtAbility) txtAbility.text = "";
        if (txtHeldItem) txtHeldItem.text = "Ninguno";
    }

    // Learnset
    private void ToggleLearnsetPanel()
    {
        if (!learnsetRoot) return;
        bool show = !learnsetRoot.activeSelf;
        if (show) ShowLearnset(); else HideLearnset();
    }

    private void ShowLearnset()
    {
        if (!learnsetRoot || !learnsetGrid || current == null) return;
        learnsetRoot.SetActive(true);
        RefreshLearnsetList(true);
    }

    private void HideLearnset()
    {
        if (learnsetRoot) learnsetRoot.SetActive(false);
        if (learnsetGrid) learnsetGrid.ClearCustomSource();
    }

    private void RefreshLearnsetList(bool ensureVisible)
    {
        if (!learnsetGrid || current == null) return;

        var list = new List<MoveData>();
        var sp = current.species;
        if (sp != null && sp.learnableAttacks != null)
        {
            foreach (var e in sp.learnableAttacks)
                if (e != null && e.attackData != null && e.level <= current.level)
                    list.Add(e.attackData);
        }
        list = list.Distinct().OrderBy(m => m.moveName).ToList();

        learnsetGrid.SetLearnset(current, list);
        if (ensureVisible && learnsetRoot && !learnsetRoot.activeSelf) learnsetRoot.SetActive(true);
    }

    // Helpers
    private static string SafeItemName(ItemData item)
    {
        if (item == null) return "Ninguno";
        if (!string.IsNullOrWhiteSpace(item.itemName)) return item.itemName;
        return item.name;
    }

    private static string ResolveAbilityName(PokemonInstance p)
    {
        if (p == null || p.species == null) return "";
        if (p.ability != null)
            return string.IsNullOrWhiteSpace(p.ability.abilityName) ? p.ability.name : p.ability.abilityName;

        if (p.species.possibleAbilities != null && p.species.possibleAbilities.Length > 0 && p.species.possibleAbilities[0] != null)
        {
            var fb = p.species.possibleAbilities[0];
            return string.IsNullOrWhiteSpace(fb.abilityName) ? fb.name : fb.abilityName;
        }
        return "";
    }

    private static bool IsPointerOverHierarchy(GameObject root)
    {
        if (root == null || EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        foreach (var r in results)
            if (r.gameObject != null && r.gameObject.transform.IsChildOf(root.transform))
                return true;
        return false;
    }
}

