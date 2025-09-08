using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.AI;

public class CombatBoundary : MonoBehaviour
{
    // ---- API existente ----
    private Func<Vector3> getPlayerPos;
    private Action<Vector3> setPlayerPos;
    private Func<Vector3> getCenter;
    private float radius;

    public void Setup(Func<Vector3> getPlayerPos, Action<Vector3> setPlayerPos,
                      Func<Vector3> getCenter, float radius)
    {
        this.getPlayerPos = getPlayerPos;
        this.setPlayerPos = setPlayerPos;
        this.getCenter = getCenter;
        this.radius = Mathf.Max(0.1f, radius);
        enabled = true;
        _nextScan = 0f;
    }

    public void Clear()
    {
        enabled = false;
        getPlayerPos = null;
        setPlayerPos = null;
        getCenter = null;
        radius = 0f;
        ReleaseAllAgents();
    }

    // ---- Repulsión integrada ----
    [Header("Repulsión de criaturas cercanas")]
    [SerializeField] private bool repelOthers = true;
    [SerializeField, Min(0.1f)] private float repelMargin = 1.5f;
    [SerializeField, Min(2f)] private float outerPadding = 12f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.4f;
    [SerializeField] private LayerMask creatureMask = ~0;

    [Header("Movimiento de repelidos")]
    [SerializeField, Min(0.5f)] private float minSpeed = 3.5f;
    [SerializeField, Min(0.5f)] private float maxSpeed = 6.0f;
    [SerializeField, Min(0.05f)] private float retargetInterval = 0.5f;
    [SerializeField] private bool physicsPushIfNoAgent = true;
    [SerializeField, Min(0f)] private float pushForce = 6f;

    private struct AgentInfo
    {
        public Transform root;
        public NavMeshAgent agent;
        public Rigidbody rb;
        public float origSpeed;
        public float nextRetarget;
    }

    private readonly Dictionary<Transform, AgentInfo> _active = new Dictionary<Transform, AgentInfo>();
    private float _nextScan;

    private void LateUpdate()
    {
        if (getPlayerPos == null || setPlayerPos == null || getCenter == null) return;

        // 1) Limitar jugador dentro del anillo
        var pos = getPlayerPos();
        var c = getCenter();

        var flat = pos; flat.y = 0f;
        var flatC = c; flatC.y = 0f;

        var v = flat - flatC;
        var d = v.magnitude;
        if (d > radius)
        {
            var clamped = flatC + v.normalized * radius;
            clamped.y = pos.y;
            setPlayerPos(clamped);
        }

        // 2) Repeler criaturas cercanas
        if (!repelOthers) return;

        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + scanInterval;
            ScanAndAttach(flatC);
        }
        UpdateAgents(flatC);
    }

    // ---------- Repulsión ----------
    private void ScanAndAttach(Vector3 center)
    {
        float inner = radius + repelMargin;
        float outer = inner + outerPadding;

        var cols = Physics.OverlapSphere(center, outer, creatureMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            var root = cols[i].transform.root;
            if (!root) continue;

            // Excluir combatientes activos y jugador
            if (root.GetComponentInChildren<CombatantController>(true) != null) continue;
            if (root.GetComponentInChildren<PlayerController>(true) != null) continue;

            // Requisitos mínimos de “criatura”
            var agent = root.GetComponentInChildren<NavMeshAgent>(true);
            var rb = root.GetComponentInChildren<Rigidbody>(true);
            if (agent == null && rb == null) continue;

            if (_active.ContainsKey(root)) continue;

            var info = new AgentInfo
            {
                root = root,
                agent = agent,
                rb = rb,
                origSpeed = agent ? agent.speed : 0f,
                nextRetarget = 0f
            };
            _active[root] = info;
        }

        // Limpia nulos
        var keys = new List<Transform>(_active.Keys);
        for (int i = 0; i < keys.Count; i++)
            if (!keys[i]) _active.Remove(keys[i]);
    }

    private void UpdateAgents(Vector3 center)
    {
        float inner = radius + repelMargin;
        float outer = inner + outerPadding;

        var toRemove = new List<Transform>();
        var keys = new List<Transform>(_active.Keys); // snapshot para evitar InvalidOperationException

        for (int k = 0; k < keys.Count; k++)
        {
            var t = keys[k];
            if (!_active.TryGetValue(t, out var info) || !t)
            {
                toRemove.Add(t);
                continue;
            }

            Vector3 pos = t.position;
            Vector3 dir = pos - center; dir.y = 0f;
            float dist = dir.magnitude;

            float targetDist = Mathf.Max(inner + 0.75f, dist);
            if (targetDist > outer - 0.25f) targetDist = outer - 0.25f;

            Vector3 baseDir = (dir.sqrMagnitude > 1e-4f ? dir.normalized : UnityEngine.Random.onUnitSphere);
            baseDir.y = 0f;
            Vector3 target = center + baseDir * targetDist;
            target.y = pos.y;

            bool needRetarget = Time.unscaledTime >= info.nextRetarget || dist < inner;

            bool canUseAgent =
                info.agent != null &&
                info.agent.enabled &&
                info.agent.isActiveAndEnabled &&
                info.agent.gameObject.activeInHierarchy &&
                info.agent.isOnNavMesh;

            if (canUseAgent)
            {
                if (needRetarget)
                {
                    info.agent.speed = Mathf.Clamp(info.agent.speed, minSpeed, maxSpeed);
                    // No tocar isStopped: evitar “Resume” en agentes fuera de NavMesh
                    info.agent.SetDestination(target);
                    info.nextRetarget = Time.unscaledTime + retargetInterval;
                }
            }
            else if (physicsPushIfNoAgent && info.rb != null && !info.rb.isKinematic)
            {
                if (dist < inner)
                {
                    Vector3 pushDir = (dir.sqrMagnitude > 1e-4f ? dir.normalized : UnityEngine.Random.insideUnitSphere);
                    pushDir.y = 0f;
                    info.rb.AddForce(pushDir * pushForce, ForceMode.Acceleration);
                }
            }
            else
            {
                if (dist < inner)
                {
                    float spd = Mathf.Lerp(minSpeed, maxSpeed, 0.5f);
                    Vector3 step = (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward) * spd * Time.deltaTime;
                    t.position += step;
                }
            }

            // Guardar cambios del struct sin invalidar el enumerador
            _active[t] = info;

            // Quitar si se fue lejísimos o si ya no existe
            if (!t || (t.position - center).sqrMagnitude > (outer * outer * 1.5f))
                toRemove.Add(t);
        }

        for (int i = 0; i < toRemove.Count; i++) ReleaseAgent(toRemove[i]);
    }

    private void ReleaseAllAgents()
    {
        var keys = new List<Transform>(_active.Keys);
        for (int i = 0; i < keys.Count; i++) ReleaseAgent(keys[i]);
        _active.Clear();
    }

    private void ReleaseAgent(Transform t)
    {
        if (!_active.TryGetValue(t, out var info)) return;
        if (info.agent != null && info.agent.enabled)
            info.agent.speed = info.origSpeed > 0f ? info.origSpeed : info.agent.speed;
        _active.Remove(t);
    }
}
