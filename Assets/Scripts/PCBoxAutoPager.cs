using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class PCBoxAutoPager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum Direction { Prev = -1, Next = +1 }

    [Header("Config")]
    [Min(0.1f)] public float delaySeconds = 1.0f;
    public Direction direction = Direction.Next;

    [Header("Refs")]
    public PanelPokemonTeamController teamPanel; // controlador que expone PrevBox/NextBox

    private bool pointerOver;
    private Coroutine loop;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
        if (loop == null) loop = StartCoroutine(AutoPageLoop());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    private IEnumerator AutoPageLoop()
    {
        while (pointerOver)
        {
            // Solo auto-paginar mientras haya un drag en curso
            if (IsDragging())
            {
                if (teamPanel != null)
                {
                    if (direction == Direction.Prev) teamPanel.PrevBox();
                    else teamPanel.NextBox();
                }
                yield return new WaitForSeconds(delaySeconds);
            }
            else
            {
                yield return null; // esperar siguiente frame si no hay drag
            }
        }
        loop = null;
    }

    private bool IsDragging()
    {
        // Consultar el singleton; evita CS0120
        return DragDropController.Instance != null && DragDropController.Instance.IsDraggingAny;
    }

    private void OnDisable()
    {
        pointerOver = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }
}
