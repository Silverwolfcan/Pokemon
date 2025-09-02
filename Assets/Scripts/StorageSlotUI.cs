using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class StorageSlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    // ====== Registro global para selección única ======
    private static readonly HashSet<StorageSlotUI> AllSlots = new HashSet<StorageSlotUI>();

    private void OnEnable() { AllSlots.Add(this); }
    private void OnDisable() { AllSlots.Remove(this); }

    public static void ClearGlobalSelectionVisuals()
    {
        foreach (var s in AllSlots) s.InternalSetSelected(false);
    }

    // ====== Config común ======
    [Header("Roots opcionales (se activan según el modo)")]
    [SerializeField] private GameObject partyRoot;
    [SerializeField] private GameObject pcRoot;

    [Header("Assets UI (comunes)")]
    [SerializeField] private UIAssetsRegistry assets; // Sexo + fondos party

    // ====== PARTY ======
    [Header("Party UI")]
    [SerializeField] private Image partySelectedArrow;   // Flecha izquierda (ON si seleccionado)
    [SerializeField] private Image partyBackground;      // Fondo seleccionado/no seleccionado (de UIAssetsRegistry)
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgSprite;
    [SerializeField] private Image imgExpRadial;         // Filled Radial 360 (0..1 hacia próximo nivel)
    [SerializeField] private TextMeshProUGUI txtLevel;   // SOLO número
    [SerializeField] private Image imgSex;               // Sprite viene de UIAssetsRegistry
    [SerializeField] private Slider sliderHealth;
    [SerializeField] private TextMeshProUGUI txtHealth;

    // ====== PC (3 imágenes) ======
    [Header("PC UI (solo estos 3)")]
    [SerializeField] private Image pcBackground;     // Img_PokemonBackground
    [SerializeField] private Image pcPokemon;        // Img_Pokemon
    [SerializeField] private Image pcSelectedFrame;  // Img_PokemonSelected

    [Header("Colores PC")]
    [SerializeField] private Color pcOccupiedColor = new Color32(0x08, 0x37, 0x51, 0xFF); // #083751
    [SerializeField] private Color pcEmptyColor = new Color32(0xC6, 0xC6, 0xC6, 0xFF); // #C6C6C6

    // ====== Visual vacío (no afecta a PC) ======
    [Header("Visual de vacío (opcional, no afecta a PC)")]
    [SerializeField] private GameObject emptyPlaceholder;
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.25f;
    [SerializeField] private bool fadeEmptySlots = true;
    [SerializeField] private bool autoHideAllTextsWhenEmpty = true;
    [SerializeField] private GameObject[] extraHideWhenEmpty;

    public IPokemonStorage Storage { get; private set; }
    public int Index { get; private set; }

    private PokemonInstance current;
    private StorageGridUI parentGrid;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI[] cachedTexts;

    // Estados de selección visual
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

        // Asegurar visual "no seleccionado" al arrancar
        if (pcSelectedFrame) pcSelectedFrame.enabled = false;
        if (partySelectedArrow) partySelectedArrow.enabled = false;
        if (partyBackground && assets != null)
        {
            partyBackground.sprite = assets.partyBgUnselected;
            partyBackground.enabled = partyBackground.sprite != null;
        }
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

    private void ApplyEmptyVisuals(bool hasContent, bool isPcMode)
    {
        if (!isPcMode)
        {
            if (emptyPlaceholder) emptyPlaceholder.SetActive(!hasContent);
            if (fadeEmptySlots && canvasGroup) canvasGroup.alpha = hasContent ? 1f : emptyAlpha;

            if (autoHideAllTextsWhenEmpty && cachedTexts != null)
                foreach (var t in cachedTexts) if (t) t.enabled = hasContent;

            if (extraHideWhenEmpty != null)
                foreach (var go in extraHideWhenEmpty) if (go) go.SetActive(hasContent);
        }
        else
        {
            if (canvasGroup) canvasGroup.alpha = 1f; // PC sin fade
            if (emptyPlaceholder) emptyPlaceholder.SetActive(false);
            if (cachedTexts != null)
                foreach (var t in cachedTexts) if (t) t.enabled = false;
            if (extraHideWhenEmpty != null)
                foreach (var go in extraHideWhenEmpty) if (go) go.SetActive(false);
        }
    }

    // ====== PC ======
    private void RefreshPc(bool has)
    {
        if (pcBackground) pcBackground.color = has ? pcOccupiedColor : pcEmptyColor;

        if (pcPokemon)
        {
            if (has)
            {
                pcPokemon.enabled = true;
                pcPokemon.sprite = current.species?.pokemonSprite;
                pcPokemon.preserveAspect = true;
            }
            else
            {
                pcPokemon.enabled = false;
                pcPokemon.sprite = null;
            }
        }

        if (pcSelectedFrame) pcSelectedFrame.enabled = has && pcIsSelected;
    }

    // ====== PARTY ======
    private void RefreshParty(bool has)
    {
        // Fondo según selección
        if (partyBackground && assets != null)
        {
            partyBackground.sprite = partyIsSelected ? assets.partyBgSelected : assets.partyBgUnselected;
            partyBackground.enabled = partyBackground.sprite != null;
            partyBackground.preserveAspect = false;
        }

        // Flecha selección
        if (partySelectedArrow) partySelectedArrow.enabled = partyIsSelected && has;

        if (!has)
        {
            if (txtName) txtName.text = "";
            if (imgSprite) { imgSprite.enabled = false; imgSprite.sprite = null; }
            if (imgExpRadial) { imgExpRadial.fillAmount = 0f; imgExpRadial.enabled = false; }
            if (txtLevel) txtLevel.text = "";
            if (imgSex) { imgSex.enabled = false; imgSex.sprite = null; }
            if (sliderHealth) { sliderHealth.value = 0; sliderHealth.gameObject.SetActive(false); }
            if (txtHealth) txtHealth.text = "";
            return;
        }

        if (txtName) txtName.text = current.species?.pokemonName ?? "";

        if (imgSprite)
        {
            imgSprite.enabled = true;
            imgSprite.sprite = current.species?.pokemonSprite;
            imgSprite.preserveAspect = true;
        }

        if (imgExpRadial)
        {
            imgExpRadial.enabled = true;
            imgExpRadial.fillAmount = GetExpProgress01(current); // TODO EXP real
        }

        if (txtLevel) txtLevel.text = current.level.ToString();

        if (imgSex)
        {
            var sp = assets ? assets.GetGenderSprite(current.gender) : null;
            if (sp) { imgSex.enabled = true; imgSex.sprite = sp; }
            else { imgSex.enabled = false; imgSex.sprite = null; }
        }

        if (sliderHealth)
        {
            sliderHealth.maxValue = current.stats.MaxHP;
            sliderHealth.value = current.currentHP;
            sliderHealth.gameObject.SetActive(true);
        }
        if (txtHealth) txtHealth.text = $"{current.currentHP}/{current.stats.MaxHP}";
    }

    // ====== Selección interna (aplica al modo actual) ======
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

    // ====== Helpers ======
    private float GetExpProgress01(PokemonInstance p) => 0f; // conectar EXP real cuando lo tengas

    public Sprite GetDisplaySprite()
    {
        if (Storage == null) return null;
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcPokemon != null && pcPokemon.sprite != null
                     ? pcPokemon.sprite
                     : current?.species?.pokemonSprite;
        return imgSprite != null && imgSprite.sprite != null
             ? imgSprite.sprite
             : current?.species?.pokemonSprite;
    }

    public RectTransform GetIconRectTransform()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcPokemon ? pcPokemon.rectTransform : null;
        return imgSprite ? imgSprite.rectTransform : null;
    }

    // ====== Interacciones ======
    public void OnPointerClick(PointerEventData eventData)
    {
        if (current == null) return; // vacíos no son seleccionables

        ClearGlobalSelectionVisuals();
        InternalSetSelected(true);

        // Notifica al grid (stats/moves)
        parentGrid ??= GetComponentInParent<StorageGridUI>();
        parentGrid?.OnSlotClicked(this, current);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (current == null) return; // no drag en vacíos

        // Seleccionar el origen antes de arrastrar
        ClearGlobalSelectionVisuals();
        InternalSetSelected(true);

        // Notificar como click para pintar stats/moves
        parentGrid ??= GetComponentInParent<StorageGridUI>();
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

        // Tras el drop, selecciona el destino (con posible swap)
        StartCoroutine(SelectDestinationNextFrame());
    }

    private IEnumerator SelectDestinationNextFrame()
    {
        yield return null; // espera refrescos de storage
        current = Storage?.GetAt(Index);

        ClearGlobalSelectionVisuals();
        InternalSetSelected(current != null);

        if (current != null)
        {
            parentGrid ??= GetComponentInParent<StorageGridUI>();
            parentGrid?.OnSlotClicked(this, current);
        }
    }
}
