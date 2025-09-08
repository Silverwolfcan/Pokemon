using System;
using UnityEngine;
using UnityEngine.UI;

public class BagContextMenuUI : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button btnUse;
    [SerializeField] private Button btnGive;
    [SerializeField] private Button btnExit;

    public event Action OnUseClicked;
    public event Action OnGiveClicked;
    public event Action OnExitClicked;

    Canvas _canvas;
    RectTransform _rt;

    void Awake()
    {
        _rt = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        if (btnUse) btnUse.onClick.AddListener(() => OnUseClicked?.Invoke());
        if (btnGive) btnGive.onClick.AddListener(() => OnGiveClicked?.Invoke());
        if (btnExit) btnExit.onClick.AddListener(() => OnExitClicked?.Invoke());
        Hide();
    }

    /// <summary>Muestra el menú anclado a un elemento de la lista.</summary>
    public void Show(RectTransform anchor)
    {
        gameObject.SetActive(true);
        if (_rt && anchor)
        {
            // Colocar junto a la fila (a la derecha)
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 worldPos = (corners[2] + corners[3]) * 0.5f; // centro del borde derecho
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, screen, _canvas.worldCamera, out var local);
            _rt.anchoredPosition = local + new Vector2(160f, 0f);
        }
    }

    public void Hide() => gameObject.SetActive(false);
}
