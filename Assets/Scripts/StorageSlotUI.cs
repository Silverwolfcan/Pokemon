using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class StorageSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Roots opcionales (se activan según el modo)")]
    [SerializeField] private GameObject partyRoot;
    [SerializeField] private GameObject pcRoot;

    [Header("Party UI (opcional)")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgSprite;
    [SerializeField] private Slider sliderHealth;
    [SerializeField] private TextMeshProUGUI txtHealth;
    [SerializeField] private TextMeshProUGUI txtLevel;
    [SerializeField] private Image imgSex;
    [SerializeField] private Sprite maleSprite;
    [SerializeField] private Sprite femaleSprite;
    [SerializeField] private Sprite unknownSprite;

    [Header("PC UI (opcional)")]
    [SerializeField] private Image pcImgSprite;
    [SerializeField] private TextMeshProUGUI pcTxtLevel;

    [Header("Visual de vacío")]
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

    private void Awake()
    {
        parentGrid = GetComponentInParent<StorageGridUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        cachedTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
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

        ApplyEmptyVisuals(has);

        if (isPcMode) RefreshPc(has);
        else RefreshParty(has);
    }

    private void ApplyEmptyVisuals(bool hasContent)
    {
        if (emptyPlaceholder) emptyPlaceholder.SetActive(!hasContent);
        if (fadeEmptySlots && canvasGroup) canvasGroup.alpha = hasContent ? 1f : emptyAlpha;

        if (autoHideAllTextsWhenEmpty && cachedTexts != null)
            foreach (var t in cachedTexts) if (t) t.enabled = hasContent;

        if (extraHideWhenEmpty != null)
            foreach (var go in extraHideWhenEmpty) if (go) go.SetActive(hasContent);
    }

    private void RefreshPc(bool has)
    {
        if (!has)
        {
            if (pcImgSprite) { pcImgSprite.enabled = false; pcImgSprite.sprite = null; }
            if (pcTxtLevel) pcTxtLevel.text = "";
            return;
        }
        if (pcImgSprite) { pcImgSprite.enabled = true; pcImgSprite.sprite = current.species?.pokemonSprite; }
        if (pcTxtLevel) pcTxtLevel.text = $"Lv {current.level}";
    }

    private void RefreshParty(bool has)
    {
        if (!has)
        {
            if (txtName) txtName.text = "";
            if (imgSprite) { imgSprite.enabled = false; imgSprite.sprite = null; }
            if (sliderHealth) { sliderHealth.value = 0; sliderHealth.gameObject.SetActive(false); }
            if (txtHealth) txtHealth.text = "";
            if (txtLevel) txtLevel.text = "";
            if (imgSex) { imgSex.enabled = false; imgSex.sprite = null; }
            return;
        }

        if (txtName) txtName.text = current.species?.pokemonName ?? "";
        if (imgSprite) { imgSprite.enabled = true; imgSprite.sprite = current.species?.pokemonSprite; }

        if (sliderHealth)
        {
            sliderHealth.maxValue = current.stats.MaxHP;
            sliderHealth.value = current.currentHP;
            sliderHealth.gameObject.SetActive(true);
        }
        if (txtHealth) txtHealth.text = $"{current.currentHP}/{current.stats.MaxHP}";
        if (txtLevel) txtLevel.text = $"Lv {current.level}";

        if (imgSex)
        {
            switch (current.gender)
            {
                case Gender.Male:
                    if (maleSprite) { imgSex.enabled = true; imgSex.sprite = maleSprite; }
                    else { imgSex.enabled = false; imgSex.sprite = null; }
                    break;
                case Gender.Female:
                    if (femaleSprite) { imgSex.enabled = true; imgSex.sprite = femaleSprite; }
                    else { imgSex.enabled = false; imgSex.sprite = null; }
                    break;
                default:
                    if (unknownSprite) { imgSex.enabled = true; imgSex.sprite = unknownSprite; }
                    else { imgSex.enabled = false; imgSex.sprite = null; }
                    break;
            }
        }
    }

    // ---------- Helpers para el “fantasma” ----------
    public Sprite GetDisplaySprite()
    {
        if (Storage == null) return null;
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcImgSprite != null && pcImgSprite.sprite != null
                     ? pcImgSprite.sprite
                     : current?.species?.pokemonSprite;
        return imgSprite != null && imgSprite.sprite != null
             ? imgSprite.sprite
             : current?.species?.pokemonSprite;
    }
    public RectTransform GetIconRectTransform()
    {
        bool isPcMode = parentGrid != null && parentGrid.mode == StorageGridUI.GridMode.PCBox;
        if (isPcMode) return pcImgSprite ? pcImgSprite.rectTransform : null;
        return imgSprite ? imgSprite.rectTransform : null;
    }

    // Interacciones
    public void OnPointerClick(PointerEventData eventData)
    {
        if (current == null) return;
        parentGrid ??= GetComponentInParent<StorageGridUI>();
        parentGrid?.OnSlotClicked(this, current);
    }

    public void OnBeginDrag(PointerEventData eventData) => DragDropController.Instance?.BeginDrag(this, eventData);
    public void OnDrag(PointerEventData eventData) => DragDropController.Instance?.DoDrag(this, eventData);
    public void OnEndDrag(PointerEventData eventData) => DragDropController.Instance?.EndDrag(this, eventData);
    public void OnDrop(PointerEventData eventData) => DragDropController.Instance?.HandleDrop(this, eventData);
}
