// UI/MoveSlotUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class MoveSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image imgMove;       // Icono opcional
    [SerializeField] private Image imgMoveType;   // Tipo elemental
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtPP;

    [Header("Assets")]
    [SerializeField] private UIAssetsRegistry assets;

    [Header("Apariencia")]
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float dragDimAlpha = 0.5f;

    private CanvasGroup cg;
    private MoveGridUI grid;
    private MoveInstance move;
    private PokemonInstance owner;

    private int fromIndex = -1;
    private RectTransform rt;
    private GameObject ghost;
    private Transform dragLayer;
    private float originalAlpha = 1f;

    private int visualIndex = -1;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    public void AttachGrid(MoveGridUI g) => grid = g;
    public void DetachGrid() => grid = null;
    public void SetVisualIndex(int vis) => visualIndex = vis;

    public void Setup(MoveInstance move, PokemonInstance owner, bool transparentIfEmpty)
    {
        this.move = move;
        this.owner = owner;
        Refresh(transparentIfEmpty);
    }

    public void Refresh(bool transparentIfEmpty)
    {
        bool empty = (move == null || move.data == null);

        if (txtName) txtName.text = empty ? "" : move.data.moveName;
        if (txtPP) txtPP.text = empty ? "" : $"{move.currentPP}/{move.maxPP}";

        if (imgMoveType)
        {
            if (empty)
            {
                imgMoveType.enabled = false;
                imgMoveType.sprite = null;
            }
            else
            {
                // Usa sprites por ElementType (movimientos)
                var spType = assets ? assets.GetTypeSprite(move.data.type) : null;
                imgMoveType.enabled = spType != null;
                imgMoveType.sprite = spType;
                imgMoveType.preserveAspect = true;
            }
        }

        if (imgMove)
        {
            if (empty) imgMove.enabled = false;
            else
            {
                imgMove.enabled = imgMove.sprite != null;
                imgMove.preserveAspect = true;
            }
        }

        if (cg) cg.alpha = (empty && transparentIfEmpty) ? emptyAlpha : 1f;
    }

    private bool IsEmpty => (move == null || move.data == null);

    // ---------- CLICK para modo combate ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (grid == null || grid.Mode != MoveGridUI.GridMode.Combat) return;
        if (IsEmpty || move.currentPP <= 0) return;

        grid.NotifyClick(visualIndex);
    }

    // ---------- Drag (edición y aprendibles) ----------
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (grid == null || IsEmpty) return;
        if (grid.Mode != MoveGridUI.GridMode.Edit && grid.Mode != MoveGridUI.GridMode.Learnset) return;

        fromIndex = transform.GetSiblingIndex();

        dragLayer = EnsureDragLayer(grid.RootCanvas);
        ghost = Instantiate(gameObject, dragLayer, true);
        ghost.name = $"{name}__Ghost";

        foreach (var ms in ghost.GetComponentsInChildren<MoveSlotUI>(true)) Destroy(ms);
        foreach (var sel in ghost.GetComponentsInChildren<Selectable>(true)) sel.interactable = false;
        foreach (var g in ghost.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        var ghostCg = ghost.GetComponent<CanvasGroup>() ?? ghost.AddComponent<CanvasGroup>();
        ghostCg.blocksRaycasts = false;
        ghostCg.alpha = 1f;

        originalAlpha = cg.alpha;
        cg.alpha = dragDimAlpha;

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!ghost || grid == null) return;
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (grid == null) { CleanupGhost(); return; }

        var target = MoveGridUI.FindGridAtScreenPoint(eventData.position, grid.RootCanvas, MoveGridUI.GridMode.Edit);

        if (target != null)
        {
            int dstVis = target.FindIndexForPointer(eventData.position, null);

            if (grid == target && grid.Mode == MoveGridUI.GridMode.Edit)
            {
                int clamped = grid.ClampToNonNullZone(dstVis);
                grid.NotifyDropWithinGrid(fromIndex, clamped);
            }
            else
            {
                if (grid.Mode == MoveGridUI.GridMode.Learnset && move != null && move.data != null)
                {
                    target.ApplyIncomingMoveFromLearnset(move.data, dstVis);
                }
            }
        }

        CleanupGhost();
        fromIndex = -1;
    }

    private void UpdateGhostPosition(PointerEventData ev)
    {
        if (!ghost) return;
        ((RectTransform)ghost.transform).position = ev.position;
    }

    private void CleanupGhost()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        if (cg) cg.alpha = originalAlpha;
    }

    private static Transform EnsureDragLayer(Canvas rootCanvas)
    {
        if (!rootCanvas) return null;
        var t = rootCanvas.transform.Find("__DragLayer");
        if (!t)
        {
            var go = new GameObject("__DragLayer", typeof(RectTransform));
            t = go.transform;
            t.SetParent(rootCanvas.transform, false);
            var r = (RectTransform)t;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
        t.SetAsLastSibling();
        return t;
    }
}

