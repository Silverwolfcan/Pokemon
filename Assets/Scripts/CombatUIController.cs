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

    [Tooltip("Botones de los 4 movimientos en orden 0..3.")]
    public Button[] moveButtons = new Button[4];

    [Tooltip("Labels del NOMBRE de cada movimiento (0..3).")]
    public TMP_Text[] moveNameLabels = new TMP_Text[4];

    [Tooltip("Labels de PP (formato x/y) para cada movimiento (0..3).")]
    public TMP_Text[] movePPLabels = new TMP_Text[4];

    [Tooltip("Labels del TIPO para cada movimiento (0..3).")]
    public TMP_Text[] moveTypeLabels = new TMP_Text[4];

    [Header("Estilos")]
    [Tooltip("Color del texto general (nombre, tipo y PP cuando hay PP).")]
    public Color textColor = Color.black;
    [Tooltip("Color del texto de PP cuando hay PP (>0).")]
    public Color ppOkColor = Color.black;
    [Tooltip("Color del texto de PP cuando está en 0.")]
    public Color ppZeroColor = new Color(0.85f, 0f, 0f, 1f);
    [Tooltip("Color del texto del tipo.")]
    public Color typeColor = Color.black;

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
            // Panel de equipo si procede
        });

        btnItems.onClick.RemoveAllListeners();
        btnItems.onClick.AddListener(() =>
        {
            // Inventario si procede
        });

        // Botones de movimientos
        for (int i = 0; i < moveButtons.Length; i++)
        {
            int idx = i;
            if (moveButtons[i] == null) continue;

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
        foreach (var c in all) { if (c.IsPlayer) { player = c; break; } }
        if (player == null || player.Model == null) { ClearAllMoveSlots(); return; }

        var moves = player.Model.Moves;

        for (int i = 0; i < 4; i++)
        {
            var hasData = (moves != null && i < moves.Count && moves[i] != null && moves[i].data != null);

            // Nombre
            if (moveNameLabels != null && i < moveNameLabels.Length && moveNameLabels[i] != null)
            {
                moveNameLabels[i].text = hasData ? moves[i].data.moveName : "-";
                moveNameLabels[i].color = textColor;
            }

            // PP
            if (movePPLabels != null && i < movePPLabels.Length && movePPLabels[i] != null)
            {
                if (hasData)
                {
                    int cur = Mathf.Max(0, moves[i].currentPP);
                    int max = Mathf.Max(0, moves[i].maxPP);
                    movePPLabels[i].text = $"{cur}/{max}";
                    movePPLabels[i].color = (cur > 0) ? ppOkColor : ppZeroColor;
                }
                else
                {
                    movePPLabels[i].text = "";
                    movePPLabels[i].color = ppOkColor;
                }
            }

            // Tipo
            if (moveTypeLabels != null && i < moveTypeLabels.Length && moveTypeLabels[i] != null)
            {
                moveTypeLabels[i].text = hasData ? moves[i].data.type.ToString() : "";
                moveTypeLabels[i].color = typeColor;
            }

            // Interactuable
            if (moveButtons != null && i < moveButtons.Length && moveButtons[i] != null)
            {
                bool canUse = hasData && moves[i].currentPP > 0;
                moveButtons[i].interactable = canUse;
            }
        }
    }

    private void ClearAllMoveSlots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (moveNameLabels != null && i < moveNameLabels.Length && moveNameLabels[i] != null)
            {
                moveNameLabels[i].text = "-";
                moveNameLabels[i].color = textColor;
            }

            if (movePPLabels != null && i < movePPLabels.Length && movePPLabels[i] != null)
            {
                movePPLabels[i].text = "";
                movePPLabels[i].color = ppOkColor;
            }

            if (moveTypeLabels != null && i < moveTypeLabels.Length && moveTypeLabels[i] != null)
            {
                moveTypeLabels[i].text = "";
                moveTypeLabels[i].color = typeColor;
            }

            if (moveButtons != null && i < moveButtons.Length && moveButtons[i] != null)
                moveButtons[i].interactable = false;
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
