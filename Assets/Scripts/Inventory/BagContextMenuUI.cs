// UI/BagContextMenuUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BagContextMenuUI : MonoBehaviour
{
    public enum Mode
    {
        ItemActions,         // Usar / Dar / Salir
        RemoveHeldItem,      // Quitar / Salir
        ItemActionsOnlyUse   // Usar / Salir
    }

    [Header("Botones")]
    [SerializeField] private Button btnUse;
    [SerializeField] private Button btnGive;
    [SerializeField] private Button btnExit;

    [Header("Textos opcionales")]
    [SerializeField] private TMP_Text txtUse;
    [SerializeField] private TMP_Text txtGive;

    public event Action OnUseClicked;
    public event Action OnGiveClicked;
    public event Action OnExitClicked;
    public event Action OnRemoveClicked;

    private Canvas _canvas;
    private RectTransform _rt;
    private Mode _currentMode = Mode.ItemActions;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        _rt = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>(true);

        if (!txtUse && btnUse) txtUse = btnUse.GetComponentInChildren<TMP_Text>(true);
        if (!txtGive && btnGive) txtGive = btnGive.GetComponentInChildren<TMP_Text>(true);

        if (btnUse) btnUse.onClick.AddListener(HandleUseOrRemove);
        if (btnGive) btnGive.onClick.AddListener(() => OnGiveClicked?.Invoke());
        if (btnExit) btnExit.onClick.AddListener(() => OnExitClicked?.Invoke());

        Hide();
    }

    // Compatibilidad
    public void Show(RectTransform anchor) => Show(anchor, Mode.ItemActions);

    public void Show(RectTransform anchor, Mode mode)
    {
        _currentMode = mode;
        ConfigureButtonsForMode(mode);

        gameObject.SetActive(true);
        if (_rt && anchor)
        {
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var worldPos = (corners[2] + corners[3]) * 0.5f; // borde derecho

            var cam = _canvas && _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

            var parentRt = _rt.parent as RectTransform;
            if (parentRt && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPoint, cam, out var local))
                _rt.anchoredPosition = local + new Vector2(160f, 0f);
        }
    }

    public void Hide() => gameObject.SetActive(false);

    public bool ContainsScreenPoint(Vector2 screenPos)
    {
        if (!_rt || !_canvas || !IsOpen) return false;
        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(_rt, screenPos, cam);
    }

    private void HandleUseOrRemove()
    {
        if (_currentMode == Mode.RemoveHeldItem) OnRemoveClicked?.Invoke();
        else OnUseClicked?.Invoke();
    }

    private void ConfigureButtonsForMode(Mode mode)
    {
        switch (mode)
        {
            case Mode.ItemActions:
                SetActive(btnUse, true); SetText(txtUse, "Usar");
                SetActive(btnGive, true); SetText(txtGive, "Dar");
                SetActive(btnExit, true);
                break;

            case Mode.RemoveHeldItem:
                SetActive(btnUse, true); SetText(txtUse, "Quitar");
                SetActive(btnGive, false);
                SetActive(btnExit, true);
                break;

            case Mode.ItemActionsOnlyUse:
                SetActive(btnUse, true); SetText(txtUse, "Usar");
                SetActive(btnGive, false);
                SetActive(btnExit, true);
                break;
        }
    }

    private static void SetActive(Behaviour b, bool v) { if (b) b.gameObject.SetActive(v); }
    private static void SetText(TMP_Text t, string s) { if (t) t.text = s; }
}
