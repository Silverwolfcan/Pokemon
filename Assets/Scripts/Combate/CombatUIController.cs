using UnityEngine;
using UnityEngine.UI;

public class CombatUIController : MonoBehaviour
{
    [Header("Main")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private Button btnFight;
    [SerializeField] private Button btnTeam;
    [SerializeField] private Button btnCapture;
    [SerializeField] private Button btnBag;
    [SerializeField] private Button btnRun;

    [Header("Movimientos")]
    [SerializeField] private GameObject panelMoves;
    [SerializeField] private MoveGridUI moveGrid;

    [Header("Panel Equipo")]
    [SerializeField] private GameObject teamPanelRoot;

    [Header("Bag")]
    [SerializeField] private BagPanel bagPanel;
    [SerializeField] private GameObject bagPanelRoot;

    private PlayerController playerController;
    private bool captureMode = false;
    private bool turnBound = false;

    private void OnEnable()
    {
        playerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        BindButtons();
        BindTurn(ResolveTurn());
        captureMode = false;
        ShowMain();
        SetCursorUI();
    }

    private void OnDisable()
    {
        UnbindTurn(ResolveTurn());
        captureMode = false;
        turnBound = false;
    }

    private void Update()
    {
        // Re-vincula si el Encounter acaba de crearse
        if (!turnBound && ResolveTurn() != null) BindTurn(ResolveTurn());

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (captureMode) { ExitCaptureModeToMain(); return; }
            if (IsAnySubmenuOpen()) { CloseAllSubmenusToMain(); return; }
        }
    }

    // --- resolver Turn activo ---
    private TurnController ResolveTurn()
    {
        var cs = CombatService.Instance;
        var t = cs != null ? cs.CurrentTurn : null;
        if (t == null) t = FindAnyObjectByType<TurnController>(FindObjectsInactive.Exclude);
        return t;
    }

    private void BindTurn(TurnController turn)
    {
        if (turn == null || turnBound) return;
        turn.OnPlayerTurnStart -= OnPlayerTurnStart;
        turn.OnEnemyTurnStart -= OnEnemyTurnStart;
        turn.OnPlayerTurnStart += OnPlayerTurnStart;
        turn.OnEnemyTurnStart += OnEnemyTurnStart;
        turnBound = true;
    }
    private void UnbindTurn(TurnController turn)
    {
        if (turn == null) return;
        turn.OnPlayerTurnStart -= OnPlayerTurnStart;
        turn.OnEnemyTurnStart -= OnEnemyTurnStart;
    }

    // --- wiring botones ---
    private void BindButtons()
    {
        if (btnFight) { btnFight.onClick.RemoveAllListeners(); btnFight.onClick.AddListener(OpenMoves); }
        if (btnTeam) { btnTeam.onClick.RemoveAllListeners(); btnTeam.onClick.AddListener(OpenTeam); }
        if (btnCapture) { btnCapture.onClick.RemoveAllListeners(); btnCapture.onClick.AddListener(EnterCaptureMode); }
        if (btnBag) { btnBag.onClick.RemoveAllListeners(); btnBag.onClick.AddListener(OpenBag); }
        if (btnRun)
        {
            btnRun.onClick.RemoveAllListeners();
            btnRun.onClick.AddListener(() =>
            {
                var turn = ResolveTurn();
                if (turn == null) return;
                turn.QueueRun();                // encola huida en el Turn válido
                ShowNone();
                SetCursorGameplay();
            });
        }

        if (moveGrid)
        {
            moveGrid.OnMoveSelected -= OnMoveChosen;
            moveGrid.OnMoveSelected += OnMoveChosen;
        }
    }

    // --- callbacks de turno ---
    private void OnPlayerTurnStart()
    {
        captureMode = false;
        playerController?.EnableControls(false);
        ShowMain();
        SetCursorUI();
    }

    private void OnEnemyTurnStart()
    {
        captureMode = false;
        ShowNone();
        SetCursorGameplay();
    }

    // --- paneles ---
    private void ShowMain()
    {
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(false);
        if (bagPanel) bagPanel.Close();
        if (panelMoves) panelMoves.SetActive(false);
        if (panelMain) panelMain.SetActive(true);
    }

    private void ShowNone()
    {
        if (panelMain) panelMain.SetActive(false);
        if (panelMoves) panelMoves.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(false);
    }

    private void OpenMoves()
    {
        if (!moveGrid) { Debug.LogWarning("[CombatUI] Falta MoveGridUI."); return; }
        moveGrid.SetMode(MoveGridUI.GridMode.Combat);

        var all = FindObjectsByType<CombatantController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in all) if (c && c.IsPlayer) { moveGrid.SetPokemon(c.Model); break; }

        if (panelMain) panelMain.SetActive(false);
        if (panelMoves) panelMoves.SetActive(true);
        SetCursorUI();
    }

    private void OnMoveChosen(int modelIndex)
    {
        var turn = ResolveTurn();
        if (turn != null) turn.QueueMove(modelIndex); // encola SIEMPRE aquí
        ShowNone();
        SetCursorGameplay();
    }

    private void OpenTeam()
    {
        if (panelMain) panelMain.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(true);
        SetCursorUI();
    }

    private void EnterCaptureMode()
    {
        captureMode = true;
        ShowNone();
        playerController?.EnableControls(true);
        SetCursorGameplay();
    }

    private void ExitCaptureModeToMain()
    {
        captureMode = false;
        playerController?.EnableControls(false);
        ShowMain();
        SetCursorUI();
    }

    private void OpenBag()
    {
        if (!bagPanel) return;

        if (panelMain) panelMain.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(true);
        bagPanel.gameObject.SetActive(true);

        bagPanel.OpenForCombat(
            onUse: (item, target) =>
            {
                var turn = ResolveTurn();
                if (turn != null) turn.QueueUseItem(item, target);
                bagPanel.Close();
                ShowNone();
                SetCursorGameplay();
            },
            onClose: () =>
            {
                bagPanel.Close();
                ShowMain();
                SetCursorUI();
            }
        );
        SetCursorUI();
    }

    // --- helpers ---
    private bool IsAnySubmenuOpen()
    {
        if (captureMode) return true;
        if (panelMoves && panelMoves.activeInHierarchy) return true;
        if (teamPanelRoot && teamPanelRoot.activeInHierarchy) return true;
        if (bagPanel && bagPanel.gameObject.activeInHierarchy) return true;
        if (bagPanelRoot && bagPanelRoot.activeInHierarchy) return true;
        return false;
    }

    private void CloseAllSubmenusToMain()
    {
        if (captureMode) { ExitCaptureModeToMain(); return; }
        if (panelMoves) panelMoves.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanel) bagPanel.Close();
        if (bagPanelRoot) bagPanelRoot.SetActive(false);
        ShowMain();
        SetCursorUI();
    }

    private static void SetCursorUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private static void SetCursorGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
