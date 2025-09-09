// UI/CombatantHUD.cs
using System;
using System.Linq;
using System.Reflection;
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
    [SerializeField] private Image primaryStatusIcon; // Se activa solo cuando hay estado

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
    private Transform target;
    private object model;
    private Camera cam;
    private Canvas myCanvas;
    private StatusContainer status;

    // Reflection cache
    private FieldInfo fi_currentHP, fi_level, fi_species, fi_stats;
    private PropertyInfo pi_currentHP, pi_level, pi_species, pi_stats;

    private int maxHPCached = -1;
    private string displayNameCached = "?";
    private int levelCached = 1;

    // ---- API pública ----
    public void Bind(Transform targetTransform, object pokemonInstance, Camera cameraOverride = null)
    {
        target = targetTransform;
        model = pokemonInstance;
        cam = cameraOverride != null ? cameraOverride : Camera.main;

        if (!TryGetComponent(out myCanvas)) myCanvas = GetComponentInParent<Canvas>();
        if (myCanvas != null && myCanvas.renderMode == RenderMode.WorldSpace && myCanvas.worldCamera == null)
            myCanvas.worldCamera = cam;

        if (target == null || model == null)
        {
            Debug.LogError("[CombatantHUD] Bind inválido (target/model null).");
            enabled = false;
            return;
        }

        status = target.GetComponent<StatusContainer>();
        if (status != null)
        {
            status.OnPrimaryChanged += HandlePrimaryChanged;
        }

        CacheMembers();
        CacheStaticData();
        RefreshImmediate();

        // Inicial de estado
        if (status != null) HandlePrimaryChanged(status.Primary);
        else HandlePrimaryChanged(StatusService.PrimaryStatus.None);

        // Posición inicial
        Vector3 anchorPos = ComputeAnchorPosition();
        transform.position = anchorPos + worldOffset;
        ApplyBillboard(true);
    }

    private void OnDestroy()
    {
        if (status != null)
        {
            status.OnPrimaryChanged -= HandlePrimaryChanged;
        }
    }

    // ---- Ciclo ----
    private void LateUpdate()
    {
        if (target == null || model == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = ComputeAnchorPosition() + worldOffset;
        if (smoothFollow)
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * followLerp);
        else
            transform.position = targetPos;

        ApplyBillboard(false);
        RefreshHPOnly();
    }

    private void ApplyBillboard(bool instant)
    {
        if (!billboardToCamera || cam == null) return;

        Quaternion targetRot;
        if (uprightBillboard)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-6f) return;
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

    // ---- Anclaje ----
    private Vector3 ComputeAnchorPosition()
    {
        if (anchorMode == AnchorMode.AnchorTransform && anchorOverride != null)
            return anchorOverride.position;

        if (anchorMode == AnchorMode.RendererBoundsTop || anchorOverride == null)
        {
            if (TryGetCombinedBounds(target, out Bounds b))
                return new Vector3(b.center.x, b.max.y + boundsExtraHeight, b.center.z);
        }

        return target.position + Vector3.up * (boundsExtraHeight > 0f ? boundsExtraHeight : 0.15f);
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0)
        {
            bounds = default;
            return false;
        }

        var valid = rends.Where(r => r.enabled && r.bounds.size.sqrMagnitude > 0f).ToArray();
        if (valid.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = valid[0].bounds;
        for (int i = 1; i < valid.Length; i++)
            bounds.Encapsulate(valid[i].bounds);

        return true;
    }

    // ---- UI refresh ----
    private void RefreshImmediate()
    {
        if (nameText) nameText.text = displayNameCached;
        if (levelText) levelText.text = "Nv. " + levelCached.ToString();
        if (hpSlider) { hpSlider.minValue = 0; hpSlider.maxValue = Mathf.Max(1, maxHPCached); }
        RefreshHPOnly();
    }

    private void RefreshHPOnly()
    {
        int hp = GetCurrentHP();
        if (hpSlider) hpSlider.value = Mathf.Clamp(hp, 0, Mathf.Max(1, maxHPCached));
    }

    // ---- Estado UI ----
    private void HandlePrimaryChanged(StatusService.PrimaryStatus s)
    {
        if (primaryStatusIcon == null)
            return;

        if (s == StatusService.PrimaryStatus.None)
        {
            // Sin estado: ocultar icono y opcionalmente desactivar su GO
            primaryStatusIcon.enabled = false;
            primaryStatusIcon.gameObject.SetActive(false);
            return;
        }

        // Con estado: activar y asignar sprite/color desde el registry
        Sprite spr = assetsRegistry != null ? assetsRegistry.GetStatusSprite(s) : null;
        Color col = assetsRegistry != null ? assetsRegistry.GetStatusColor(s) : Color.white;

        primaryStatusIcon.gameObject.SetActive(true);
        primaryStatusIcon.enabled = spr != null;
        primaryStatusIcon.sprite = spr;
        primaryStatusIcon.color = col;
    }

    // ---- Reflection base de modelo ----
    private void CacheMembers()
    {
        var t = model.GetType();
        fi_currentHP = t.GetField("currentHP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        pi_currentHP = t.GetProperty("currentHP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        fi_level = t.GetField("level", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        pi_level = t.GetProperty("level", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        fi_species = t.GetField("species", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        pi_species = t.GetProperty("species", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        fi_stats = t.GetField("stats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        pi_stats = t.GetProperty("stats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private void CacheStaticData()
    {
        levelCached = SafeGetInt(fi_level, pi_level, model, 1);

        object speciesObj = SafeGetObject(fi_species, pi_species, model);
        if (speciesObj != null)
        {
            var ts = speciesObj.GetType();
            var fiPN = ts.GetField("pokemonName") ?? ts.GetField("name");
            var piPN = ts.GetProperty("pokemonName") ?? ts.GetProperty("name");
            displayNameCached = SafeGetString(fiPN, piPN, speciesObj, "?");
        }

        object statsObj = SafeGetObject(fi_stats, pi_stats, model);
        if (statsObj != null)
        {
            var ts = statsObj.GetType();
            var fiMax = ts.GetField("MaxHP") ?? ts.GetField("maxHP") ?? ts.GetField("HP") ?? ts.GetField("hp");
            var piMax = ts.GetProperty("MaxHP") ?? ts.GetProperty("maxHP") ?? ts.GetProperty("HP") ?? ts.GetProperty("hp");
            maxHPCached = SafeGetInt(fiMax, piMax, statsObj, -1);
        }

        if (maxHPCached <= 0)
        {
            var mi = model.GetType().GetMethod("GetMaxHP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null) maxHPCached = Convert.ToInt32(mi.Invoke(model, null));
        }
        if (maxHPCached <= 0) maxHPCached = Mathf.Max(1, GetCurrentHP());
    }

    private int GetCurrentHP()
    {
        return SafeGetInt(fi_currentHP, pi_currentHP, model, 1);
    }

    private static int SafeGetInt(FieldInfo fi, PropertyInfo pi, object obj, int def)
    {
        try
        {
            if (fi != null) return Convert.ToInt32(fi.GetValue(obj));
            if (pi != null) return Convert.ToInt32(pi.GetValue(obj));
        }
        catch { }
        return def;
    }

    private static string SafeGetString(FieldInfo fi, PropertyInfo pi, object obj, string def)
    {
        try
        {
            if (fi != null) return (fi.GetValue(obj) ?? def)?.ToString();
            if (pi != null) return (pi.GetValue(obj) ?? def)?.ToString();
        }
        catch { }
        return def;
    }

    private static object SafeGetObject(FieldInfo fi, PropertyInfo pi, object obj)
    {
        try
        {
            if (fi != null) return fi.GetValue(obj);
            if (pi != null) return pi.GetValue(obj, null);
        }
        catch { }
        return null;
    }

    // ---- Gizmos ----
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}
