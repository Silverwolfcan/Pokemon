using System.Collections;
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

    private GameObject dragGhost;
    private RectTransform dragGhostRT;
    private Canvas targetCanvas;

    private IPokemonStorage srcStorage;
    private int srcIndex = -1;
    private StorageGridUI srcGridForRefresh;
    private bool srcIsParty;

    private StorageSlotUI draggingFrom;

    // Selección por instancia tras refrescar grids
    private PokemonInstance desiredSelectionAfterDrop;

    public bool IsDraggingAny { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Seguir el ratón
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

        // Fallback: si no llega OnDrop, resolvemos manualmente
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
                    var slot = r.gameObject.GetComponentInParent<StorageSlotUI>();
                    if (slot != null) { foundSlot = slot; break; }
                }

                if (foundSlot != null)
                {
                    HandleDrop(foundSlot, ped);
                    return;
                }
            }
            CleanupAfterOperation(null);
        }
    }

    // -------- API desde StorageSlotUI --------
    public void BeginDrag(StorageSlotUI from, PointerEventData ev)
    {
        draggingFrom = from;

        srcStorage = from?.Storage;
        srcIndex = from ? from.Index : -1;
        srcGridForRefresh = from ? from.GetComponentInParent<StorageGridUI>(true) : null;
        srcIsParty = srcStorage is PokemonParty;

        IsDraggingAny = true;

        if (srcStorage == null || !srcStorage.IsIndexValid(srcIndex) || srcStorage.GetAt(srcIndex) == null)
            return;

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
        img.raycastTarget = false;

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
        // Deja que Update resuelva el drop manual
        DestroyGhost();
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

        // Regla: no dejar party vacía al mover fuera de party a hueco
        if (enforceMinPartyOne && srcIsParty && !ReferenceEquals(srcStorage, dstStorage))
        {
            var party = (PokemonParty)srcStorage;
            int count = 0;
            for (int i = 0; i < party.MaxCapacity; i++) if (party.GetAt(i) != null) count++;
            if (count <= 1 && dstStorage.GetAt(dstIndex) == null)
            {
                CleanupAfterOperation(dstGrid);
                return;
            }
        }

        // MISMO STORAGE
        if (ReferenceEquals(srcStorage, dstStorage))
        {
            var moving = srcStorage.GetAt(srcIndex);               // capturamos instancia
            if (srcIndex != dstIndex)
            {
                var b = srcStorage.GetAt(dstIndex);
                srcStorage.TryInsertAt(dstIndex, moving, out _);
                srcStorage.TryInsertAt(srcIndex, b, out _);
            }

            desiredSelectionAfterDrop = moving;                    // seleccionar por instancia
            RefreshGrids(srcGridForRefresh, dstGrid);
            StartCoroutine(SelectAfterRefresh(desiredSelectionAfterDrop, srcGridForRefresh, dstGrid, dstIndex));
            FinalizeDrop();
            return;
        }

        // STORAGES DIFERENTES
        var movingOut = srcStorage.RemoveAt(srcIndex);
        if (movingOut == null)
        {
            RefreshGrids(srcGridForRefresh, dstGrid);
            StartCoroutine(SelectAfterRefresh(null, srcGridForRefresh, dstGrid, dstIndex));
            FinalizeDrop();
            return;
        }

        if (!dstStorage.TryInsertAt(dstIndex, movingOut, out var displaced))
        {
            // Revertir
            srcStorage.TryInsertAt(srcIndex, movingOut, out _);
            RefreshGrids(srcGridForRefresh, dstGrid);
            StartCoroutine(SelectAfterRefresh(movingOut, srcGridForRefresh, dstGrid, srcIndex));
            FinalizeDrop();
            return;
        }

        if (displaced != null)
            srcStorage.TryInsertAt(srcIndex, displaced, out _);

        desiredSelectionAfterDrop = movingOut;
        RefreshGrids(srcGridForRefresh, dstGrid);
        StartCoroutine(SelectAfterRefresh(desiredSelectionAfterDrop, srcGridForRefresh, dstGrid, dstIndex));
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

        // limpiar origen
        srcStorage = null;
        srcIndex = -1;
        srcGridForRefresh = null;
        srcIsParty = false;
    }

    private IEnumerator SelectAfterRefresh(PokemonInstance instance, StorageGridUI gridA, StorageGridUI gridB, int fallbackIndex)
    {
        yield return null; // esperar a que Refresh cree/actualice slots

        // Busca por instancia en ambos grids
        StorageSlotUI foundSlot = null;
        StorageGridUI foundGrid = null;

        if (instance != null)
        {
            foundSlot = FindSlotWithInstance(gridA, instance) ?? FindSlotWithInstance(gridB, instance);
            if (foundSlot != null) foundGrid = foundSlot.GetComponentInParent<StorageGridUI>(true);
        }

        // Fallback por índice en gridB
        if (foundSlot == null && gridB != null)
        {
            var slots = gridB.GetComponentsInChildren<StorageSlotUI>(true);
            if (fallbackIndex >= 0 && fallbackIndex < slots.Length) foundSlot = slots[fallbackIndex];
            foundGrid = gridB;
        }

        if (foundSlot != null)
        {
            StorageSlotUI.ClearGlobalSelectionVisuals();
            var es = EventSystem.current;
            var ped = es != null ? new PointerEventData(es) : null;
            foundSlot.OnPointerClick(ped);
        }

        desiredSelectionAfterDrop = null;
    }

    private StorageSlotUI FindSlotWithInstance(StorageGridUI grid, PokemonInstance instance)
    {
        if (grid == null || instance == null) return null;
        var slots = grid.GetComponentsInChildren<StorageSlotUI>(true);
        foreach (var s in slots)
        {
            if (s == null || s.Storage == null) continue;
            if (s.Storage.IsIndexValid(s.Index) && ReferenceEquals(s.Storage.GetAt(s.Index), instance))
                return s;
        }
        return null;
    }

    private void CleanupAfterOperation(StorageGridUI dstGrid)
    {
        if (dstGrid) dstGrid.Refresh();
        FinalizeDrop();
    }
}
