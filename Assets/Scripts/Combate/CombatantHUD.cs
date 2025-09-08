using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// HUD world-space del combatiente.
/// Sigue al objetivo, hace billboard y se refresca por eventos:
/// - GameEventBus.PokemonChanged(model) → HP/nombre/nivel
/// - PokemonStatusService.OnStatusChanged(model) → icono de estado
public class CombatantHUD : MonoBehaviour
{
    public enum AnchorMode { RendererBoundsTop, AnchorTransform, FixedOffset }

    [Header("UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image statusIcon;

    [Header("Assets (opcional)")]
    [SerializeField] private UIAssetsRegistry assets;

    [Header("Seguimiento")]
    [SerializeField] private AnchorMode anchorMode = AnchorMode.RendererBoundsTop;
    [SerializeField] private Transform anchorTransform;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 0.25f, 0);
    [SerializeField] private bool smoothFollow = true;
    [SerializeField, Min(0.01f)] private float followLerp = 12f;
    [SerializeField] private bool billboardToCamera = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    // Estado
    private Transform target;
    private PokemonInstance model;
    private Camera cam;
    private Renderer cachedRenderer;

    // ------------ API ------------
    public void Bind(Transform followTarget, PokemonInstance model, Camera cameraForBillboard = null)
    {
        this.target = followTarget;
        this.model = model;
        this.cam = cameraForBillboard != null ? cameraForBillboard : Camera.main;

        if (assets == null) assets = FindAnyObjectByType<UIAssetsRegistry>();
        cachedRenderer = null;
        if (target != null && anchorMode == AnchorMode.RendererBoundsTop)
            cachedRenderer = target.GetComponentInChildren<Renderer>();

        if (nameText) nameText.text = model != null ? model.DisplayName : "-";
        if (levelText) levelText.text = model != null ? model.level.ToString() : "-";

        RefreshHPOnly();
        RefreshStatusIcon(PokemonStatusService.GetStatus(model));

        UpdateWorldPosition(true);
        UpdateBillboard(true);

        SubscribeEvents(true);
        if (verboseLogs) Debug.Log($"[HUD] Bind → {this.model?.DisplayName}");
    }

    public void Unbind()
    {
        SubscribeEvents(false);
        target = null; model = null;
        if (verboseLogs) Debug.Log("[HUD] Unbind");
    }

    // ------------ Ciclo ------------
    private void LateUpdate()
    {
        if (!target) return;
        UpdateWorldPosition(false);
        UpdateBillboard(false);
    }

    private void UpdateWorldPosition(bool instant)
    {
        Vector3 anchorPos = ComputeAnchorPosition() + worldOffset;
        if (smoothFollow && !instant)
            transform.position = Vector3.Lerp(transform.position, anchorPos, Time.unscaledDeltaTime * followLerp);
        else
            transform.position = anchorPos;
    }

    private Vector3 ComputeAnchorPosition()
    {
        if (anchorMode == AnchorMode.AnchorTransform && anchorTransform)
            return anchorTransform.position;

        if (anchorMode == AnchorMode.RendererBoundsTop && cachedRenderer != null)
        {
            var b = cachedRenderer.bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        return target ? target.position : transform.position;
    }

    private void UpdateBillboard(bool instant)
    {
        if (!billboardToCamera) return;
        var c = cam != null ? cam : Camera.main;
        if (!c) return;

        Vector3 fwd = transform.position - c.transform.position;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;

        var look = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        transform.rotation = instant ? look : Quaternion.Slerp(transform.rotation, look, Time.unscaledDeltaTime * 12f);
    }

    // ------------ Eventos ------------
    private void OnEnable() => SubscribeEvents(true);
    private void OnDisable() => SubscribeEvents(false);

    private void SubscribeEvents(bool add)
    {
        if (add)
        {
            GameEventBus.PokemonChanged += OnPokemonChanged;
            PokemonStatusService.OnStatusChanged += OnStatusChanged;
        }
        else
        {
            GameEventBus.PokemonChanged -= OnPokemonChanged;
            PokemonStatusService.OnStatusChanged -= OnStatusChanged;
        }
    }

    private void OnPokemonChanged(PokemonInstance p)
    {
        if (model == null || p == null || !ReferenceEquals(p, model)) return;
        if (verboseLogs) Debug.Log($"[HUD] PokemonChanged → {p.DisplayName}");
        RefreshHPOnly();
    }

    private void OnStatusChanged(PokemonInstance p)
    {
        if (model == null || p == null || !ReferenceEquals(p, model)) return;
        var st = PokemonStatusService.GetStatus(p);
        if (verboseLogs) Debug.Log($"[HUD] StatusChanged → {st}");
        RefreshStatusIcon(st);
    }

    // ------------ Refresh ------------
    private void RefreshHPOnly()
    {
        if (model == null) return;
        try
        {
            int max = Mathf.Max(1, model.stats.MaxHP);
            int cur = Mathf.Clamp(model.currentHP, 0, max);
            if (hpSlider) { hpSlider.maxValue = max; hpSlider.value = cur; }
            if (levelText) levelText.text = model.level.ToString();
            if (nameText) nameText.text = model.DisplayName;
        }
        catch { }
    }

    private void RefreshStatusIcon(PrimaryStatus status)
    {
        if (!statusIcon) return;
        Sprite sp = assets ? assets.GetStatusSprite(status) : null;
        statusIcon.sprite = sp;
        statusIcon.enabled = sp != null;
    }
}
