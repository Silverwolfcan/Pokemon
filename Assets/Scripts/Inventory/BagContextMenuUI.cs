using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BagContextMenuUI : MonoBehaviour
{
    public enum Mode { ItemActions, RemoveHeldItem }

    [Header("Botones")]
    [SerializeField] private Button btnUse;
    [SerializeField] private Button btnGive;
    [SerializeField] private Button btnExit;

    [Header("Textos de los botones (opcional)")]
    [SerializeField] private TMP_Text txtUse;
    [SerializeField] private TMP_Text txtGive;
    [SerializeField] private TMP_Text txtExit;

    // Eventos
    public event Action OnUseClicked;
    public event Action OnGiveClicked;
    public event Action OnExitClicked;
    public event Action OnRemoveClicked;  // para modo RemoveHeldItem

    private Canvas _canvas;
    private RectTransform _rt;
    private Mode _mode = Mode.ItemActions;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _rt = transform as RectTransform;

        // Autoreferenciar textos si no se asignaron
        if (!txtUse && btnUse) txtUse = btnUse.GetComponentInChildren<TMP_Text>(true);
        if (!txtGive && btnGive) txtGive = btnGive.GetComponentInChildren<TMP_Text>(true);
        if (!txtExit && btnExit) txtExit = btnExit.GetComponentInChildren<TMP_Text>(true);

        Hide();

        // Handlers comunes; delegan según _mode
        if (btnUse) { btnUse.onClick.RemoveAllListeners(); btnUse.onClick.AddListener(() => { if (_mode == Mode.ItemActions) OnUseClicked?.Invoke(); else OnRemoveClicked?.Invoke(); }); }
        if (btnGive) { btnGive.onClick.RemoveAllListeners(); btnGive.onClick.AddListener(() => OnGiveClicked?.Invoke()); }
        if (btnExit) { btnExit.onClick.RemoveAllListeners(); btnExit.onClick.AddListener(() => OnExitClicked?.Invoke()); }
    }

    public void Show(RectTransform anchor, Mode mode)
    {
        _mode = mode;
        gameObject.SetActive(true);

        // Config visual por modo
        if (_mode == Mode.ItemActions)
        {
            if (btnUse) btnUse.gameObject.SetActive(true);
            if (btnGive) btnGive.gameObject.SetActive(true);
            if (txtUse) txtUse.text = "Usar";
            if (txtGive) txtGive.text = "Dar";
        }
        else // RemoveHeldItem
        {
            if (btnUse) btnUse.gameObject.SetActive(true);
            if (btnGive) btnGive.gameObject.SetActive(false);
            if (txtUse) txtUse.text = "Quitar";
        }

        // Posición junto al ancla (a la derecha)
        if (_rt && anchor && _canvas)
        {
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 worldRightCenter = (corners[2] + corners[3]) * 0.5f;
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(cam, worldRightCenter),
                cam,
                out var local);

            _rt.anchoredPosition = local + new Vector2(160f, 0f);
        }
    }

    public void Hide() => gameObject.SetActive(false);
}
