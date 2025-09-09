using System.Collections;
using UnityEngine;

public class CombatFaintSwitchBinder : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject teamPanelRoot;
    [SerializeField] private CombatSwitchPanel switchPanel;
    [SerializeField] private GameObject mainPanelRoot;

    [Header("Opciones")]
    [SerializeField] private bool controlCursor = true;

    private TurnController turn;
    private bool forcedOpen;
    private bool selectionMade;

    private CursorLockMode prevLock;
    private bool prevVisible;

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (turn != null)
            turn.OnPlayerFaintedRequireSwitch -= OnPlayerFaintedRequireSwitch;
        turn = null;

        if (forcedOpen) RestoreCursor();
        forcedOpen = false;
        selectionMade = false;
    }

    private IEnumerator BindWhenReady()
    {
        while (turn == null)
        {
            var cs = CombatService.Instance;
            if (cs != null && cs.CurrentTurn != null) turn = cs.CurrentTurn;
            if (turn != null) break;
            yield return null;
        }

        turn.OnPlayerFaintedRequireSwitch -= OnPlayerFaintedRequireSwitch;
        turn.OnPlayerFaintedRequireSwitch += OnPlayerFaintedRequireSwitch;
    }

    private void Update()
    {
        if (!forcedOpen) return;

        if (teamPanelRoot && !teamPanelRoot.activeSelf && !selectionMade)
            teamPanelRoot.SetActive(true);
        if (mainPanelRoot && mainPanelRoot.activeSelf)
            mainPanelRoot.SetActive(false);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (teamPanelRoot) teamPanelRoot.SetActive(true);
            if (mainPanelRoot) mainPanelRoot.SetActive(false);
        }
    }

    private void OnPlayerFaintedRequireSwitch()
    {
        selectionMade = false;
        forcedOpen = true;

        if (controlCursor) CaptureCursor();

        if (mainPanelRoot) mainPanelRoot.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(true);

        if (switchPanel != null)
        {
            switchPanel.OpenForced(p =>
            {
                // Ahora ejecutamos el cambio inmediato
                turn?.ForceSwitchImmediately(p);

                selectionMade = true;
                forcedOpen = false;
                if (teamPanelRoot) teamPanelRoot.SetActive(false);
                if (controlCursor) RestoreCursor();
            });
        }
    }

    private void CaptureCursor()
    {
        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
    }
}
