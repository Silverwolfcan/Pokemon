using System.Collections.Generic;
using UnityEngine;

public class MoveGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform content;        // contenedor con GridLayoutGroup
    [SerializeField] private GameObject slotPrefab;    // prefab con MoveSlotUI (referencias asignadas)

    public RectTransform ContentRT => (RectTransform)content;
    public Canvas RootCanvas { get; private set; }

    private PokemonInstance current;

    // Mapeo visual (0..3) -> índice real (0..3). -1 = hueco
    private readonly List<int> visibleToModel = new();
    private int nonNullVisualCount = 0;

    private readonly List<MoveSlotUI> liveSlots = new();

    private void Awake()
    {
        if (!content) content = transform;
        RootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (!RootCanvas)
            Debug.LogWarning("[MoveGridUI] RootCanvas es null.");
    }

    private void OnDisable()
    {
        foreach (var s in liveSlots) if (s) s.DetachGrid();
        liveSlots.Clear();
    }

    public void SetPokemon(PokemonInstance p)
    {
        current = p;
        Refresh();
    }

    public void Refresh()
    {
        if (!content)
        {
            Debug.LogError("[MoveGridUI] Falta asignar 'content'.");
            return;
        }
        if (!slotPrefab)
        {
            Debug.LogError("[MoveGridUI] Falta asignar 'slotPrefab' (Prefab_MoveSlot con MoveSlotUI).");
            return;
        }

        foreach (var s in liveSlots) if (s) s.DetachGrid();
        liveSlots.Clear();
        visibleToModel.Clear();
        nonNullVisualCount = 0;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        if (current == null) return;

        // Normaliza lista (mueve nulls al final manteniendo orden)
        current.CompactMoves();

        // Recolectar no-nulos reales
        var nonNull = new List<(int modelIndex, MoveInstance move)>(4);
        for (int i = 0; i < 4 && i < current.Moves.Count; i++)
        {
            var mv = current.Moves[i];
            if (!IsNullMove(mv)) nonNull.Add((i, mv));
        }
        nonNullVisualCount = nonNull.Count;

        // Construye siempre 4 celdas visuales (2x2)
        for (int vis = 0; vis < 4; vis++)
        {
            var go = Instantiate(slotPrefab, content);
            var slot = go.GetComponent<MoveSlotUI>();
            if (!slot)
            {
                Debug.LogError("[MoveGridUI] El slotPrefab no tiene MoveSlotUI.");
                Destroy(go);
                continue;
            }

            slot.AttachGrid(this);

            if (vis < nonNull.Count)
            {
                var (model, mv) = nonNull[vis];
                visibleToModel.Add(model);
                slot.Setup(mv, current, transparentIfEmpty: false);
            }
            else
            {
                visibleToModel.Add(-1);
                slot.Setup(null, current, transparentIfEmpty: true);
            }

            liveSlots.Add(slot);
        }
    }

    // ---------- APIs usadas por MoveSlotUI durante drag ----------
    /// Devuelve el índice visual (0..N-1) del hijo cuya caja contiene el puntero.
    /// Si no cae dentro de ninguna caja, devuelve el más cercano por centro.
    public int FindIndexForPointer(Vector2 screenPos, Transform ignoreChild)
    {
        int bestIndex = -1;
        float bestDist = float.MaxValue;

        Camera cam = RootCanvas && RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? RootCanvas.worldCamera : null;

        for (int i = 0; i < ContentRT.childCount; i++)
        {
            var child = ContentRT.GetChild(i) as RectTransform;
            if (!child || child == ignoreChild) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(child, screenPos, cam))
                return i;

            // distancia al centro como fallback
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var center = 0.5f * (corners[0] + corners[2]);
            float d = (new Vector2(center.x, center.y) - screenPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestIndex = i; }
        }

        // si no hay hijos (no debería ocurrir) o todos ignorados
        if (bestIndex < 0) bestIndex = Mathf.Clamp(ContentRT.childCount - 1, 0, int.MaxValue);
        return bestIndex;
    }

    /// Restringe el índice visual de destino para no soltar “después” del último movimiento real.
    public int ClampToNonNullZone(int requestedIndex)
    {
        if (nonNullVisualCount <= 0) return 0;
        int max = Mathf.Max(0, nonNullVisualCount - 1);
        return Mathf.Clamp(requestedIndex, 0, max);
    }

    public void NotifyDrop(int fromVisual, int toVisual)
    {
        if (current == null) { Refresh(); return; }

        int fromModel = (fromVisual >= 0 && fromVisual < visibleToModel.Count) ? visibleToModel[fromVisual] : -1;
        if (fromModel < 0) { Refresh(); return; } // no arrastramos vacíos

        int clampedVis = ClampToNonNullZone(toVisual);
        clampedVis = Mathf.Clamp(clampedVis, 0, visibleToModel.Count - 1);

        int targetModel = visibleToModel[clampedVis];

        if (targetModel >= 0 && targetModel != fromModel)
        {
            current.SwapMoves(fromModel, targetModel);
        }
        else
        {
            int firstNull = FirstNullIndex(current);
            int dest = (firstNull >= 0) ? firstNull : Mathf.Min(3, LastNonNullIndex(current) + 1);
            current.MoveMove(fromModel, dest);
        }

        current.CompactMoves();
        try { SaveManager.Instance?.ManualSave(); } catch { }
        Refresh();
    }
    // --------------------------------------------------------------

    private static bool IsNullMove(MoveInstance mv) => (mv == null || mv.data == null);
    private static int FirstNullIndex(PokemonInstance p)
    {
        for (int i = 0; i < 4 && i < p.Moves.Count; i++)
            if (IsNullMove(p.Moves[i])) return i;
        return -1;
    }
    private static int LastNonNullIndex(PokemonInstance p)
    {
        for (int i = Mathf.Min(3, p.Moves.Count - 1); i >= 0; i--)
            if (!IsNullMove(p.Moves[i])) return i;
        return -1;
    }
}
