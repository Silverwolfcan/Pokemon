using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways, AddComponentMenu("UI/Pokemon Radar Chart (TMP)")]
public class PokemonRadarChart : Graphic
{
    // ----- DATOS -----
    [Header("Valores (actuales)")]
    public float HP = 394f, Attack = 251f, Defense = 251f, Speed = 240f, SpDefense = 394f, SpAttack = 350f;

    [Header("Valores máximos (normalización)")]
    public float MaxHP = 500f, MaxAttack = 400f, MaxDefense = 400f, MaxSpeed = 400f, MaxSpDefense = 400f, MaxSpAttack = 400f;

    // ----- ESTILO Y GEOMETRÍA -----
    [Header("Geometría")]
    public float radius = 0f;
    [Tooltip("Ángulo inicial en grados. -90 coloca HP arriba.")]
    public float startAngleDeg = -90f;

    [Header("Colores")]
    public Color fillColor = new Color(1f, 0.85f, 0.1f, 1f); // opaco
    public Color valueOutlineColor = new Color(0f, 0f, 0f, 0.55f);
    public Color maxRingFillColor = new Color(1f, 1f, 1f, 0.08f);
    public Color gridColor = new Color(1f, 1f, 1f, 0.12f);

    [Header("Trazos")]
    [Range(0f, 8f)] public float valueOutlineThickness = 2f;

    [Header("Rejilla")]
    [Range(0, 8)] public int gridRings = 4;
    [Range(0f, 6f)] public float gridThickness = 1.2f;

    [Header("Ejes (interiores)")]
    public bool drawAxes = true;
    [Range(0f, 6f)] public float axisThickness = 1.5f;
    public Color axisColor = new Color(1f, 1f, 1f, 0.25f);
    public bool axisOutline = true;
    [Range(0f, 8f)] public float axisOutlineThickness = 3f;
    public Color axisOutlineColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Borde exterior (hexágono máximo)")]
    public bool outerOutline = true;
    [Range(0f, 8f)] public float outerOutlineThickness = 2.5f;
    public Color outerOutlineColor = new Color(0f, 0f, 0f, 0.4f);

    [Header("Fondo del máximo")]
    public bool drawMaxBackground = true;

    // ----- MARCADORES EN EL BORDE -----
    public enum MarkerShape { Triangle, Circle }
    [Header("Marcadores de pico (en el radio máximo)")]
    public bool drawMarkers = true;
    public MarkerShape markerShape = MarkerShape.Triangle;
    public float markerOffset = 6f;
    public float markerSize = 12f;
    public float markerBaseWidth = 16f;

    [Tooltip("Radio de redondeo para los vértices del triángulo. Si es 0, vértice en punta.")]
    public float markerCornerRadius = 4f;
    [Range(1, 12), Tooltip("Segmentos por esquina para el redondeo.")]
    public int markerCornerSegments = 4;

    public Color[] markerColors = new Color[6]
    {
        new Color(0.31f,0.87f,0.35f,1f), // HP
        new Color(1f,0.86f,0.35f,1f),    // Attack
        new Color(1f,0.62f,0.29f,1f),    // Defense
        new Color(1f,0.42f,0.74f,1f),    // Speed
        new Color(0.45f,0.72f,1f,1f),    // SpDef
        new Color(0.32f,0.82f,0.98f,1f), // SpAtk
    };

    // ----- AUTO-ETIQUETAS (TMP) -----
    [Header("Auto-etiquetas TMP (opcional)")]
    public bool autoPlaceLabels = true;
    public TMP_Text[] labels = new TMP_Text[6]; // HP, Attack, Defense, Speed, SpDef, SpAtk
    public float labelPadding = 18f;
    public string[] labelNames = { "HP", "Attack", "Defense", "Speed", "Sp.Def", "Sp.Atk" };
    public bool showValuesInLabels = true;
    public string labelTemplate = "{name}\n{value}";
    public string numberFormat = "0";

    // -------------------------------------------------------------

