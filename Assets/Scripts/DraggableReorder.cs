using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Componente universal para arrastrar y reordenar elementos UI dentro de un LayoutGroup.
/// Al soltar, mantiene el orden visual y dispara el evento OnReordered.
/// Limita el arrastre solo en eje Y y dentro de los bordes del contenedor original.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableReorder : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>
    /// Índice original y nuevo tras reordenar
    /// </summary>
    public event Action<int, int> OnReordered;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalIndex;
    private GameObject placeholder;
    private float originalWorldX;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Buscar el Canvas contenedor (puede ser null si no está en uno)
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogWarning($"[DraggableReorder] No se encontró Canvas padre en {gameObject.name}");

        // Obtener o añadir CanvasGroup sin duplicados
        if (!TryGetComponent<CanvasGroup>(out canvasGroup))
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Guardamos padre e índice
        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();
        // Guardamos posición X para restringir horizontalmente
        originalWorldX = rectTransform.position.x;

        // Creamos placeholder para reservar espacio
        placeholder = new GameObject("placeholder");
        var le = placeholder.AddComponent<LayoutElement>();
        le.preferredWidth = rectTransform.rect.width;
        le.preferredHeight = rectTransform.rect.height;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        placeholder.transform.SetParent(originalParent);
        placeholder.transform.SetSiblingIndex(originalIndex);

        // Sacamos el elemento al canvas para poder moverlo libremente
        transform.SetParent(canvas.transform);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Solo eje Y y dentro de los límites del contenedor
        float clampedY = eventData.position.y;
        if (originalParent is RectTransform containerRect)
        {
            Vector3[] corners = new Vector3[4];
            containerRect.GetWorldCorners(corners);
            float minY = corners[0].y;
            float maxY = corners[1].y;
            clampedY = Mathf.Clamp(clampedY, minY, maxY);
        }
        rectTransform.position = new Vector3(originalWorldX, clampedY, rectTransform.position.z);

        // Actualizar placeholder según Y
        int newIndex = originalParent.childCount;
        for (int i = 0; i < originalParent.childCount; i++)
        {
            var child = originalParent.GetChild(i);
            if (rectTransform.position.y > child.position.y)
            {
                newIndex = i;
                if (placeholder.transform.GetSiblingIndex() < newIndex)
                    newIndex--;
                break;
            }
        }
        placeholder.transform.SetSiblingIndex(newIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Calculamos índice final
        int newIndex = placeholder.transform.GetSiblingIndex();

        // Volvemos a insertar en el contenedor original
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(newIndex);
        canvasGroup.blocksRaycasts = true;

        // Eliminamos placeholder
        Destroy(placeholder);

        // Notificamos el reordenamiento
        OnReordered?.Invoke(originalIndex, newIndex);
    }
}
