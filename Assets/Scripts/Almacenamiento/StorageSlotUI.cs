using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class StorageSlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    // ===== Registro global =====
    private static readonly HashSet<StorageSlotUI> AllSlots = new HashSet<StorageSlotUI>();
    private void OnEnable() { AllSlots.Add(this); }
    private void OnDisable() { AllSlots.Remove(this); }
    public static void ClearGlobalSelectionVisuals() { foreach (var s in AllSlots) s.InternalSetSelected(false); }

    [Header("Roots (opcionales)")]
    [SerializeField] private GameObject partyRoot; // si no se asigna, usa este GameObject
    [SerializeField] private GameObject pcRoot;    // grupo visual del PC

    [Header("Refs")]
    [SerializeField] private UIAssetsRegistry assets;
    [SerializeField] private StorageGridUI parentGrid;

    // ===== Party =====
    [Header("Party UI")]
    [SerializeField] private Image partySelectedArrow;
    [SerializeField] private Image partyBackground; // <== Img_slotbackground
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgSprite;
    [SerializeField] private Image imgExpRadial;
    [SerializeField] private TextMeshProUGUI txtLevel;
    [SerializeField] private Image imgSex;
    [SerializeField] private Slider sliderHealth;
    [SerializeField] private TextMeshProUGUI txtHealth;

    // ===== PC =====
    [Header("PC UI (3 elementos)")]
    [SerializeField] private Image pcBackground;
    [SerializeField] private Image pcPokemon;
    [SerializeField] private Image pcSelectedFrame;

    [Header("Colores PC")]
    [SerializeField] private Color pcOccupiedColor = new Color32(0x08, 0x37, 0x51, 0xFF);
    [SerializeField] private Color pcEmptyColor = new Color32(0xC6, 0xC6, 0xC6, 0xFF);

    [Header("Vacío (Party)")]
    [SerializeField, Range(0f, 1f)] private float emptyBgAlpha = 0.35f;

    public IPokemonStorage Storage { get; private set; }
    public int Index { get; private set; }

    private PokemonInstance current;
    private bool partyIsSelected;
    private bool pcIsSelected;

    // Lista de nodos a ocultar cuando el slot está vacío (Party)
    private readonly List<Transform> partyNodesToToggle = new List<Transform>();
    private Transform partyKeep; // background y su cadena de ancestros

    private void Reset()
    {
        assets = assets ? assets : UnityEngine.Object.FindFirstObjectByType<UIAssetsRegistry>();
        parentGrid = parentGrid ? parentGrid : GetComponentInParent<StorageGridUI>(true);
    }

    private void Awake()
    {
        if (!partyRoot) partyRoot = gameObject;
        BuildPartyToggleList();
    }

    private void Start()
    {
        assets = assets ? assets : UnityEngine.Object.FindFirstObjectByType<UIAssetsRegistry>();
        parentGrid = parentGrid ? parentGrid : GetComponentInParent<StorageGridUI>(true);

        if (pcSelectedFrame) pcSelectedFrame.enabled = false;
        if (partySelectedArrow) partySelectedArrow.enabled = false;

        if (partyBackground && assets != null)
        {
            partyBackground.sprite = assets.partyBgUnselected;
            partyBackground.enabled = partyBackground.sprite != null;
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!partyRoot) partyRoot = gameObject;
        BuildPartyToggleList();
    }

    private void BuildPartyToggleList()
    {
        partyNodesToToggle.Clear();

        // Qué conservar: el background y TODOS sus ancestros dentro de partyRoot
        partyKeep = partyBackground ? partyBackground.transform : null;

        bool IsInKeepChain(Transform t)
        {
            if (partyKeep == null) return false;
            var cur = partyKeep;
            while (cur != null)
            {
                if (cur == t) return true;
                cur = cur.parent;
            }
            return false;
        }

        void AddDescendants(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (pcRoot && (c == pcRoot || c.IsChildOf(pcRoot.transform))) continue;

                if (!IsInKeepChain(c))
                    partyNodesToToggle.Add(c);

                AddDescendants(c);
            }
        }

        if (partyRoot) AddDescendants(partyRoot.transform);
    }

    public void SetContext(IPokemonStorage storage, int index)
    {
        Storage = storage; Index = index;
        if (parentGrid == null) parentGrid = GetComponentInParent<StorageGridUI>(true);
        Refresh();
    }

    public void Refresh()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;

        if (partyRoot && partyRoot != gameObject) partyRoot.SetActive(!isPcMode);
        if (pcRoot && pcRoot != gameObject) pcRoot.SetActive(isPcMode);

        current = Storage?.GetAt(Index);
        bool has = current != null;

        if (isPcMode) RefreshPC(has);
        else RefreshParty(has);
    }

    // ---------- PC ----------
    private void RefreshPC(bool has)
    {
        if (pcBackground)
        {
            pcBackground.color = has ? pcOccupiedColor : pcEmptyColor;
            pcBackground.enabled = true;
        }

        if (pcPokemon)
        {
            pcPokemon.enabled = has;
            pcPokemon.sprite = has ? (current.species?.pokemonSprite) : null;
            pcPokemon.preserveAspect = true;
        }

        if (pcSelectedFrame) pcSelectedFrame.enabled = has && pcIsSelected;
    }

    // ---------- PARTY ----------
    private void RefreshParty(bool has)
    {
        if (partyBackground && assets != null)
        {
            partyBackground.sprite = partyIsSelected ? assets.partyBgSelected : assets.partyBgUnselected;
            var c = Color.white; c.a = has ? 1f : emptyBgAlpha;
            partyBackground.color = c;
            partyBackground.enabled = partyBackground.sprite != null;
        }

        for (int i = 0; i < partyNodesToToggle.Count; i++)
            if (partyNodesToToggle[i] && partyNodesToToggle[i].gameObject.activeSelf != has)
                partyNodesToToggle[i].gameObject.SetActive(has);

        if (!has) { if (partySelectedArrow) partySelectedArrow.enabled = false; return; }

        if (partySelectedArrow) partySelectedArrow.enabled = partyIsSelected;

        if (txtName) txtName.text = current.DisplayName;

        if (imgSprite)
        {
            imgSprite.sprite = current.species?.pokemonSprite;
            imgSprite.preserveAspect = true;
        }

        if (imgExpRadial) imgExpRadial.fillAmount = Mathf.Clamp01(PokemonExpAdapter.GetFraction(current));
        if (txtLevel) txtLevel.text = current.level.ToString();

        if (imgSex)
        {
            var sp = assets ? assets.GetGenderSprite(current.gender) : null;
            imgSex.enabled = sp != null;
            imgSex.sprite = sp;
        }

        if (sliderHealth)
        {
            sliderHealth.maxValue = current.stats.MaxHP;
            sliderHealth.value = current.currentHP;
        }

        if (txtHealth) txtHealth.text = $"{current.currentHP}/{current.stats.MaxHP}";
    }

    // ---------- Selección ----------
    private void InternalSetSelected(bool selected)
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;

        if (isPcMode)
        {
            pcIsSelected = selected && (current != null);
            if (pcSelectedFrame) pcSelectedFrame.enabled = pcIsSelected;
        }
        else
        {
            partyIsSelected = selected && (current != null);
            if (partySelectedArrow) partySelectedArrow.enabled = partyIsSelected && current != null;
            if (partyBackground && assets != null)
                partyBackground.sprite = partyIsSelected ? assets.partyBgSelected : assets.partyBgUnselected;
        }
    }

    public void ForceSetSelectedVisual(bool selected) => InternalSetSelected(selected);

    // ---------- Helpers ----------
    public Sprite GetDisplaySprite()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode)
            return pcPokemon && pcPokemon.sprite ? pcPokemon.sprite : current?.species?.pokemonSprite;
        return imgSprite && imgSprite.sprite ? imgSprite.sprite : current?.species?.pokemonSprite;
    }

    public RectTransform GetIconRectTransform()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcPokemon ? pcPokemon.rectTransform : null;
        return imgSprite ? imgSprite.rectTransform : null;
    }

    // ---------- Input ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (current == null) return;
        ClearGlobalSelectionVisuals();
        InternalSetSelected(true);
        parentGrid ??= GetComponentInParent<StorageGridUI>(true);
        parentGrid?.OnSlotClicked(this, current);
    }
    public void OnBeginDrag(PointerEventData eventData) { if (current == null) return; DragDropController.Instance?.BeginDrag(this, eventData); }
    public void OnDrag(PointerEventData eventData) { DragDropController.Instance?.DoDrag(this, eventData); }
    public void OnEndDrag(PointerEventData eventData) { DragDropController.Instance?.EndDrag(this, eventData); }
    public void OnDrop(PointerEventData eventData) { DragDropController.Instance?.HandleDrop(this, eventData); }
}
