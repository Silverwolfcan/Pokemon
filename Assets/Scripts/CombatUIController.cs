using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUIController : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("Panel raíz del menú principal (NO pongas aquí el Canvas).")]
    public GameObject panelRoot;
    public Button btnAttack, btnCapture, btnSwitch, btnItems, btnRun;

    [Header("Moves")]
    public GameObject panelMoves;
    public Button[] moveButtons = new Button[4];
    public TMP_Text[] moveLabels = new TMP_Text[4];

    private TurnController turn;
    private PlayerController playerController;
    private bool captureMode = false;

    private void OnEnable()
    {
        HideAll();
        captureMode = false;
        playerController = FindAnyObjectByType<PlayerController>();
        InvokeRepeating(nameof(TryBind), 0.05f, 0.25f);
    }

    private void OnDisable()
    {
        if (turn != null)
        {
            turn.OnPlayerTurnStart -= OnPlayerTurnStart;
            turn.OnEnemyTurnStart -= OnEnemyTurnStart;
            turn = null;
        }
        CancelInvoke(nameof(TryBind));
        captureMode = false;
    }

    private void Update()
    {
        // ESC cancela modo captura y vuelve al menú (bloquea movimiento y libera cursor)
        if (captureMode && Input.GetKeyDown(KeyCode.Escape))
        {
            captureMode = false;
            playerController?.EnableControls(false);
            ShowMainMenu();
            SetCursorForUI();
        }
    }

    private void TryBind()
    {
        if (turn != null) return;

        var t = FindAnyObjectByType<TurnController>();
        if (t == null) return;

        turn = t;
        turn.OnPlayerTurnStart += OnPlayerTurnStart;
        turn.OnEnemyTurnStart += OnEnemyTurnStart;

        // Botones principales
        btnAttack.onClick.RemoveAllListeners();
        btnAttack.onClick.AddListener(() =>
        {
            ShowMoves(true);
            SetCursorForUI();
        });

        btnCapture.onClick.RemoveAllListeners();
        btnCapture.onClick.AddListener(() =>
        {
            // Entrar en “modo captura”: habilitar controles y ocultar UI
            captureMode = true;
            playerController?.EnableControls(true);
            HideAll();
            SetCursorForGameplay();
        });

        btnRun.onClick.RemoveAllListeners();
        btnRun.onClick.AddListener(() =>
        {
            turn.QueueRun();
            HideAll();
            SetCursorForGameplay();
        });

        btnSwitch.onClick.RemoveAllListeners();
        btnSwitch.onClick.AddListener(() =>
        {
            // Aquí abrirías tu panel de equipo si procede
        });

        btnItems.onClick.RemoveAllListeners();
        btnItems.onClick.AddListener(() =>
        {
            // Aquí abrirías inventario si procede
        });

        // Botones de movimientos
        for (int i = 0; i < moveButtons.Length; i++)
        {
            int idx = i;
            moveButtons[i].onClick.RemoveAllListeners();
            moveButtons[i].onClick.AddListener(() =>
            {
                turn.QueueMove(idx);
                HideAll();
                SetCursorForGameplay();
            });
        }
    }

    private void OnPlayerTurnStart()
    {
        // Turno del jugador: bloquear movimiento y mostrar menú con cursor libre
        captureMode = false;
        playerController?.EnableControls(false);

        PopulateMovesIfPossible();
        ShowMainMenu();
        SetCursorForUI();
    }

    private void OnEnemyTurnStart()
    {
        // Turno enemigo: sin menú ni movimiento; cursor bloqueado
        captureMode = false;
        playerController?.EnableControls(false);
        HideAll();
        SetCursorForGameplay();
    }

    public void ShowMainMenu()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (panelMoves != null) panelMoves.SetActive(false);
    }

    public void HideAll()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (panelMoves != null) panelMoves.SetActive(false);
    }

    private void ShowMoves(bool v)
    {
        if (panelMoves != null) panelMoves.SetActive(v);
        if (panelRoot != null) panelRoot.SetActive(!v);
    }

    private void PopulateMovesIfPossible()
    {
        var all = FindObjectsOfType<CombatantController>();
        CombatantController player = null;
        foreach (var c in all)
        {
            if (c.IsPlayer) { player = c; break; }
        }
        if (player == null || player.Model == null) return;

        var moves = player.Model.Moves;
        for (int i = 0; i < moveLabels.Length; i++)
        {
            if (i < moves.Count && moves[i] != null && moves[i].data != null)
            {
                moveLabels[i].text = moves[i].data.moveName;
                moveButtons[i].interactable = moves[i].currentPP > 0;
            }
            else
            {
                moveLabels[i].text = "-";
                moveButtons[i].interactable = false;
            }
        }
    }

    private static void SetCursorForUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void SetCursorForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