    protected override void OnValidate()
    {
        base.OnValidate();
        MaxHP = Mathf.Max(0.0001f, MaxHP);
        MaxAttack = Mathf.Max(0.0001f, MaxAttack);
        MaxDefense = Mathf.Max(0.0001f, MaxDefense);
        MaxSpeed = Mathf.Max(0.0001f, MaxSpeed);
        MaxSpDefense = Mathf.Max(0.0001f, MaxSpDefense);
        MaxSpAttack = Mathf.Max(0.0001f, MaxSpAttack);
        if (fillColor.a < 1f) fillColor.a = 1f;
        SetVerticesDirty();
    }

    [ContextMenu("Randomize Values")]
    void Randomize()
    {
        System.Random rng = new System.Random();
        float R(float max) => (float)(rng.NextDouble() * max * 0.95 + max * 0.05);
        HP = R(MaxHP); Attack = R(MaxAttack); Defense = R(MaxDefense);
        Speed = R(MaxSpeed); SpDefense = R(MaxSpDefense); SpAttack = R(MaxSpAttack);
        SetVerticesDirty();
    }

    void LateUpdate()
    {
        if (autoPlaceLabels && labels != null && labels.Length >= 6)
            PositionLabelsAndTexts();
    }

    // -------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        const int N = 6;

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float maxR = radius > 0f ? radius : Mathf.Min(rect.width, rect.height) * 0.5f;
        float step = Mathf.PI * 2f / N;
        float start = startAngleDeg * Mathf.Deg2Rad;

