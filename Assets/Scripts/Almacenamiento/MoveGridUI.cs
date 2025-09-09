using System;
using System.Collections.Generic;
using UnityEngine;

public class MoveGridUI : MonoBehaviour
{
    public enum GridMode { Edit, Combat }

    [Header("Refs")]
    [SerializeField] private Transform content;
    [SerializeField] private GameObject slotPrefab;

    [Header("Modo")]
    [SerializeField] private GridMode mode = GridMode.Edit;

    [Header("Turn Controller (solo combate)")]
    [SerializeField] private TurnController turn; // opcional

    public RectTransform ContentRT => (RectTransform)content;
    public Canvas RootCanvas { get; private set; }
    public GridMode Mode => mode;

    public event Action<int> OnMoveChosen;
    public event Action<int> OnMoveSelected;

    private PokemonInstance current;
    private readonly List<int> visibleToModel = new();
    private int nonNullVisualCount = 0;
    private readonly List<MoveSlotUI> liveSlots = new();

    private void Awake()
    {
        if (!content) content = transform;
        RootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (!turn) turn = ResolveTurn();
    }
    private void Update()
    {
        if (!turn) turn = ResolveTurn();
    }
    private TurnController ResolveTurn()
    {
        var cs = CombatService.Instance;
        var t = cs != null ? cs.CurrentTurn : null;
        if (t == null) t = FindAnyObjectByType<TurnController>(FindObjectsInactive.Exclude);
        return t;
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
    public void SetMode(GridMode m) => mode = m;

    public void Refresh()
    {
        if (!content) { Debug.LogError("[MoveGridUI] Falta 'content'."); return; }
        if (!slotPrefab) { Debug.LogError("[MoveGridUI] Falta 'slotPrefab'."); return; }

        foreach (var s in liveSlots) if (s) s.DetachGrid();
        liveSlots.Clear();
        visibleToModel.Clear();
        nonNullVisualCount = 0;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        if (current == null) return;

        current.CompactMoves();

        var nonNull = new List<(int modelIndex, MoveInstance move)>(4);
        for (int i = 0; i < 4 && i < current.Moves.Count; i++)
        {
            var mv = current.Moves[i];
            if (!IsNullMove(mv)) nonNull.Add((i, mv));
        }
        nonNullVisualCount = nonNull.Count;

        for (int vis = 0; vis < 4; vis++)
        {
            var go = Instantiate(slotPrefab, content);
            var slot = go.GetComponent<MoveSlotUI>();
            if (!slot) { Debug.LogError("[MoveGridUI] El slotPrefab no tiene MoveSlotUI."); Destroy(go); continue; }

            slot.AttachGrid(this);
            slot.SetVisualIndex(vis);

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

    public int FindIndexForPointer(Vector2 screenPos, Transform ignoreChild)
    {
        int bestIndex = -1; float bestDist = float.MaxValue;
        Camera cam = RootCanvas && RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? RootCanvas.worldCamera : null;

        for (int i = 0; i < ContentRT.childCount; i++)
        {
            var child = ContentRT.GetChild(i) as RectTransform;
            if (!child || child == ignoreChild) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(child, screenPos, cam))
                return i;

            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var center = 0.5f * (corners[0] + corners[2]);
            float d = (new Vector2(center.x, center.y) - screenPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestIndex = i; }
        }
        if (bestIndex < 0) bestIndex = Mathf.Clamp(ContentRT.childCount - 1, 0, int.MaxValue);
        return bestIndex;
    }

    public int ClampToNonNullZone(int requestedIndex)
    {
        if (nonNullVisualCount <= 0) return 0;
        int max = Mathf.Max(0, nonNullVisualCount - 1);
        return Mathf.Clamp(requestedIndex, 0, max);
    }

    public void NotifyDrop(int fromVisual, int toVisual)
    {
        if (mode != GridMode.Edit) return;
        if (current == null) { Refresh(); return; }

        int fromModel = (fromVisual >= 0 && fromVisual < visibleToModel.Count) ? visibleToModel[fromVisual] : -1;
        if (fromModel < 0) { Refresh(); return; }

        int clampedVis = ClampToNonNullZone(toVisual);
        clampedVis = Mathf.Clamp(clampedVis, 0, visibleToModel.Count - 1);

        int targetModel = visibleToModel[clampedVis];

        if (targetModel >= 0 && targetModel != fromModel)
            current.SwapMoves(fromModel, targetModel);
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

    public void NotifyClick(int visualIndex)
    {
        if (mode != GridMode.Combat) return;

        int modelIndex = (visualIndex >= 0 && visualIndex < visibleToModel.Count) ? visibleToModel[visualIndex] : -1;
        if (modelIndex < 0 || current == null) return;

        var mv = (modelIndex < current.Moves.Count) ? current.Moves[modelIndex] : null;
        if (IsNullMove(mv) || mv.currentPP <= 0) return;

        var t = turn ?? ResolveTurn();
        t?.QueueMove(modelIndex);

        OnMoveChosen?.Invoke(modelIndex);
        OnMoveSelected?.Invoke(modelIndex);
    }

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
