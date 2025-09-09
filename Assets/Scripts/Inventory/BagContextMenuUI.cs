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
    [SerializeField] private TMP_Text txtUse;   // si no se asigna, se intenta resolver en Awake
    [SerializeField] private TMP_Text txtGive;  // opcional

    public event Action OnUseClicked;
    public event Action OnGiveClicked;
    public event Action OnExitClicked;
    public event Action OnRemoveClicked; // disparado cuando el modo es RemoveHeldItem

    private Canvas _canvas;
    private RectTransform _rt;
    private Mode _currentMode = Mode.ItemActions;

    private void Awake()
    {
        _rt = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();

        if (!txtUse && btnUse) txtUse = btnUse.GetComponentInChildren<TMP_Text>(true);
        if (!txtGive && btnGive) txtGive = btnGive.GetComponentInChildren<TMP_Text>(true);

        if (btnUse) btnUse.onClick.AddListener(HandleUseOrRemove);
        if (btnGive) btnGive.onClick.AddListener(() => OnGiveClicked?.Invoke());
        if (btnExit) btnExit.onClick.AddListener(() => OnExitClicked?.Invoke());

        Hide();
    }

    // Compatibilidad con versiones anteriores
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
            var worldPos = (corners[2] + corners[3]) * 0.5f; // centro del borde derecho

            var cam = _canvas && _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

            var parentRt = _rt.parent as RectTransform;
            if (parentRt && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPoint, cam, out var local))
            {
                _rt.anchoredPosition = local + new Vector2(160f, 0f);
            }
        }
    }

    public void Hide() => gameObject.SetActive(false);

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
                SetActive(btnUse, true);
                SetActive(btnGive, true);
                SetActive(btnExit, true);
                SetText(txtUse, "Usar");
                SetText(txtGive, "Dar");
                break;

            case Mode.RemoveHeldItem:
                SetActive(btnUse, true);
                SetActive(btnGive, false);
                SetActive(btnExit, true);
                SetText(txtUse, "Quitar");
                break;

            case Mode.ItemActionsOnlyUse:
                SetActive(btnUse, true);
                SetActive(btnGive, false);
                SetActive(btnExit, true);
                SetText(txtUse, "Usar");
                break;
        }
    }

    private static void SetActive(Behaviour b, bool v) { if (b) b.gameObject.SetActive(v); }
    private static void SetText(TMP_Text t, string s) { if (t) t.text = s; }
}