        // Direcciones y vértices del hexágono máximo
        Vector2[] dirs = new Vector2[N];
        Vector2[] ringMax = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            dirs[i] = AxisDir(i, start, step);
            ringMax[i] = center + dirs[i] * maxR;
        }

        // 1) Dibujos de fondo (grid, ejes, contorno exterior)
        if (gridRings > 0 && gridThickness > 0.01f)
        {
            for (int r = 1; r <= gridRings; r++)
            {
                float t = r / (float)gridRings;
                float ro = maxR * t;
                float ri = Mathf.Max(0f, ro - gridThickness);
                AddRing(vh, center, ri, ro, N, start, step, Mult(gridColor, color));
            }
        }

        if (drawAxes && axisThickness > 0.01f)
        {
            for (int i = 0; i < N; i++)
            {
                if (axisOutline && axisOutlineThickness > axisThickness)
                    AddQuadLine(vh, center, ringMax[i], axisOutlineThickness, Mult(axisOutlineColor, color));
                AddQuadLine(vh, center, ringMax[i], axisThickness, Mult(axisColor, color));
            }
        }

        if (outerOutline && outerOutlineThickness > 0.01f)
        {
            for (int i = 0; i < N; i++)
            {
                Vector2 a = ringMax[i];
                Vector2 b = ringMax[(i + 1) % N];
                AddQuadLine(vh, a, b, outerOutlineThickness, Mult(outerOutlineColor, color));
            }
        }

        if (drawMaxBackground)
            AddFilledPolygon(vh, ringMax, Mult(maxRingFillColor, color));

        // 2) Polígono de valores (opaco) + contorno
        float[] vals = { HP, Attack, Defense, Speed, SpDefense, SpAttack };
        float[] maxs = { MaxHP, MaxAttack, MaxDefense, MaxSpeed, MaxSpDefense, MaxSpAttack };

        Vector2[] ringValue = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            float norm = Mathf.Clamp01(vals[i] / maxs[i]);
            ringValue[i] = center + dirs[i] * (maxR * norm);
        }

        AddFanFromCenter(vh, center, ringValue, Mult(fillColor, color));

        if (valueOutlineThickness > 0.01f)
        {
            for (int i = 0; i < N; i++)
            {
                Vector2 a = ringValue[i];
                Vector2 b = ringValue[(i + 1) % N];
                AddQuadLine(vh, a, b, valueOutlineThickness, Mult(valueOutlineColor, color));
            }
        }

        // 3) Marcadores (triángulos con vértices redondeados o puntos)
        if (drawMarkers)
        {
            for (int i = 0; i < N; i++)
            {
                Color mc = (markerColors != null && markerColors.Length > i) ? markerColors[i] : Color.white;
                if (markerShape == MarkerShape.Circle)
                {
                    AddCircleMarker(vh, center + dirs[i] * (maxR + markerOffset + markerSize * 0.5f), markerSize * 0.5f, 12, mc);
                }
                else // Triangle (con o sin redondeo)
                {
                    AddRoundedTriangleMarker(
                        vh, center, dirs[i], maxR,
                        markerOffset, markerSize, markerBaseWidth,
                        markerCornerRadius, markerCornerSegments, mc
                    );
                }
            }
        }
    }

    // ---------- Auto-etiquetado TMP ----------
    void PositionLabelsAndTexts()
    {
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float maxR = radius > 0f ? radius : Mathf.Min(rect.width, rect.height) * 0.5f;
        float step = Mathf.PI * 2f / 6f;
        float start = startAngleDeg * Mathf.Deg2Rad;

        float[] vals = { HP, Attack, Defense, Speed, SpDefense, SpAttack };

        for (int i = 0; i < 6; i++)
        {
            var lab = labels[i];
            if (!lab) continue;

            Vector2 dir = AxisDir(i, start, step);
            var rt = lab.rectTransform;
            rt.anchoredPosition = center + dir * (maxR + labelPadding);

            if (labelNames != null && i < labelNames.Length)
            {
                lab.text = showValuesInLabels
                    ? labelTemplate.Replace("{name}", labelNames[i]).Replace("{value}", vals[i].ToString(numberFormat))
                    : labelNames[i];
            }
        }
    }

    // ---------- Helpers geométricos ----------
    static Vector2 AxisDir(int i, float start, float step)
    {
        float a = start + step * i;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    static Color Mult(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    static void AddFilledPolygon(VertexHelper vh, Vector2[] verts, Color col)
    {
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < verts.Length; i++) centroid += verts[i];
        centroid /= verts.Length;

        int baseIndex = vh.currentVertCount;
        vh.AddVert(centroid, col, Vector2.zero);
        for (int i = 0; i < verts.Length; i++) vh.AddVert(verts[i], col, Vector2.zero);

        for (int i = 0; i < verts.Length; i++)
        {
            int a = baseIndex;
            int b = baseIndex + 1 + i;
            int c = baseIndex + 1 + ((i + 1) % verts.Length);
            vh.AddTriangle(a, b, c);
        }
    }

    static void AddFanFromCenter(VertexHelper vh, Vector2 center, Vector2[] verts, Color col)
    {
        int baseIndex = vh.currentVertCount;
        vh.AddVert(center, col, Vector2.zero);
        for (int i = 0; i < verts.Length; i++) vh.AddVert(verts[i], col, Vector2.zero);

        for (int i = 0; i < verts.Length; i++)
        {
            int a = baseIndex;
            int b = baseIndex + 1 + i;
            int c = baseIndex + 1 + ((i + 1) % verts.Length);
            vh.AddTriangle(a, b, c);
        }
    }

    static void AddRing(VertexHelper vh, Vector2 center, float rInner, float rOuter,
                        int sides, float start, float step, Color col)
    {
        int startIndex = vh.currentVertCount;
        for (int i = 0; i < sides; i++)
        {
            float a = start + step * i;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(center + dir * rOuter, col, Vector2.zero);
            vh.AddVert(center + dir * rInner, col, Vector2.zero);
        }
        for (int i = 0; i < sides; i++)
        {
            int i0 = startIndex + i * 2;
            int i1 = startIndex + ((i + 1) % sides) * 2;
            vh.AddTriangle(i0, i0 + 1, i1 + 1);
            vh.AddTriangle(i0, i1 + 1, i1);
        }
    }

    static void AddQuadLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color col)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-6f) return;
        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (thickness * 0.5f);

        int start = vh.currentVertCount;
        vh.AddVert(a + n, col, Vector2.zero);
        vh.AddVert(b + n, col, Vector2.zero);
        vh.AddVert(b - n, col, Vector2.zero);
        vh.AddVert(a - n, col, Vector2.zero);

        vh.AddTriangle(start + 0, start + 1, start + 2);
        vh.AddTriangle(start + 0, start + 2, start + 3);
    }

    // ---- Marcadores ----

    // Triángulo con esquinas redondeadas (aproximación por arcos)
    static void AddRoundedTriangleMarker(
        VertexHelper vh, Vector2 center, Vector2 dir, float baseRadius,
        float offsetOut, float height, float baseWidth,
        float cornerRadius, int cornerSegments, Color col)
    {
        Vector2 tangent = new Vector2(-dir.y, dir.x);
        Vector2 baseCenter = center + dir * (baseRadius + offsetOut);
        Vector2 tip = baseCenter + dir * height;
        Vector2 bl = baseCenter - tangent * (baseWidth * 0.5f); // base left
        Vector2 br = baseCenter + tangent * (baseWidth * 0.5f); // base right

        if (cornerRadius <= 0.0001f || cornerSegments < 1)
        {
            // Triángulo normal
            int i0 = vh.currentVertCount;
            vh.AddVert(tip, col, Vector2.zero);
            vh.AddVert(bl, col, Vector2.zero);
            vh.AddVert(br, col, Vector2.zero);
            vh.AddTriangle(i0, i0 + 1, i0 + 2);
            return;
        }

        // Polígono base en orden (CCW): bl -> tip -> br
        Vector2[] poly = new Vector2[] { bl, tip, br };

        // Genera puntos con arcos en cada esquina
        System.Collections.Generic.List<Vector2> pts = new System.Collections.Generic.List<Vector2>(3 * (cornerSegments + 1));
        for (int i = 0; i < 3; i++)
        {
            Vector2 C = poly[i];
            Vector2 Pp = poly[(i + 2) % 3]; // prev
            Vector2 Pn = poly[(i + 1) % 3]; // next

            Vector2 d1 = (Pp - C);
            Vector2 d2 = (Pn - C);
            float len1 = d1.magnitude;
            float len2 = d2.magnitude;

            if (len1 < 1e-4f || len2 < 1e-4f)
                continue;

            d1 /= len1; d2 /= len2;

            float r = Mathf.Min(cornerRadius, 0.49f * Mathf.Min(len1, len2));

            // puntos de inicio/fin del arco
            Vector2 pStart = C + d1 * r;
            Vector2 pEnd = C + d2 * r;

            float aStart = Mathf.Atan2(d1.y, d1.x);
            float aEnd = Mathf.Atan2(d2.y, d2.x);
            float deltaDeg = Mathf.DeltaAngle(aStart * Mathf.Rad2Deg, aEnd * Mathf.Rad2Deg);
            float stepDeg = deltaDeg / cornerSegments;

            for (int s = 0; s <= cornerSegments; s++)
            {
                float a = aStart + stepDeg * s * Mathf.Deg2Rad;
                pts.Add(C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }

        // Triangulación por "fan" desde el centroide del polígono redondeado
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) centroid += pts[i];
        centroid /= Mathf.Max(1, pts.Count);

        int baseIndex = vh.currentVertCount;
        vh.AddVert(centroid, col, Vector2.zero);
        for (int i = 0; i < pts.Count; i++) vh.AddVert(pts[i], col, Vector2.zero);

        for (int i = 0; i < pts.Count; i++)
        {
            int a = baseIndex;
            int b = baseIndex + 1 + i;
            int c = baseIndex + 1 + ((i + 1) % pts.Count);
            vh.AddTriangle(a, b, c);
        }
    }

    static void AddCircleMarker(VertexHelper vh, Vector2 center, float r, int sides, Color col)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, col, Vector2.zero);
        for (int i = 0; i < sides; i++)
        {
            float a = (Mathf.PI * 2f * i) / sides;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            vh.AddVert(p, col, Vector2.zero);
        }
        for (int i = 0; i < sides; i++)
        {
            int b = start + 1 + i;
            int c = start + 1 + ((i + 1) % sides);
            vh.AddTriangle(start, b, c);
        }
    }
}
