using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragDropController : MonoBehaviour
{
    public static DragDropController Instance { get; private set; }

    [Header("Ghost visual")]
    [Range(0f, 1f)] public float ghostAlpha = 1.0f;
    public bool matchSourceSize = true;
    public Vector2 defaultGhostSize = new Vector2(96, 96);

    [Header("Reglas")]
    public bool enforceMinPartyOne = true;
    public int minPartyCount = 1;

    // Estado del drag (se mantiene aunque cambies de página)
    private IPokemonStorage srcStorage;
    private int srcIndex = -1;
    private StorageGridUI srcGridForRefresh;
    private bool srcIsParty = false;

    private StorageSlotUI draggingFrom;

    // Ghost
    private GameObject dragGhost;
    private RectTransform dragGhostRT;
    private Canvas targetCanvas;

    public static bool IsDraggingAny { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Mantener el ghost siguiendo al ratón durante todo el drag
        if (IsDraggingAny && dragGhostRT != null && targetCanvas != null)
        {
            Vector2 pos;
            var cam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)targetCanvas.transform, Input.mousePosition, cam, out pos))
                dragGhostRT.anchoredPosition = pos;
            else
                dragGhostRT.position = Input.mousePosition;
        }

        // MouseUp: si Unity no envió OnDrop (porque el origen fue destruido),
        // hacemos nosotros el raycast y resolvemos el drop manualmente.
        if (IsDraggingAny && Input.GetMouseButtonUp(0))
        {
            var es = EventSystem.current;
            if (es != null)
            {
                var ped = new PointerEventData(es) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                es.RaycastAll(ped, results);

                StorageSlotUI foundSlot = null;
                foreach (var r in results)
                {
                    // Busca el slot más cercano en la jerarquía
                    var slot = r.gameObject.GetComponentInParent<StorageSlotUI>();
                    if (slot != null) { foundSlot = slot; break; }
                }

                if (foundSlot != null)
                {
                    HandleDrop(foundSlot, ped);
                    return; // HandleDrop ya limpia todo
                }
            }

            // Si no hay ningún slot bajo el ratón, cancelamos solo lo visual
            DestroyGhost();
            draggingFrom = null;
            IsDraggingAny = false;
        }
    }

    // -------- API desde StorageSlotUI --------
    public void BeginDrag(StorageSlotUI from, PointerEventData ev)
    {
        draggingFrom = from;

        // Capturamos storage e índice de ORIGEN
        srcStorage = from?.Storage;
        srcIndex = from ? from.Index : -1;
        srcGridForRefresh = from ? from.GetComponentInParent<StorageGridUI>(true) : null;
        srcIsParty = srcStorage is PokemonParty;

        IsDraggingAny = true;

        // Si slot vacío, no generamos ghost (pero mantenemos el flag para auto-paginado)
        if (srcStorage == null || !srcStorage.IsIndexValid(srcIndex) || srcStorage.GetAt(srcIndex) == null)
            return;

        // Canvas raíz para el ghost
        targetCanvas = from.GetComponentInParent<Canvas>()?.rootCanvas;
        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas == null) return;

        var sprite = from.GetDisplaySprite();
        if (sprite == null) return;

        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragGhost.transform.SetParent(targetCanvas.transform, false);
        dragGhostRT = (RectTransform)dragGhost.transform;

        var img = dragGhost.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false; // el ghost nunca bloquea raycasts

        var cg = dragGhost.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = ghostAlpha;

        Vector2 size = defaultGhostSize;
        var srcRT = from.GetIconRectTransform();
        if (matchSourceSize && srcRT != null) size = srcRT.rect.size;
        dragGhostRT.sizeDelta = size;
        dragGhostRT.pivot = new Vector2(0.5f, 0.5f);
        dragGhostRT.anchorMin = dragGhostRT.anchorMax = new Vector2(0.5f, 0.5f);

        SetGhostPosition(ev);
        dragGhost.transform.SetAsLastSibling();
    }

    public void DoDrag(StorageSlotUI from, PointerEventData ev)
    {
        if (dragGhostRT == null) return;
        SetGhostPosition(ev);
    }

    public void EndDrag(StorageSlotUI from, PointerEventData ev)
    {
        // NO apagamos IsDraggingAny: dejemos que Update resuelva el drop manual
        DestroyGhost();
        draggingFrom = null;
    }

    public void HandleDrop(StorageSlotUI target, PointerEventData ev)
    {
        var dstStorage = target?.Storage;
        int dstIndex = target ? target.Index : -1;
        var dstGrid = target ? target.GetComponentInParent<StorageGridUI>(true) : null;

        if (srcStorage == null || dstStorage == null || !srcStorage.IsIndexValid(srcIndex) || !dstStorage.IsIndexValid(dstIndex))
        {
            CleanupAfterOperation(dstGrid);
            return;
        }

        // Regla: no dejar la party por debajo del mínimo si mueves fuera de la party a un hueco
        if (enforceMinPartyOne && srcIsParty && !ReferenceEquals(srcStorage, dstStorage))
        {
            var party = (PokemonParty)srcStorage;
            int nonNull = party.CountNonNull;
            bool destOccupied = dstStorage.GetAt(dstIndex) != null;
            if (nonNull <= minPartyCount && !destOccupied)
            {
                CleanupAfterOperation(dstGrid);
                return;
            }
        }

        // MISMO STORAGE → swap
        if (ReferenceEquals(srcStorage, dstStorage))
        {
            if (srcIndex != dstIndex)
            {
                if (srcStorage is PokemonParty party) party.Swap(srcIndex, dstIndex);
                else if (srcStorage is PCBox box) box.Swap(srcIndex, dstIndex);
                else
                {
                    var a = srcStorage.GetAt(srcIndex);
                    var b = srcStorage.GetAt(dstIndex);
                    srcStorage.TryInsertAt(dstIndex, a, out _);
                    srcStorage.TryInsertAt(srcIndex, b, out _);
                }
            }

            RefreshGrids(srcGridForRefresh, dstGrid);
            FinalizeDrop();
            return;
        }

        // STORAGES DISTINTOS → mover + posible intercambio
        var moving = srcStorage.RemoveAt(srcIndex);
        if (moving == null)
        {
            RefreshGrids(srcGridForRefresh, dstGrid);
            FinalizeDrop();
            return;
        }

        if (!dstStorage.TryInsertAt(dstIndex, moving, out var displaced))
        {
            // Revertir si no cupo
            srcStorage.TryInsertAt(srcIndex, moving, out _);
            RefreshGrids(srcGridForRefresh, dstGrid);
            FinalizeDrop();
            return;
        }

        // Si había alguien en destino, vuelve al origen
        if (displaced != null)
            srcStorage.TryInsertAt(srcIndex, displaced, out _);

        RefreshGrids(srcGridForRefresh, dstGrid);
        FinalizeDrop();
    }

    // -------- Helpers --------
    private void SetGhostPosition(PointerEventData ev)
    {
        if (dragGhostRT == null || targetCanvas == null) return;

        Vector2 pos;
        var cam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)targetCanvas.transform, ev.position, cam, out pos))
            dragGhostRT.anchoredPosition = pos;
        else
            dragGhostRT.position = ev.position;
    }

    private void DestroyGhost()
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null; dragGhostRT = null; targetCanvas = null;
    }

    private void RefreshGrids(StorageGridUI srcGrid, StorageGridUI dstGrid)
    {
        if (srcGrid) srcGrid.Refresh();
        if (dstGrid && dstGrid != srcGrid) dstGrid.Refresh();
    }

    private void FinalizeDrop()
    {
        DestroyGhost();
        draggingFrom = null;
        IsDraggingAny = false;

        var selector = Object.FindAnyObjectByType<ItemSelectorUI>();
        if (selector != null) selector.RefreshCapturedPokemon();

        srcStorage = null;
        srcIndex = -1;
        srcGridForRefresh = null;
        srcIsParty = false;
    }

    private void CleanupAfterOperation(StorageGridUI dstGrid)
    {
        if (dstGrid) dstGrid.Refresh();
        FinalizeDrop();
    }
}
