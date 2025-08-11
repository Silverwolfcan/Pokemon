using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class MoveSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI (opcionales)")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtPP;
    [SerializeField] private TextMeshProUGUI txtType;

    [Header("Apariencia")]
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float dragDimAlpha = 0.5f; // oscurecer slot original durante drag

    private CanvasGroup cg;
    private MoveGridUI grid;
    private MoveInstance move;
    private PokemonInstance owner;

    // Drag state
    private int fromIndex = -1;
    private RectTransform rt;
    private GameObject ghost;
    private Transform dragLayer;

    private float fixedXScreen;
    private float minYScreen, maxYScreen;
    private float halfHeightScreen;
    private Vector2 pointerOffsetScreen;
    private float originalAlpha = 1f;

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
        if (txtType) txtType.text = empty ? "" : move.data.type.ToString();

        if (cg) cg.alpha = (empty && transparentIfEmpty) ? emptyAlpha : 1f;
    }

    private bool IsEmpty => (move == null || move.data == null);

    // ---------------- Drag con “fantasma” ----------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (grid == null || IsEmpty) return;

        fromIndex = transform.GetSiblingIndex();

        // Datos del contenedor en pantalla
        var bounds = grid.GetVerticalBoundsScreen();
        minYScreen = bounds.minY; maxYScreen = bounds.maxY;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        halfHeightScreen = 0.5f * (corners[1].y - corners[0].y);

        fixedXScreen = grid.GetRowCenterXScreen(transform);

        var cam = grid.RootCanvas && grid.RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? grid.RootCanvas.worldCamera : null;
        var itemScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        pointerOffsetScreen = itemScreen - eventData.position;

        // Crear fantasma clonando el slot
        dragLayer = EnsureDragLayer(grid.RootCanvas);
        ghost = Instantiate(gameObject, dragLayer, true);
        ghost.name = $"{name}__Ghost";

        // eliminar este script en el fantasma y bloquear raycasts/interacción
        foreach (var ms in ghost.GetComponentsInChildren<MoveSlotUI>(true)) Destroy(ms);
        foreach (var sel in ghost.GetComponentsInChildren<Selectable>(true)) sel.interactable = false;
        foreach (var g in ghost.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        // alpha del fantasma = 1.0 (sin fade)
        var ghostCg = ghost.GetComponent<CanvasGroup>() ?? ghost.AddComponent<CanvasGroup>();
        ghostCg.blocksRaycasts = false;
        ghostCg.alpha = 1f;

        // Oscurecer SOLO el slot original
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

        // Índice visual de destino: ignorando ESTA fila
        int requested = grid.FindIndexForPointer(eventData.position, transform);
        int clamped = grid.ClampToNonNullZone(requested);

        // Persistir y refrescar (MoveGridUI compacta/guarda)
        grid.NotifyDrop(fromIndex, clamped);

        CleanupGhost();
        fromIndex = -1;
    }

    private void UpdateGhostPosition(PointerEventData ev)
    {
        float y = Mathf.Clamp(ev.position.y + pointerOffsetScreen.y, minYScreen + halfHeightScreen, maxYScreen - halfHeightScreen);
        if (ghost)
        {
            var grt = (RectTransform)ghost.transform;
            grt.position = new Vector3(fixedXScreen, y, 0f);
        }
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
