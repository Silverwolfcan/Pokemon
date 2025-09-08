using System.Collections;
using UnityEngine;

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
    [SerializeField] private float traceRadius = 0.12f;
    [SerializeField] private float baseArcHeight = 2.0f;
    [SerializeField] private float baseCurveStrength = 0.8f;
    [SerializeField] private float duration = 0.9f;

    [Header("Rebotes")]
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private float bounciness = 0.4f;
    [SerializeField] private float stopSpeed = 0.7f;

    [Header("Animación de captura")]
    [SerializeField] private float shakeAmplitude = 0.12f;
    [SerializeField] private float shakeDuration = 0.15f;

    private Vector3 startPoint, endPoint, controlPoint;
    private float timer = 0f;
    private bool physicsActivated = false;
    private bool hasHitPokemon = false;
    private bool hasLanded = false;

    private CreatureBehavior targetPokemon;
    private GameObject targetObject;
    private Rigidbody rb;

    private float criticalCaptureChance = 0.10f;

    public void Initialize(Vector3 start, Vector3 end, float maxHeight, float maxCurveStrength, PokeballData data)
    {
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

        float h = Mathf.Max(baseArcHeight, maxHeight);
        float curve = Mathf.Max(baseCurveStrength, maxCurveStrength);
        Vector3 mid = (start + end) * 0.5f;
        Vector3 up = Vector3.up * h;
        Vector3 right = Vector3.Cross((end - start).normalized, Vector3.up) * curve;
        controlPoint = mid + up + right;

        timer = 0f;
        physicsActivated = false;
        hasHitPokemon = false;
        hasLanded = false;
    }

    private void Update()
    {
        if (!rb) return;

        if (!physicsActivated)
        {
            float prevT = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));

            Vector3 prevPos = EvaluateBezier(startPoint, controlPoint, endPoint, prevT);
            Vector3 newPos = EvaluateBezier(startPoint, controlPoint, endPoint, t);

            Vector3 delta = newPos - prevPos;
            float len = delta.magnitude;
            if (len > 0.0001f)
            {
                if (Physics.SphereCast(prevPos, traceRadius, delta.normalized, out RaycastHit hit, len, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.TryGetComponent(out CreatureBehavior wild))
                    {
                        transform.position = hit.point;
                        HandleHitWild(wild);
                        return;
                    }

                    if (IsOnGroundLayer(hit.collider.gameObject.layer))
                    {
                        transform.position = hit.point;
                        ActivatePhysics(delta / Mathf.Max(Time.deltaTime, 0.001f));
                        return;
                    }
                }
            }

            transform.position = newPos;

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

        if (IsOnGroundLayer(col.collider.gameObject.layer))
        {
            if (!hasLanded)
            {
                hasLanded = true;
                TryRestOnGround();
            }

            if (rb.linearVelocity.magnitude > stopSpeed && maxBounces > 0)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Abs(rb.linearVelocity.y) * bounciness, rb.linearVelocity.z);
                maxBounces--;
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    private void HandleHitWild(CreatureBehavior wild)
    {
        if (hasHitPokemon) return;
        hasHitPokemon = true;

        targetPokemon = wild;
        targetObject = wild.gameObject;

        targetObject.SetActive(false);
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        StartCoroutine(CaptureSequence());
    }

    private IEnumerator CaptureSequence()
    {
        if (TryGetGroundPoint(out var groundPos))
            transform.position = groundPos;

        Vector3 basePos = transform.position;

        // Obtener la instancia a través del API disponible en CreatureBehavior
        var inst = targetPokemon != null ? targetPokemon.GetPokemonInstance() : null;
        if (inst == null || inst.species == null)
        {
            Debug.LogError("[Ball] Instancia o especie nula en captura.");
            yield break;
        }

        float chance = CaptureService.ComputeCaptureChance(inst, pokeballData);
        bool success = CaptureService.RollCapture(inst, pokeballData, criticalCaptureChance);

        int shakes = CaptureService.ComputeShakeCount(success, chance);
        for (int i = 0; i < shakes; i++)
        {
            yield return Shake(basePos);
            yield return new WaitForSeconds(0.25f);
        }
        transform.position = basePos;

        if (success)
        {
            PokemonStorageManager.Instance?.CapturePokemon(inst);

            var selector = Object.FindAnyObjectByType<ItemSelectorUI>();
            if (selector != null)
            {
                selector.RefreshCapturedPokemon();
                selector.UpdateUI();
                selector.gameObject.SendMessage("RefreshBalls", SendMessageOptions.DontRequireReceiver);
            }

            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureSuccess();

            Destroy(targetObject);
            Destroy(gameObject, 0.5f);
        }
        else
        {
            if (targetObject)
            {
                targetObject.SetActive(true);
                // No llamamos a métodos inexistentes; OnEnable del CreatureBehavior ya reanuda su loop.
            }

            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureFailed();

            Destroy(gameObject);
        }
    }

    private IEnumerator Shake(Vector3 basePos)
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin((t / shakeDuration) * Mathf.PI);
            Vector3 off = new Vector3(Mathf.PerlinNoise(Time.time, 0f) - 0.5f, 0f, Mathf.PerlinNoise(0f, Time.time) - 0.5f).normalized * shakeAmplitude * a;
            transform.position = basePos + off;
            yield return null;
        }
        transform.position = basePos;
    }

    private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private bool IsOnGroundLayer(int layer)
    {
        if (groundLayer == 0) return true;
        return (groundLayer.value & (1 << layer)) != 0;
    }

    private void TryRestOnGround()
    {
        if (TryGetGroundPoint(out var p)) transform.position = p;
    }

    private bool TryGetGroundPoint(out Vector3 result)
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;

        if (groundLayer != 0)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                result = hit.point + Vector3.up * Mathf.Max(0f, groundRestOffset);
                return true;
            }
        }

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hitAny, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            result = hitAny.point + Vector3.up * Mathf.Max(0f, groundRestOffset);
            return true;
        }

        result = transform.position;
        return false;
    }
}
