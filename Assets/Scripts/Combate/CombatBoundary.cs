using System;
using UnityEngine;

/// Limita ÚNICAMENTE al jugador dentro de un anillo lógico.
/// No crea colliders ni obstáculos. No afecta a NPCs ni agentes.
public class CombatBoundary : MonoBehaviour
{
    // Entradas delegadas desde EncounterController
    private Func<Vector3> getPlayerPos;
    private Action<Vector3> setPlayerPos;
    private Func<Vector3> getCenter;

    private float radius = 8f;
    [SerializeField] private float innerPadding = 0.05f; // pequeño margen para evitar jitter
    [SerializeField] private bool drawGizmos = true;

    public void Setup(Func<Vector3> getPlayerPos,
                      Action<Vector3> setPlayerPos,
                      Func<Vector3> getCenter,
                      float radius)
    {
        this.getPlayerPos = getPlayerPos;
        this.setPlayerPos = setPlayerPos;
        this.getCenter = getCenter;
        this.radius = Mathf.Max(0.1f, radius);
    }

    void LateUpdate()
    {
        if (getPlayerPos == null || setPlayerPos == null || getCenter == null) return;

        Vector3 p = getPlayerPos();
        Vector3 c = getCenter();

        Vector3 v = p - c;
        v.y = 0f;
        float d = v.magnitude;
        float maxR = Mathf.Max(0.01f, radius - innerPadding);

        if (d > maxR)
        {
            Vector3 clamped = c + (v / d) * maxR;
            clamped.y = p.y; // conservar altura del jugador
            setPlayerPos(clamped);
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (getCenter == null) return;

        Vector3 c = getCenter();
        c.y += 0.02f;
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        DrawCircleGizmo(c, radius, 48);
    }

    private void DrawCircleGizmo(Vector3 center, float r, int segs)
    {
        Vector3 prev = center + new Vector3(r, 0f, 0f);
        float step = Mathf.PI * 2f / Mathf.Max(8, segs);
        for (int i = 1; i <= segs; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
