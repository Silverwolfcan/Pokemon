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
        if (loop == null) loop = StartCoroutine(AutoPagerLoop());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
    }

    private IEnumerator AutoPagerLoop()
    {
        while (true)
        {
            if (!pointerOver) { yield return null; continue; }
            if (!IsDragging()) { yield return null; continue; }

            yield return new WaitForSeconds(delaySeconds);

            if (pointerOver && IsDragging())
            {
                if (direction == Direction.Next) teamPanel?.NextBox();
                else teamPanel?.PrevBox();
            }
        }
    }

    private bool IsDragging()
    {
        var s = ServiceLocator.Get<DraggingStateService>();
        if (s != null) return s.IsDraggingAny;
        return DragDropController.Instance != null && DragDropController.Instance.IsDraggingAny;
    }

    private void OnDisable()
    {
        pointerOver = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }
}
