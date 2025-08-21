using System.Collections;
using UnityEngine;

/// Pokéball:
/// 1) Vuelo por Bezier (kinemática) con SphereCast de impacto.
/// 2) Al terminar el arco o tocar suelo, activa física real (Rigidbody+gravedad).
/// 3) Al impactar un salvaje, congela la física, posa sobre el suelo y ejecuta animación de sacudidas.
/// 4) Refresca ItemSelectorUI y notifica a CombatService en éxito/fracaso.
/// 5) Fuerza 1 intento de captura por turno en combate.
/// 6) En fallo de captura, reanuda el comportamiento del salvaje y la bola desaparece inmediatamente.
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [Header("Captura")]
    [SerializeField] private PokeballData pokeballData;
    [Tooltip("Capa(s) de suelo para posado/colisión. Si no se asigna, se usará TODAS (~0).")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Altura a la que debe quedar la bola por encima del suelo al empezar la animación.")]
    [SerializeField] private float groundRestOffset = 0.5f;

    [Header("Vuelo (Bezier)")]
    [Tooltip("Radio de detección mientras vuela (SphereCast).")]
    [SerializeField] private float traceRadius = 0.12f;
    [Tooltip("Altura base del arco cuando distance=10m (se escala por distancia).")]
    [SerializeField] private float baseArcHeight = 2.0f;

    private Vector3 startPoint, endPoint, controlPoint;
    private float duration = 1f, timer = 0f;
    private bool physicsActivated = false;
    private bool hasHitPokemon = false;
    private bool hasLanded = false;

    // cache
    private CreatureBehavior targetPokemon;
    private GameObject targetObject;
    private Rigidbody rb;

    // prob. crítica adicional al multiplicador de la ball (MVP)
    private float criticalCaptureChance = 0.10f;

    // ---------------- API pública ----------------
    /// <param name="start">origen del lanzamiento</param>
    /// <param name="end">punto objetivo aprox.</param>
    /// <param name="maxHeight">altura extra solicitada (se mezcla con baseArcHeight)</param>
    /// <param name="maxCurveStrength">desviación lateral del arco (m)</param>
    public void Initialize(Vector3 start, Vector3 end, float maxHeight, float maxCurveStrength, PokeballData data)
    {
        // Evitar múltiples lanzamientos durante el mismo turno en combate
        if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
        {
            if (!CombatService.Instance.BeginCaptureAttempt())
            {
                Destroy(gameObject);
                return;
            }
        }

        startPoint = start;
        endPoint = end;
        pokeballData = data;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Random.onUnitSphere * 20f;

        // Calcular controlPoint (altura + curva lateral hacia la DERECHA)
        Vector3 dir = (endPoint - startPoint);
        float dist = Mathf.Max(0.01f, dir.magnitude);
        Vector3 mid = (startPoint + endPoint) * 0.5f;

        // Derecha: Cross(UP, dir)
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
        float arc = Mathf.Max(0.1f, baseArcHeight + maxHeight * 0.5f) * Mathf.Clamp(dist / 10f, 0.5f, 1.5f);
        float lateral = Mathf.Clamp(maxCurveStrength, -5f, 5f);

        controlPoint = mid + Vector3.up * arc + side * lateral;

        // Duración proporcional a la distancia → vuelo más natural
        duration = Mathf.Clamp(dist / 10f, 0.6f, 1.4f);
        timer = 0f;

        // Seguridad por si queda suelta
        Destroy(gameObject, 12f);
    }

    private void Update()
    {
        if (hasHitPokemon || hasLanded) return;

        if (!physicsActivated)
        {
            float prevT = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));

            Vector3 prevPos = EvaluateBezier(startPoint, controlPoint, endPoint, prevT);
            Vector3 newPos = EvaluateBezier(startPoint, controlPoint, endPoint, t);

            // Trazado de colisión entre prevPos -> newPos
            Vector3 delta = newPos - prevPos;
            float len = delta.magnitude;
            if (len > 0.0001f)
            {
                if (Physics.SphereCast(prevPos, traceRadius, delta.normalized, out RaycastHit hit, len, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Impacto con salvaje en fase Bezier
                    if (hit.collider.TryGetComponent(out CreatureBehavior wild))
                    {
                        transform.position = hit.point;
                        HandleHitWild(wild);
                        return;
                    }

                    // Impacto con suelo/obstáculo → activar física y dejar rebotar
                    if (IsOnGroundLayer(hit.collider.gameObject.layer))
                    {
                        transform.position = hit.point;
                        ActivatePhysics(delta / Mathf.Max(Time.deltaTime, 0.001f));
                        return;
                    }
                }
            }

            // Avanzar sin colisión
            transform.position = newPos;

            // Fin del arco → pasar a física con velocidad residual
            if (t >= 0.999f)
            {
                Vector3 vel = (newPos - prevPos) / Mathf.Max(Time.deltaTime, 0.001f);
                ActivatePhysics(vel);
            }
        }
    }

    private void ActivatePhysics(Vector3 initialVelocity)
    {
        physicsActivated = true;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = initialVelocity;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (hasHitPokemon) return;

        if (col.collider.TryGetComponent(out CreatureBehavior wild))
        {
            HandleHitWild(wild);
            return;
        }

        // Toca suelo
        if (IsOnGroundLayer(col.gameObject.layer))
        {
            hasLanded = true;
            Destroy(gameObject, 3f);
        }
    }

    // ----------------- Captura -----------------
    private void HandleHitWild(CreatureBehavior wild)
    {
        hasHitPokemon = true;
        targetPokemon = wild;
        targetObject = wild.gameObject;

        // Congelar física para la animación de sacudidas
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        StartCoroutine(CoCaptureFlow());
    }

    private IEnumerator CoCaptureFlow()
    {
        if (targetObject) targetObject.SetActive(false);

        // Posar la bola sobre el SUELO debajo del objetivo (raycast robusto)
        Vector3 basePos;
        if (!TryGetGroundUnder(targetPokemon.transform.position, out basePos))
        {
            // Fallback: usar el punto actual con offset
            basePos = transform.position;
            basePos.y += Mathf.Max(0.1f, groundRestOffset);
        }
        transform.position = basePos;

        // Probabilidad básica (MVP)
        var inst = targetPokemon.GetPokemonInstance();
        if (inst == null || inst.species == null)
        {
            Debug.LogError("[Ball] Instancia o especie nula en captura.");
            yield break;
        }

        float baseRate = Mathf.Clamp01(inst.species.catchRate);
        float mult = pokeballData ? Mathf.Max(0f, pokeballData.catchMultiplier) : 1f;
        float chance = Mathf.Clamp01(baseRate * mult);
        bool success = (Random.value < criticalCaptureChance) || (Random.value <= chance);

        // Sacudidas
        int shakes = success ? 3 : Random.Range(1, 3);
        for (int i = 0; i < shakes; i++)
        {
            yield return Shake(basePos);
            yield return new WaitForSeconds(0.25f);
        }
        transform.position = basePos;

        if (success)
        {
            PokemonStorageManager.Instance?.CapturePokemon(inst);

            // Refrescar UI
            var selector = Object.FindAnyObjectByType<ItemSelectorUI>();
            if (selector != null)
            {
                selector.RefreshCapturedPokemon();
                selector.UpdateUI();
                selector.gameObject.SendMessage("RefreshBalls", SendMessageOptions.DontRequireReceiver);
            }

            // Cerrar combate si procede
            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureSuccess();

            Destroy(targetObject);
            Destroy(gameObject, 0.5f); // éxito: permite ver el cierre de animación
        }
        else
        {
            // Fallo: reactivar salvaje y reanudar su comportamiento
            if (targetObject)
            {
                targetObject.SetActive(true);

                var beh = targetObject.GetComponent<CreatureBehavior>();
                if (beh != null)
                {
                    if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                    {
                        // Seguimos en combate: mantener desactivada la IA de mundo
                        beh.SetCombatMode(true);
                    }
                    else
                    {
                        // Fuera de combate: retomar IA de mundo
                        beh.SetCombatMode(false);

                        // "Kick" del componente para reiniciar corutinas si se pararon al desactivar el GO
                        beh.enabled = false;
                        beh.enabled = true;
                    }
                }
            }

            // Notificar al combate para consumir turno si procede
            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureFailed();

            // Bola desaparece INMEDIATAMENTE en fallo
            Destroy(gameObject);
        }
    }

    private IEnumerator Shake(Vector3 basePosition)
    {
        float dur = 0.3f;
        float amp = 0.15f;
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float off = Mathf.Sin(e * 40f) * amp;
            transform.position = basePosition + new Vector3(off, 0, 0);
            yield return null;
        }
        transform.position = basePosition;
    }

    // ---------------- Utilidades ----------------
    private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private bool IsOnGroundLayer(int layer)
    {
        // Si no asignaste groundLayer en el inspector, admite TODO.
        int mask = (groundLayer.value != 0) ? groundLayer.value : ~0;
        return (mask & (1 << layer)) != 0;
    }

    /// Busca suelo bajo "center" con varios fallbacks.
    /// Devuelve el punto de apoyo + offset vertical para posar la bola.
    private bool TryGetGroundUnder(Vector3 center, out Vector3 result)
    {
        int mask = (groundLayer.value != 0) ? groundLayer.value : ~0;

        // 1) Raycast largo desde arriba
        Vector3 origin = center + Vector3.up * 50f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore))
        {
            result = hit.point + Vector3.up * Mathf.Max(0f, groundRestOffset);
            return true;
        }

        // 2) SphereCast corto desde la posición actual de la bola
        Vector3 start = transform.position + Vector3.up * 1f;
        if (Physics.SphereCast(start, 0.2f, Vector3.down, out hit, 3f, mask, QueryTriggerInteraction.Ignore))
        {
            result = hit.point + Vector3.up * Mathf.Max(0f, groundRestOffset);
            return true;
        }

        // 3) Raycast global sin máscara, por si la capa de suelo no coincide
        if (Physics.Raycast(origin, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            result = hit.point + Vector3.up * Mathf.Max(0f, groundRestOffset);
            return true;
        }

        // 4) Fallback final: mantener altura actual
        result = transform.position;
        return false;
    }
}
