using System.Collections.Generic;
using UnityEngine;

public class MoveGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform content;        // contenedor con Grid/VerticalLayoutGroup
    [SerializeField] private GameObject slotPrefab;    // prefab con MoveSlotUI (nombre/PP/tipo)

    public RectTransform ContentRT => (RectTransform)content;
    public Canvas RootCanvas { get; private set; }

    private PokemonInstance current;
    // índice visual (0..3) -> índice real (0..3), -1 = hueco
    private readonly List<int> visibleToModel = new();
    // nº de filas con movimiento (para bloquear placeholder/drag)
    private int nonNullVisualCount = 0;

    private readonly List<MoveSlotUI> liveSlots = new();

    private void Awake()
    {
        if (!content) content = transform;
        RootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (!RootCanvas) Debug.LogWarning("[MoveGridUI] No se encontró Canvas raíz.");
    }

    private void OnDisable()
    {
        foreach (var s in liveSlots) if (s) s.DetachGrid();
        liveSlots.Clear();
    }

    // Llamar al seleccionar un Pokémon de la party
    public void SetPokemon(PokemonInstance p)
    {
        current = p;
        Refresh();
    }

    public void Refresh()
    {
        foreach (var s in liveSlots) if (s) s.DetachGrid();
        liveSlots.Clear();
        visibleToModel.Clear();
        nonNullVisualCount = 0;

        // limpiar hijos
        if (content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }
        if (current == null || content == null) return;

        // Normaliza + compacta: cualquier MoveInstance sin data es "vacío"
        current.CompactMoves();

        // recolectar NO-NULOS reales (mv != null && mv.data != null)
        var nonNull = new List<(int modelIndex, MoveInstance move)>(4);
        for (int i = 0; i < 4 && i < current.Moves.Count; i++)
        {
            var mv = current.Moves[i];
            if (!IsNullMove(mv)) nonNull.Add((i, mv));
        }
        nonNullVisualCount = nonNull.Count; // 0..4

        // construir SIEMPRE 4 filas
        for (int vis = 0; vis < 4; vis++)
        {
            var go = Instantiate(slotPrefab, content);
            var slot = go.GetComponent<MoveSlotUI>();
            if (!slot) slot = go.AddComponent<MoveSlotUI>();
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

    // ---------- APIs que usan los slots durante el drag ----------
    public int FindIndexForPointer(Vector2 screenPos, Transform ignoreChild)
    {
        int targetIndex = ContentRT.childCount;
        for (int i = 0; i < ContentRT.childCount; i++)
        {
            var child = ContentRT.GetChild(i) as RectTransform;
            if (!child || child == ignoreChild) continue;

            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            float midY = 0.5f * (corners[0].y + corners[1].y);
            if (screenPos.y > midY) { targetIndex = i; break; }
        }
        return targetIndex;
    }

    // Limita el placeholder a la zona de movimientos reales (no-vacíos)
    public int ClampToNonNullZone(int requestedIndex)
    {
        if (nonNullVisualCount <= 0) return 0;
        int max = Mathf.Max(0, nonNullVisualCount - 1);
        return Mathf.Clamp(requestedIndex, 0, max);
    }

    // --- SWAP: si sueltas sobre otro movimiento, intercambia; si es hueco, mueve al primer hueco ---
    public void NotifyDrop(int fromVisual, int toVisual)
    {
        if (current == null) { Refresh(); return; }

        int fromModel = (fromVisual >= 0 && fromVisual < visibleToModel.Count) ? visibleToModel[fromVisual] : -1;
        if (fromModel < 0) { Refresh(); return; } // no arrastramos vacíos

        // Aseguramos que el destino visual está dentro de la zona de no-vacíos
        int clampedVis = ClampToNonNullZone(toVisual);
        if (clampedVis < 0 || clampedVis >= visibleToModel.Count) clampedVis = Mathf.Clamp(nonNullVisualCount - 1, 0, 3);

        int targetModel = visibleToModel[clampedVis];

        if (targetModel >= 0 && targetModel != fromModel)
        {
            // Intercambio directo
            current.SwapMoves(fromModel, targetModel);
        }
        else
        {
            // Por si acaso cae sobre hueco o la misma posición: enviar al primer hueco real
            int firstNull = FirstNullIndex(current);
            int dest = (firstNull >= 0) ? firstNull : Mathf.Min(3, LastNonNullIndex(current) + 1);
            current.MoveMove(fromModel, dest);
        }

        current.CompactMoves();                // nulls SIEMPRE al final
        try { SaveManager.Instance?.ManualSave(); } catch { }
        Refresh();
    }
    // -----------------------------------------------------------------------------------------------

    public (float minY, float maxY) GetVerticalBoundsScreen()
    {
        Vector3[] c = new Vector3[4];
        ContentRT.GetWorldCorners(c);
        return (c[0].y, c[1].y);
    }
    public float GetRowCenterXScreen(Transform row)
    {
        Vector3[] c = new Vector3[4];
        (row as RectTransform).GetWorldCorners(c);
        return 0.5f * (c[0].x + c[3].x);
    }

    // ---------- utilidades ----------
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
