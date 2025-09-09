using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatantHUD : MonoBehaviour
{
    public enum AnchorMode { RendererBoundsTop, AnchorTransform, FixedOffset }

    [Header("UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;

    [Header("Estado")]
    [SerializeField] private UIAssetsRegistry assetsRegistry;
    [SerializeField] private Image primaryStatusIcon;

    [Header("Anclaje / Seguimiento")]
    [SerializeField] private AnchorMode anchorMode = AnchorMode.RendererBoundsTop;
    [SerializeField] private Transform anchorOverride;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 0.25f, 0);
    [SerializeField] private float boundsExtraHeight = 0.15f;
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followLerp = 20f;

    [Header("Billboard")]
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool uprightBillboard = true;
    [SerializeField] private float rotateLerp = 30f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;

    // Runtime
    private Transform target;                  // anclaje en mundo
    private PokemonInstance model;             // modelo actual
    private Camera cam;
    private Canvas myCanvas;
    private StatusContainer status;
    private int maxHPCached = -1;

    // Exposición para TurnController
    public Transform Anchor => target;

    // -------- API pública --------
    public void Bind(Transform targetTransform, PokemonInstance pokemon, Camera cameraOverride = null)
    {
        target = targetTransform;
        model = pokemon;
        cam = cameraOverride != null ? cameraOverride : Camera.main;

        if (!TryGetComponent(out myCanvas)) myCanvas = GetComponentInParent<Canvas>();
        if (myCanvas != null && myCanvas.renderMode == RenderMode.WorldSpace && myCanvas.worldCamera == null)
            myCanvas.worldCamera = cam;

        status = target ? target.GetComponent<StatusContainer>() : null;

        CacheStaticData();
        RefreshAll();

        var pos = ComputeAnchorPosition() + worldOffset;
        transform.position = pos;
        ApplyBillboard(true);
    }

    // Rebind forzado desde TurnController tras un switch
    public void ForceRebind(PokemonInstance newModel)
    {
        model = newModel;
        status = target ? target.GetComponent<StatusContainer>() : null;
        CacheStaticData();
        RefreshAll();
    }

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }

        // Seguir anclaje
        var targetPos = ComputeAnchorPosition() + worldOffset;
        transform.position = smoothFollow
            ? Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * followLerp)
            : targetPos;

        ApplyBillboard(false);

        // HP continuo
        RefreshHPOnly();

        // Estado primario
        if (status != null) ApplyPrimaryIcon(status.Primary);
    }

    private void CacheStaticData()
    {
        if (model == null) return;
        maxHPCached = Mathf.Max(1, model.stats.MaxHP);
        if (nameText) nameText.text = model.DisplayName;
        if (levelText) levelText.text = "Nv. " + model.level.ToString();
        if (hpSlider) { hpSlider.minValue = 0; hpSlider.maxValue = maxHPCached; }
    }

    private void RefreshAll()
    {
        RefreshHPOnly();
        ApplyPrimaryIcon(status ? status.Primary : StatusService.PrimaryStatus.None);
    }

    private void RefreshHPOnly()
    {
        if (!hpSlider || model == null) return;
        hpSlider.value = Mathf.Clamp(model.currentHP, 0, maxHPCached <= 0 ? 1 : maxHPCached);
    }

    private void ApplyPrimaryIcon(StatusService.PrimaryStatus s)
    {
        if (!primaryStatusIcon) return;

        if (s == StatusService.PrimaryStatus.None)
        {
            primaryStatusIcon.enabled = false;
            primaryStatusIcon.gameObject.SetActive(false);
            return;
        }

        var spr = assetsRegistry ? assetsRegistry.GetStatusSprite(s) : null;
        var col = assetsRegistry ? assetsRegistry.GetStatusColor(s) : Color.white;

        primaryStatusIcon.gameObject.SetActive(true);
        primaryStatusIcon.enabled = spr != null;
        primaryStatusIcon.sprite = spr;
        primaryStatusIcon.color = col;
    }

    private Vector3 ComputeAnchorPosition()
    {
        if (anchorMode == AnchorMode.AnchorTransform && anchorOverride) return anchorOverride.position;

        if (anchorMode == AnchorMode.RendererBoundsTop || !anchorOverride)
        {
            if (TryGetCombinedBounds(target, out Bounds b))
                return new Vector3(b.center.x, b.max.y + boundsExtraHeight, b.center.z);
        }
        return target.position + Vector3.up * (boundsExtraHeight > 0f ? boundsExtraHeight : 0.15f);
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        var rends = root ? root.GetComponentsInChildren<Renderer>(true) : null;
        if (rends == null || rends.Length == 0) { bounds = default; return false; }
        bool any = false; bounds = new Bounds(root.position, Vector3.zero);
        foreach (var r in rends)
        {
            if (!r.enabled) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    private void ApplyBillboard(bool instant)
    {
        if (!billboardToCamera || !cam) return;
        Quaternion targetRot;
        if (uprightBillboard)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f; if (toCam.sqrMagnitude < 1e-6f) return;
            targetRot = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }
        else
        {
            Vector3 dir = (transform.position - cam.transform.position);
            if (dir.sqrMagnitude < 1e-6f) return;
            targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        transform.rotation = instant
            ? targetRot
            : Quaternion.Slerp(transform.rotation, targetRot, Time.unscaledDeltaTime * rotateLerp);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        if (target) { Gizmos.color = Color.yellow; Gizmos.DrawLine(transform.position, target.position); }
    }
}
