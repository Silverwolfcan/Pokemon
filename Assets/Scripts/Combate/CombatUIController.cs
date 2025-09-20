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

    [Header("Log de combate")]
    [SerializeField] private GameObject combatLogRoot;   // Asignar el root del panel de logs
    [SerializeField] private bool autoFindCombatLog = true;

    private PlayerController playerController;
    private bool captureMode = false;
    private bool turnBound = false;

    // Estado log↔bolsa
    private bool logWasActiveBeforeBag = true;
    private bool bagOpen = false;

    private void OnEnable()
    {
        if (autoFindCombatLog && combatLogRoot == null)
        {
            var inst = CombatLogPanel.Instance;
            if (inst != null) combatLogRoot = inst.gameObject;
        }

        playerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        BindButtons();
        BindTurn(ResolveTurn());
        captureMode = false;
        bagOpen = false;
        ShowMain();
        SetCursorUI();
        // El log visible por defecto
        SetLogVisible(true);
    }

    private void OnDisable()
    {
        UnbindTurn(ResolveTurn());
        captureMode = false;
        turnBound = false;
        bagOpen = false;
    }

    private void Update()
    {
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

    // --- callbacks de turno ---
    private void OnPlayerTurnStart()
    {
        captureMode = false;
        playerController?.EnableControls(false);
        ShowMain();
        SetCursorUI();
        if (!bagOpen) SetLogVisible(true);
    }

    private void OnEnemyTurnStart()
    {
        captureMode = false;
        ShowNone();
        // Cursor permanece libre durante el combate
        SetCursorUI();
        if (!bagOpen) SetLogVisible(true);
    }

    // --- paneles ---
    private void ShowMain()
    {
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(false);
        if (bagPanel) bagPanel.Close();
        if (panelMoves) panelMoves.SetActive(false);
        if (panelMain) panelMain.SetActive(true);
        if (!bagOpen) SetLogVisible(true);
    }

    private void ShowNone()
    {
        if (panelMain) panelMain.SetActive(false);
        if (panelMoves) panelMoves.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(false);
        // El log lo gestiona quien abre/cierra bolsa
    }

    private void OpenMoves()
    {
        if (!moveGrid) { Debug.LogWarning("[UI] Falta MoveGridUI."); return; }
        moveGrid.SetMode(MoveGridUI.GridMode.Combat);

        var all = FindObjectsByType<CombatantController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in all) if (c && c.IsPlayer) { moveGrid.SetPokemon(c.Model); break; }

        if (panelMain) panelMain.SetActive(false);
        if (panelMoves) panelMoves.SetActive(true);
        SetCursorUI();
        if (!bagOpen) SetLogVisible(true);
    }

    private void OnMoveChosen(int modelIndex)
    {
        var turn = ResolveTurn();
        if (turn != null) turn.QueueMove(modelIndex);
        ShowNone();
        // Cursor sigue libre en combate
        SetCursorUI();
    }

    private void OpenTeam()
    {
        if (panelMain) panelMain.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(true);
        SetCursorUI();
        if (!bagOpen) SetLogVisible(true);
    }

    private void EnterCaptureMode()
    {
        captureMode = true;
        ShowNone();
        playerController?.EnableControls(true);
        // Mantener cursor libre incluso en modo captura
        SetCursorUI();
        if (!bagOpen) SetLogVisible(true);
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

        bagOpen = true;
        // Guardar estado y ocultar log
        logWasActiveBeforeBag = combatLogRoot ? combatLogRoot.activeSelf : true;
        SetLogVisible(false);

        if (panelMain) panelMain.SetActive(false);
        if (bagPanelRoot) bagPanelRoot.SetActive(true);
        bagPanel.gameObject.SetActive(true);

        bagPanel.OpenForCombat(
            onUse: (item, target) =>
            {
                var turn = ResolveTurn();
                if (turn != null) turn.QueueUseItem(item, target);
                bagPanel.Close();
                bagOpen = false;
                RestoreLogAfterBag();
                ShowNone();
                // Mantener cursor libre tras acción
                SetCursorUI();
            },
            onClose: () =>
            {
                bagPanel.Close();
                bagOpen = false;
                RestoreLogAfterBag();
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

        bool wasBag = bagOpen || (bagPanelRoot && bagPanelRoot.activeInHierarchy);

        if (panelMoves) panelMoves.SetActive(false);
        if (teamPanelRoot) teamPanelRoot.SetActive(false);
        if (bagPanel) bagPanel.Close();
        if (bagPanelRoot) bagPanelRoot.SetActive(false);

        bagOpen = false;
        if (wasBag) RestoreLogAfterBag();

        ShowMain();
        SetCursorUI();
    }

    private void SetLogVisible(bool visible)
    {
        if (!combatLogRoot) return;
        if (combatLogRoot.activeSelf != visible) combatLogRoot.SetActive(visible);
    }

    private void RestoreLogAfterBag()
    {
        SetLogVisible(logWasActiveBeforeBag);
    }

    private static void SetCursorUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // wiring botones
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
                turn.QueueRun();
                ShowNone();
                // Mantener cursor libre mientras el encuentro no termine
                SetCursorUI();
            });
        }

        if (moveGrid)
        {
            moveGrid.OnMoveSelected -= OnMoveChosen;
            moveGrid.OnMoveSelected += OnMoveChosen;
        }
    }
}
