using System.Collections;
using UnityEngine;

/// Lógica de Pokéball con física real (Rigidbody + gravedad) desde el lanzamiento.
/// - Calcula una velocidad inicial balística para alcanzar el punto de destino con una altura de arco.
/// - Al impactar con un salvaje, congela la física y ejecuta la secuencia de captura (shakes).
/// - Refresca ItemSelectorUI tras capturar y notifica a CombatService en éxito/fracaso.
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    [Header("Datos")]
    [SerializeField] private PokeballData pokeballData;

    [Header("Parámetros de lanzamiento")]
    [Tooltip("Multiplicador de tiempo de vuelo: >1 = arco más alto (más tiempo), <1 = más directo.")]
    [SerializeField] private float flightTimeScale = 1.0f;
    [Tooltip("Velocidad 'objetivo' para calcular el tiempo de vuelo si el apex no es válido.")]
    [SerializeField] private float throwSpeedHint = 12f;
    [Tooltip("Giro inicial para dar sensación de rotación en vuelo.")]
    [SerializeField] private float spinMagnitude = 16f;

    [Header("Captura")]
    [SerializeField] private LayerMask groundMask = -1;
    [SerializeField] private float destroyDelayOnResult = 0.5f;

    private Rigidbody rb;
    private bool resolved;               // para no procesar capturas dos veces
    private CreatureBehavior targetWild; // objetivo impactado
    private GameObject targetGO;

    // ---------------- API pública ----------------
    /// <param name="start">Punto de lanzamiento</param>
    /// <param name="end">Punto de destino estimado (se calculará la velocidad para llegar)</param>
    /// <param name="arcHeight">Altura extra del apex respecto al punto más alto entre start y end</param>
    /// <param name="curveStrength">No se usa en modo físico (compatibilidad de firma)</param>
    public void Initialize(Vector3 start, Vector3 end, float arcHeight, float curveStrength, PokeballData data)
    {
        pokeballData = data;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        // Por si no está configurada, intentar coger la capa Ground
        if (groundMask.value == 0 || groundMask == -1)
            groundMask = LayerMask.GetMask("Ground");

        // Colocar en el punto inicial y calcular velocidad balística
        transform.position = start;
        Vector3 v0 = ComputeBallisticVelocity(start, end, arcHeight, flightTimeScale, throwSpeedHint);
        rb.linearVelocity = v0;
        rb.angularVelocity = Random.onUnitSphere * spinMagnitude;

        // Orientación inicial opcional (hacia la velocidad)
        if (v0.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(new Vector3(v0.x, 0f, v0.z), Vector3.up);

        // Seguridad por si queda suelta en la escena
        Destroy(gameObject, 10f);
    }

    // ---------------- Física / Colisiones ----------------
    private void OnCollisionEnter(Collision col)
    {
        if (resolved) return;

        // Impacto con salvaje: iniciar captura
        if (col.collider.TryGetComponent(out CreatureBehavior wild))
        {
            targetWild = wild;
            targetGO = wild.gameObject;

            // Congelar física para la animación de shakes
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            StartCoroutine(CoCaptureFlow());
            return;
        }

        // Impacto con suelo: nada especial, dejar rebotar; se autodestruirá más tarde
        // (Si quieres que se quede 'clavada' al tocar suelo, descomenta:)
        // if (((1 << col.gameObject.layer) & groundMask) != 0) Destroy(gameObject, 2f);
    }

    // ---------------- Captura ----------------
    private IEnumerator CoCaptureFlow()
    {
        if (resolved) yield break;
        resolved = true;

        // Ocultar temporalmente al salvaje y colocar la bola a sus "pies"
        if (targetGO) targetGO.SetActive(false);

        float footY = 0f;
        var rend = targetGO ? targetGO.GetComponentInChildren<Renderer>() : null;
        if (rend) footY = rend.bounds.extents.y * 0.5f; // un poco más abajo para que no flote
        transform.position = targetWild.transform.position + Vector3.up * footY;

        // Calcular probabilidad simple (MVP)
        var inst = targetWild.GetPokemonInstance();
        if (inst == null || inst.species == null)
        {
            Debug.LogError("[Ball] Instancia o especie nula en captura.");
            yield break;
        }

        float baseRate = Mathf.Clamp01(inst.species.catchRate);
        float mult = pokeballData ? Mathf.Max(0f, pokeballData.catchMultiplier) : 1f;
        float chance = Mathf.Clamp01(baseRate * mult);
        bool success = Random.value <= chance;

        // Sacudidas simples
        int shakes = success ? 3 : Random.Range(1, 3);
        Vector3 basePos = transform.position;
        for (int i = 0; i < shakes; i++)
        {
            yield return Shake(basePos);
            yield return new WaitForSeconds(0.25f);
        }
        transform.position = basePos;

        if (success)
        {
            // Añadir al almacenamiento
            PokemonStorageManager.Instance?.CapturePokemon(inst);

            // 🔄 Refrescar UI (balls + pokémon)
            var selector = FindAnyObjectByType<ItemSelectorUI>();
            if (selector != null)
            {
                selector.RefreshCapturedPokemon();
                selector.UpdateUI();
                selector.gameObject.SendMessage("RefreshBalls", SendMessageOptions.DontRequireReceiver);
            }

            // Notificar fin de encuentro si estamos en combate
            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureSuccess();

            Destroy(targetGO); // eliminar salvaje del mundo
            Destroy(gameObject, destroyDelayOnResult);
        }
        else
        {
            // Fallo: devolver al salvaje a la escena y notificar al combate
            if (targetGO) targetGO.SetActive(true);

            if (CombatService.Instance != null && CombatService.Instance.IsInEncounter)
                CombatService.Instance.NotifyCaptureFailed();

            Destroy(gameObject, destroyDelayOnResult);
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
    /// Calcula una velocidad inicial para alcanzar 'end' con gravedad, usando un apex por encima del punto más alto
    /// o, si no es viable, un cálculo por tiempo de vuelo aproximado (throwSpeedHint).
    private static Vector3 ComputeBallisticVelocity(Vector3 start, Vector3 end, float arcHeight, float timeScale, float speedHint)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        Vector3 disp = end - start;
        Vector3 dispXZ = new Vector3(disp.x, 0f, disp.z);
        float distXZ = dispXZ.magnitude;

        // Intento 1: método del apex
        float apex = Mathf.Max(start.y, end.y) + Mathf.Max(0.1f, arcHeight);
        float heightUp = Mathf.Max(0.01f, apex - start.y);
        float heightDown = Mathf.Max(0.01f, apex - end.y);

        float vy = Mathf.Sqrt(2f * g * heightUp);
        float tUp = vy / g;
        float tDown = Mathf.Sqrt(2f * heightDown / g);
        float t = (tUp + tDown) * Mathf.Max(0.25f, timeScale);

        if (t > 0.01f && distXZ > 0.001f)
        {
            Vector3 vxz = dispXZ / t;
            return vxz + Vector3.up * vy;
        }

        // Intento 2: cálculo por tiempo de vuelo con pista de velocidad
        float travelTime = Mathf.Max(0.25f, distXZ / Mathf.Max(0.1f, speedHint)) * Mathf.Max(0.25f, timeScale);
        Vector3 v = new Vector3(
            disp.x / travelTime,
            (disp.y + 0.5f * g * travelTime * travelTime) / travelTime,
            disp.z / travelTime
        );
        return v;
    }
}
