using System.Collections;
using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Captura")]
    [SerializeField] private PokeballData pokeballData;
    [SerializeField] private LayerMask groundLayer;

    private Vector3 startPoint, endPoint, controlOffset;
    private float duration = 1f, timer = 0f;
    private bool physicsActivated = false, hasHitPokemon = false, hasLanded = false;

    private CreatureBehavior targetPokemon;
    private GameObject targetObject;
    private Rigidbody rb;
    private float criticalCaptureChance = 0.1f;

    public void Initialize(Vector3 start, Vector3 end, float maxHeight, float maxCurveStrength, PokeballData data)
    {
        startPoint = start; endPoint = end; pokeballData = data;

        float distance = Vector3.Distance(start, end);
        float normalizedDistance = Mathf.InverseLerp(0f, 10f, distance);
        float height = Mathf.Lerp(0f, maxHeight, normalizedDistance);
        float curveStrength = Mathf.Lerp(0f, maxCurveStrength, normalizedDistance);

        Vector3 midPoint = (start + end) * 0.5f;
        midPoint += Vector3.up * height;
        Vector3 direction = (end - start).normalized;
        Vector3 side = Vector3.Cross(direction, Vector3.up);
        midPoint -= side * curveStrength;

        controlOffset = midPoint - (start + end) * 0.5f;

        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null) { trail.Clear(); trail.emitting = true; }

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; rb.isKinematic = true;

        groundLayer = LayerMask.GetMask("Ground");
        Destroy(gameObject, 10f);
    }

    private void Update()
    {
        if (hasLanded || hasHitPokemon || physicsActivated) return;

        timer += Time.deltaTime; float t = timer / duration;
        Vector3 midpoint = (startPoint + endPoint) * 0.5f + controlOffset;
        Vector3 pos = Mathf.Pow(1 - t, 2) * startPoint + 2 * (1 - t) * t * midpoint + Mathf.Pow(t, 2) * endPoint;
        transform.position = pos;

        if (!physicsActivated && Vector3.Distance(transform.position, endPoint) < 0.5f)
        {
            rb.useGravity = true; rb.isKinematic = false; rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Vector3 nextPos = Mathf.Pow(1 - (t + 0.01f), 2) * startPoint + 2 * (1 - (t + 0.01f)) * (t + 0.01f) * midpoint + Mathf.Pow(t + 0.01f, 2) * endPoint;
            rb.linearVelocity = (nextPos - transform.position) / Time.deltaTime;
            physicsActivated = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHitPokemon || hasLanded) return;

        if (collision.collider.TryGetComponent(out CreatureBehavior wild))
        {
            hasHitPokemon = true; targetPokemon = wild; targetObject = wild.gameObject;

            wild.enabled = false; targetObject.SetActive(false);
            if (TryGetComponent(out TrailRenderer trail)) trail.emitting = false;

            if (rb != null) { rb.isKinematic = true; rb.constraints = RigidbodyConstraints.FreezeAll; rb.collisionDetectionMode = CollisionDetectionMode.Discrete; }
            if (TryGetComponent(out Collider col)) col.enabled = false;

            Vector3 impactPoint = collision.contacts[0].point;
            StartCoroutine(JumpToGround(impactPoint));
        }
    }

    private IEnumerator JumpToGround(Vector3 fromPosition)
    {
        if (!Physics.Raycast(fromPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f, groundLayer))
        { Debug.LogWarning("No se encontró el suelo bajo la Pokéball."); yield break; }

        Vector3 targetPosition = hit.point; Vector3 startPosition = transform.position;
        float height = 0.5f, duration = 0.5f, timer = 0f;
        while (timer < duration)
        {
            float t = timer / duration;
            float yOffset = Mathf.Sin(t * Mathf.PI) * height;
            transform.position = Vector3.Lerp(startPosition, targetPosition, t) + Vector3.up * yOffset;
            timer += Time.deltaTime; yield return null;
        }

        float visualOffsetY = 0f; Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) visualOffsetY = rend.bounds.extents.y;
        transform.position = targetPosition + Vector3.up * visualOffsetY;

        yield return StartCoroutine(CaptureSequence());
    }

    private IEnumerator CaptureSequence()
    {
        if (targetPokemon == null || targetPokemon.GetPokemonInstance()?.species == null)
        { Debug.LogError("❌ species es null al iniciar la secuencia de captura."); yield break; }

        float catchRate = targetPokemon.GetPokemonInstance().species.catchRate;
        float multiplier = pokeballData?.catchMultiplier ?? 1f;

        float chance = Mathf.Clamp01(catchRate * multiplier);
        float roll = Random.Range(0f, 1f);
        bool isCaptured = roll <= chance;

        bool isCriticalCapture = isCaptured && (
            (pokeballData != null && Mathf.Approximately(pokeballData.catchMultiplier, 255f)) ||
            Random.value < 0.1f
        );

        int shakeCount = isCriticalCapture ? 1 : (isCaptured ? 3 : Random.Range(1, 3));

        Vector3 basePosition = transform.position;
        for (int i = 0; i < shakeCount; i++) { yield return ShakeAnimation(basePosition); yield return new WaitForSeconds(0.4f); }
        transform.position = basePosition;

        if (isCaptured)
        {
            var inst = targetPokemon.GetPokemonInstance();
            Debug.Log("Se ha capturado a " + inst.species.pokemonName);
            PokemonStorageManager.Instance.CapturePokemon(inst);
            Destroy(targetObject); Destroy(gameObject, 0.5f);
        }
        else
        {
            targetObject.SetActive(true); targetPokemon.enabled = true; Destroy(gameObject);
        }
    }

    private IEnumerator ShakeAnimation(Vector3 basePosition)
    {
        float shakeAmount = 0.15f; float duration = 0.3f; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 40f) * shakeAmount;
            transform.position = basePosition + new Vector3(offset, 0, 0);
            yield return null;
        }
        transform.position = basePosition;
    }
}
