using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Control de drag & drop entre Party y PC usando solo la API de IPokemonStorage.
/// Mantiene compatibilidad con StorageSlotUI (BeginDrag/DoDrag/EndDrag/HandleDrop).
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

    public bool IsDraggingAny { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
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

    // ===== API llamada por StorageSlotUI =====
    public void BeginDrag(StorageSlotUI from, PointerEventData ev)
    {
        draggingFrom = from;

        srcStorage = from?.Storage;
        srcIndex = from ? from.Index : -1;
        srcGridForRefresh = from ? from.GetComponentInParent<StorageGridUI>(true) : null;
        srcIsParty = srcStorage is PokemonParty;

        IsDraggingAny = true;
        var dragSvc = ServiceLocator.Get<DraggingStateService>();
        if (dragSvc != null) dragSvc.Set(true);

        if (srcStorage == null || !srcStorage.IsIndexValid(srcIndex) || srcStorage.GetAt(srcIndex) == null)
            return;

        targetCanvas = from.GetComponentInParent<Canvas>()?.rootCanvas;
        if (targetCanvas == null) targetCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
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

    // Alias de compatibilidad
    public void DoDrag(StorageSlotUI from, PointerEventData ev) => UpdateDrag(ev);

    public void UpdateDrag(PointerEventData ev)
    {
        if (dragGhostRT == null) return;
        SetGhostPosition(ev);
    }

    // Alias de compatibilidad
    public void EndDrag(StorageSlotUI from, PointerEventData ev) => DestroyGhost();

    public void HandleDrop(StorageSlotUI target, PointerEventData ev)
    {
        var dstStorage = target?.Storage;
        int dstIndex = target ? target.Index : -1;
        var dstGrid = target ? target.GetComponentInParent<StorageGridUI>(true) : null;

        if (srcStorage == null || dstStorage == null || srcIndex < 0 || dstIndex < 0)
        {
            CleanupAfterOperation(dstGrid);
            return;
        }

        // No dejar la party vacía si así se configura
        if (enforceMinPartyOne && srcIsParty && srcStorage is PokemonParty partyCheck)
        {
            int count = partyCheck.CountNonNull;
            if (count <= 1 && dstStorage != srcStorage)
            {
                CleanupAfterOperation(dstGrid);
                return;
            }
        }

        // Recordar el Pokémon movido para reasignar selección
        PokemonInstance moved = srcStorage.GetAt(srcIndex);

        if (srcStorage == dstStorage)
        {
            // Swap dentro del mismo storage usando RemoveAt + TryInsertAt
            if (!srcStorage.IsIndexValid(srcIndex) || !srcStorage.IsIndexValid(dstIndex))
            {
                CleanupAfterOperation(dstGrid);
                return;
            }

            var a = srcStorage.GetAt(srcIndex);
            var b = dstStorage.GetAt(dstIndex);

            srcStorage.RemoveAt(srcIndex);
            srcStorage.RemoveAt(dstIndex);

            PokemonInstance _;
            srcStorage.TryInsertAt(srcIndex, b, out _);
            srcStorage.TryInsertAt(dstIndex, a, out _);

            // Compactar si es party y pasar selección al nuevo índice del movido
            CompactIfParty(srcStorage);
            if (srcStorage is PokemonParty) SelectMovedInParty(moved);

            RefreshGrids(srcGridForRefresh, dstGrid);
            FinalizeDrop();
            return;
        }
        else
        {
            // Movimiento entre contenedores con posible intercambio
            var moving = srcStorage.GetAt(srcIndex);
            var removed = srcStorage.RemoveAt(srcIndex);
            if (removed != moving)
            {
                if (removed != null) srcStorage.TryInsertAt(srcIndex, removed, out _);
                CleanupAfterOperation(dstGrid);
                return;
            }

            if (!dstStorage.TryInsertAt(dstIndex, moving, out var displaced))
            {
                // no se pudo insertar; revertir
                srcStorage.TryInsertAt(srcIndex, moving, out _);
                CleanupAfterOperation(dstGrid);
                return;
            }

            if (displaced != null)
            {
                if (!srcStorage.TryInsertAt(srcIndex, displaced, out var disp2))
                {
                    if (!srcStorage.TryAdd(displaced))
                        dstStorage.TryAdd(displaced); // último recurso
                }
            }

            // Compactar parties implicadas
            CompactIfParty(srcStorage);
            CompactIfParty(dstStorage);

            // Selección:
            if (dstStorage is PokemonParty) SelectMovedInParty(moving);
            if (srcStorage is PokemonParty) SelectFirstAliveIfNone();

            RefreshGrids(srcGridForRefresh, dstGrid);
            FinalizeDrop();
            return;
        }
    }

    // ===== Helpers =====
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

        IsDraggingAny = false;
        var dragSvc = ServiceLocator.Get<DraggingStateService>();
        if (dragSvc != null) dragSvc.Set(false);

        var selector = UnityEngine.Object.FindAnyObjectByType<ItemSelectorUI>();
        if (selector != null) selector.RefreshCapturedPokemon();

        draggingFrom = null;
        srcStorage = null; srcIndex = -1; srcGridForRefresh = null; srcIsParty = false;
    }

    private void CleanupAfterOperation(StorageGridUI dstGrid)
    {
        if (dstGrid) dstGrid.Refresh();
        FinalizeDrop();
    }

    private static void CompactIfParty(IPokemonStorage storage)
    {
        if (storage is PokemonParty party)
            party.Compact();
    }

    private static void SelectMovedInParty(PokemonInstance moved)
    {
        if (moved == null) return;
        var roster = ServiceLocator.Get<PokemonRosterService>();
        var mgr = PokemonStorageManager.Instance;
        if (roster == null || mgr == null) return;

        var party = mgr.PlayerParty;
        if (party == null) return;

        int idx = -1;
        for (int i = 0; i < party.MaxCapacity; i++)
            if (party.GetAt(i) == moved) { idx = i; break; }

        if (idx >= 0) roster.Select(idx);
        else SelectFirstAliveIfNone();
    }

    private static void SelectFirstAliveIfNone()
    {
        var roster = ServiceLocator.Get<PokemonRosterService>();
        if (roster == null) return;
        if (roster.Selected != null) return;
        int idx = roster.FirstAliveIndex();
        if (idx >= 0) roster.Select(idx);
    }
}
