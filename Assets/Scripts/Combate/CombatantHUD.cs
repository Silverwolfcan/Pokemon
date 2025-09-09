using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatantHUD : MonoBehaviour
{
    public enum AnchorMode { RendererBoundsTop, AnchorTransform, FixedOffset }

    [Header("UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFill;              // ← asigna el Fill del slider
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

    [Header("HP Colors")]
    [SerializeField, Range(0f, 1f)] private float yellowThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float redThreshold = 0.2f;
    [SerializeField] private Color colorGreen = new Color32(0x4C, 0xC2, 0x4C, 255);
    [SerializeField] private Color colorYellow = new Color32(0xFF, 0xC1, 0x2B, 255);
    [SerializeField] private Color colorRed = new Color32(0xE5, 0x3B, 0x3B, 255);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = false;

    // Runtime
    private Transform target;
    private PokemonInstance model;
    private CombatantController owner;
    private Camera cam;
    private Canvas myCanvas;
    private StatusContainer status;
    private int maxHPCached = -1;
    private float lastHpRatio = -1f;

    public Transform Anchor => target;

    public void Bind(Transform targetTransform, PokemonInstance pokemon, Camera cameraOverride = null)
    {
        target = targetTransform;
        owner = target ? target.GetComponentInParent<CombatantController>() : null;
        model = pokemon != null ? pokemon : (owner != null ? owner.Model : null);
        cam = cameraOverride != null ? cameraOverride : Camera.main;

        if (!TryGetComponent(out myCanvas)) myCanvas = GetComponentInParent<Canvas>();
        if (myCanvas != null && myCanvas.renderMode == RenderMode.WorldSpace && myCanvas.worldCamera == null)
            myCanvas.worldCamera = cam;

        status = target ? target.GetComponent<StatusContainer>() : null;

        if (!hpFill && hpSlider && hpSlider.fillRect)
            hpFill = hpSlider.fillRect.GetComponent<Image>();

        CacheStaticData();
        RefreshAll();

        var pos = ComputeAnchorPosition() + worldOffset;
        transform.position = pos;
        ApplyBillboard(true);
    }

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

        var currentOwner = target.GetComponentInParent<CombatantController>();
        if (currentOwner != owner) owner = currentOwner;

        var currentModel = owner != null ? owner.Model : model;
        if (!ReferenceEquals(model, currentModel) && currentModel != null)
        {
            model = currentModel;
            status = target.GetComponent<StatusContainer>();
            CacheStaticData();
            RefreshAll();
        }

        var targetPos = ComputeAnchorPosition() + worldOffset;
        transform.position = smoothFollow
            ? Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * followLerp)
            : targetPos;

        ApplyBillboard(false);

        RefreshHPOnly();
        if (status != null) ApplyPrimaryIcon(status.Primary);
    }

    private void CacheStaticData()
    {
        if (model == null) return;
        maxHPCached = Mathf.Max(1, model.stats.MaxHP);
        if (nameText) nameText.text = model.DisplayName;
        if (levelText) levelText.text = "Nv. " + model.level.ToString();
        if (hpSlider) { hpSlider.minValue = 0; hpSlider.maxValue = maxHPCached; }
        lastHpRatio = -1f; // fuerza recolor inicial
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

        // Color por ratio
        if (hpFill)
        {
            float ratio = maxHPCached > 0 ? (model.currentHP / (float)maxHPCached) : 0f;
            if (!Mathf.Approximately(ratio, lastHpRatio))
            {
                hpFill.color = (ratio <= redThreshold) ? colorRed
                             : (ratio <= yellowThreshold) ? colorYellow
                             : colorGreen;
                lastHpRatio = ratio;
            }
        }
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
