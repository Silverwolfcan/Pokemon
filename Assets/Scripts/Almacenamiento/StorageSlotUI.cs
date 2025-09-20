// Almacenamiento/StorageSlotUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class StorageSlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private static readonly HashSet<StorageSlotUI> AllSlots = new HashSet<StorageSlotUI>();

    private void OnEnable()
    {
        AllSlots.Add(this);
        // NO limpiar aquí: el panel selecciona el primero al abrir.
    }

    private void OnDisable()
    {
        // Limpiar selección al salir del panel
        InternalSetSelected(false);
        AllSlots.Remove(this);
    }

    public static void ClearGlobalSelectionVisuals()
    {
        foreach (var s in AllSlots) s.InternalSetSelected(false);
    }

    [Header("Roots según modo")]
    [SerializeField] private GameObject partyRoot;
    [SerializeField] private GameObject pcRoot;

    [Header("Assets UI")]
    [SerializeField] private UIAssetsRegistry assets;

    // PARTY
    [Header("Party UI")]
    [SerializeField] private Image partySelectedArrow;
    [SerializeField] private Image partyBackground;
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgSprite;
    [SerializeField] private Image imgExpRadial;
    [SerializeField] private TextMeshProUGUI txtLevel;
    [SerializeField] private Image imgSex;
    [SerializeField] private Slider sliderHealth;
    [SerializeField] private Image sliderFill;
    [SerializeField] private TextMeshProUGUI txtHealth;

    [Header("Colores HP")]
    [SerializeField, Range(0f, 1f)] private float yellowThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float redThreshold = 0.2f;
    [SerializeField] private Color colorGreen = new Color32(0x4C, 0xC2, 0x4C, 255);
    [SerializeField] private Color colorYellow = new Color32(0xFF, 0xC1, 0x2B, 255);
    [SerializeField] private Color colorRed = new Color32(0xE5, 0x3B, 0x3B, 255);

    [Header("Held Item (opcional, solo Bolsa)")]
    [SerializeField] private TextMeshProUGUI txtHeldItemName;
    [SerializeField] private Image imgHeldItemIcon;

    // PC
    [Header("PC UI")]
    [SerializeField] private Image pcBackground;
    [SerializeField] private Image pcPokemon;
    [SerializeField] private Image pcSelectedFrame;

    [Header("Colores PC")]
    [SerializeField] private Color pcOccupiedColor = new Color32(0x08, 0x37, 0x51, 0xFF);
    [SerializeField] private Color pcEmptyColor = new Color32(0xC6, 0xC6, 0xC6, 0xFF);

    [Header("Vacío (solo party)")]
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.25f;
    [SerializeField] private GameObject emptyPlaceholder;
    [SerializeField] private bool fadeEmptySlots = true;
    [SerializeField] private bool autoHideAllTextsWhenEmpty = true;
    [SerializeField] private GameObject[] extraHideWhenEmpty;

    public IPokemonStorage Storage { get; private set; }
    public int Index { get; private set; }

    private PokemonInstance current;
    private StorageGridUI parentGrid;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI[] cachedTexts;

    private bool pcIsSelected;
    private bool partyIsSelected;

    private void Awake()
    {
        parentGrid = GetComponentInParent<StorageGridUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        cachedTexts = GetComponentsInChildren<TextMeshProUGUI>(true);

        InternalSetSelected(false);
        SetHeldItemUI(false, null);
        ResolveSliderFill();
    }

    private void ResolveSliderFill()
    {
        if (!sliderFill && sliderHealth != null && sliderHealth.fillRect)
            sliderFill = sliderHealth.fillRect.GetComponent<Image>();
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

        if (partyRoot) partyRoot.SetActive(!isPcMode);
        if (pcRoot) pcRoot.SetActive(isPcMode);

        current = Storage?.GetAt(Index);
        bool has = current != null;

        ApplyEmptyVisuals(has, isPcMode);

        if (isPcMode) RefreshPc(has);
        else RefreshParty(has);
    }

    private void ApplyEmptyVisuals(bool has, bool isPcMode)
    {
        if (!isPcMode)
        {
            if (emptyPlaceholder) emptyPlaceholder.SetActive(!has);
            if (fadeEmptySlots && canvasGroup) canvasGroup.alpha = has ? 1f : emptyAlpha;

            if (autoHideAllTextsWhenEmpty && cachedTexts != null)
                foreach (var t in cachedTexts) if (t) t.enabled = has;

            if (extraHideWhenEmpty != null)
                foreach (var go in extraHideWhenEmpty) if (go) go.SetActive(has);
        }
        else
        {
            if (canvasGroup) canvasGroup.alpha = 1f;
            if (emptyPlaceholder) emptyPlaceholder.SetActive(false);
            if (cachedTexts != null)
                foreach (var t in cachedTexts) if (t) t.enabled = false;
            if (extraHideWhenEmpty != null)
                foreach (var go in extraHideWhenEmpty) if (go) go.SetActive(false);
        }
    }

    private void RefreshPc(bool has)
    {
        if (pcBackground) pcBackground.color = has ? pcOccupiedColor : pcEmptyColor;

        if (pcPokemon)
        {
            if (has)
            {
                pcPokemon.enabled = true;
                pcPokemon.sprite = current?.species?.pokemonSprite;
                pcPokemon.preserveAspect = true;
            }
            else
            {
                pcPokemon.enabled = false;
                pcPokemon.sprite = null;
            }
        }

        if (pcSelectedFrame) pcSelectedFrame.enabled = has && pcIsSelected;
        SetHeldItemUI(false, null);
    }

    private void RefreshParty(bool has)
    {
        ResolveSliderFill();

        if (partyBackground && assets != null)
        {
            partyBackground.sprite = partyIsSelected ? assets.partyBgSelected : assets.partyBgUnselected;
            partyBackground.enabled = partyBackground.sprite != null;
        }

        if (partySelectedArrow) partySelectedArrow.enabled = partyIsSelected && has;

        if (!has)
        {
            if (txtName) txtName.text = "";
            if (imgSprite) { imgSprite.enabled = false; imgSprite.sprite = null; }
            if (imgExpRadial) { imgExpRadial.fillAmount = 0f; imgExpRadial.enabled = false; }
            if (txtLevel) txtLevel.text = "";
            if (imgSex) { imgSex.enabled = false; imgSex.sprite = null; }
            if (sliderHealth)
            {
                sliderHealth.value = 0;
                sliderHealth.gameObject.SetActive(false);
            }
            if (sliderFill) sliderFill.color = colorGreen;
            if (txtHealth) txtHealth.text = "";
            SetHeldItemUI(false, null);
            return;
        }

        if (txtName) txtName.text = current.DisplayName;
        if (imgSprite)
        {
            imgSprite.enabled = true;
            imgSprite.sprite = current.species?.pokemonSprite;
            imgSprite.preserveAspect = true;
        }

        if (imgExpRadial)
        {
            bool show = current.level < 100;
            imgExpRadial.enabled = show;
            imgExpRadial.fillAmount = show ? current.ExpProgress01() : 0f;
        }

        if (txtLevel) txtLevel.text = current.level.ToString();

        if (imgSex)
        {
            var sp = assets ? assets.GetGenderSprite(current.gender) : null;
            if (sp) { imgSex.enabled = true; imgSex.sprite = sp; }
            else { imgSex.enabled = false; imgSex.sprite = null; }
        }

        UpdateHPUI();

        if (current.HasHeldItem) SetHeldItemUI(true, current.HeldItem);
        else SetHeldItemUI(false, null);
    }

    private void UpdateHPUI()
    {
        if (!sliderHealth) return;

        sliderHealth.maxValue = current.stats.MaxHP;
        sliderHealth.value = current.currentHP;
        sliderHealth.gameObject.SetActive(true);

        if (txtHealth) txtHealth.text = $"{current.currentHP}/{current.stats.MaxHP}";

        if (sliderFill)
        {
            float ratio = current.stats.MaxHP > 0 ? current.currentHP / (float)current.stats.MaxHP : 0f;
            sliderFill.color = (ratio <= redThreshold) ? colorRed
                            : (ratio <= yellowThreshold) ? colorYellow
                            : colorGreen;
        }
    }

    private void SetHeldItemUI(bool show, ItemData item)
    {
        if (txtHeldItemName)
        {
            txtHeldItemName.gameObject.SetActive(show);
            txtHeldItemName.text = show ? SafeItemName(item) : "";
        }
        if (imgHeldItemIcon)
        {
            if (show && item != null && item.icon != null)
            {
                imgHeldItemIcon.sprite = item.icon;
                imgHeldItemIcon.enabled = true;
                imgHeldItemIcon.gameObject.SetActive(true);
                imgHeldItemIcon.preserveAspect = true;
            }
            else
            {
                imgHeldItemIcon.enabled = false;
                imgHeldItemIcon.sprite = null;
                imgHeldItemIcon.gameObject.SetActive(false);
            }
        }
    }

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
            if (partySelectedArrow) partySelectedArrow.enabled = partyIsSelected;
            if (partyBackground && assets != null)
            {
                partyBackground.sprite = partyIsSelected ? assets.partyBgSelected : assets.partyBgUnselected;
                partyBackground.enabled = partyBackground.sprite != null;
            }
        }
    }

    public Sprite GetDisplaySprite()
    {
        if (Storage == null) return null;
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcPokemon != null && pcPokemon.sprite != null ? pcPokemon.sprite : current?.species?.pokemonSprite;
        return imgSprite != null && imgSprite.sprite != null ? imgSprite.sprite : current?.species?.pokemonSprite;
    }

    public RectTransform GetIconRectTransform()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcPokemon ? pcPokemon.rectTransform : null;
        return imgSprite ? imgSprite.rectTransform : null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (current == null) return;
        ClearGlobalSelectionVisuals();
        InternalSetSelected(true);
        parentGrid ??= GetComponentInParent<StorageGridUI>(true);
        parentGrid?.OnSlotClicked(this, current);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (current == null) return;
        ClearGlobalSelectionVisuals();
        InternalSetSelected(true);
        parentGrid ??= GetComponentInParent<StorageGridUI>(true);
        parentGrid?.OnSlotClicked(this, current);
        DragDropController.Instance?.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragDropController.Instance?.DoDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragDropController.Instance?.EndDrag(this, eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        DragDropController.Instance?.HandleDrop(this, eventData);
    }

    private static string SafeItemName(ItemData item)
    {
        if (item == null) return "";
        if (!string.IsNullOrWhiteSpace(item.itemName)) return item.itemName;
        return item.name;
    }
}
