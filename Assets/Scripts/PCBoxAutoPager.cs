using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class PCBoxAutoPager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum Direction { Prev = -1, Next = +1 }

    [Header("Config")]
    [Min(0.1f)] public float delaySeconds = 0.35f;   // retardo entre páginas mientras mantienes el ratón
    public Direction direction = Direction.Next;

    [Header("Refs")]
    public PokemonTeamPanel teamPanel;               // controlador del panel de equipo/PC

    private bool pointerOver;
    private Coroutine loop;

    // Requisitos: DragDropController.Instance.IsDraggingAny debe reflejar si hay un drag activo
    private bool IsDragging()
    {
        return DragDropController.Instance != null && DragDropController.Instance.IsDraggingAny;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
        if (loop == null) loop = StartCoroutine(HoverLoop());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    private IEnumerator HoverLoop()
    {
        var wait = new WaitForSecondsRealtime(delaySeconds);
        while (pointerOver)
        {
            if (IsDragging() && teamPanel != null)
            {
                if (direction == Direction.Prev) teamPanel.PrevBox();
                else teamPanel.NextBox();
                yield return wait; // repite mientras sigas encima con drag activo
            }
            else
            {
                yield return null; // espera al siguiente frame
            }
        }
        loop = null;
    }

    private void OnDisable()
    {
        pointerOver = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    // Asignaciones en el Inspector:
    // - teamPanel: arrastra el GameObject con PokemonTeamPanel.
    // - delaySeconds: 0.35–0.5 recomendado.
    // - direction: Prev en el botón izquierdo, Next en el derecho.
}
