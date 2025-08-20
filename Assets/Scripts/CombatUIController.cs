using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUIController : MonoBehaviour
{
    [Header("Root")]
    public GameObject panelRoot;
    public Button btnAttack, btnCapture, btnSwitch, btnItems, btnRun;

    [Header("Moves")]
    public GameObject panelMoves;
    public Button[] moveButtons = new Button[4];
    public TMP_Text[] moveLabels = new TMP_Text[4];

    private TurnController turn;

    void OnEnable()
    {
        // Intento de engancharme al TurnController del encuentro activo
        InvokeRepeating(nameof(TryBind), 0f, 0.25f);

        // Root
        btnAttack.onClick.AddListener(() => ShowMoves(true));
        btnCapture.onClick.AddListener(() => { turn?.QueueCapture(); HideAll(); });
        btnSwitch.onClick.AddListener(() => { /* abre tu UI de party y al elegir: turn.QueueSwitch(p); */ });
        btnItems.onClick.AddListener(() => { /* abre inventario de batalla */ });
        btnRun.onClick.AddListener(() => { /* opcional confirmar */ turn?.QueueRun(); HideAll(); });

        // Moves handlers
        for (int i = 0; i < moveButtons.Length; i++)
        {
            int idx = i;
            moveButtons[i].onClick.AddListener(() =>
            {
                turn?.QueueMove(idx);
                HideAll();
            });
        }

        HideAll();
    }

    void OnDisable()
    {
        if (turn != null)
        {
            turn.OnPlayerTurnStart -= OnPlayerTurnStart;
            turn.OnEnemyTurnStart -= OnEnemyTurnStart;
        }
        CancelInvoke(nameof(TryBind));
    }

    void TryBind()
    {
        if (turn != null) return;
        turn = FindAnyObjectByType<TurnController>();
        if (turn == null) return;

        // Suscribirse a los turnos
        turn.OnPlayerTurnStart += OnPlayerTurnStart;
        turn.OnEnemyTurnStart += OnEnemyTurnStart;

        // Al terminar el combate, ocúltate
        CombatEvents.OnEncounterEnded += _ => { HideAll(); };
    }

    void OnPlayerTurnStart()
    {
        // Rellenar nombres/PP de movimientos
        var p = FindAnyObjectByType<EncounterController>(); // sólo para localizar el contexto
        // Mejor: expón un getter en EncounterController si quieres.
        var cbt = FindAnyObjectByType<CombatantController>(); // el primero suele ser el del jugador

        // Si prefieres, puedes acceder a la instancia a través de tu manager
        var inst = cbt?.IsPlayer == true ? cbt.Model : null;

        for (int i = 0; i < 4; i++)
        {
            var has = inst != null && inst.Moves != null && i < inst.Moves.Count && inst.Moves[i] != null && inst.Moves[i].data != null;
            moveButtons[i].interactable = has;
            if (moveLabels != null && i < moveLabels.Length)
                moveLabels[i].text = has ? $"{inst.Moves[i].data.moveName} {inst.Moves[i].currentPP}/{inst.Moves[i].maxPP}" : "--";
        }

        panelRoot.SetActive(true);
        panelMoves.SetActive(false);
    }

    void OnEnemyTurnStart() { HideAll(); }

    void ShowMoves(bool v)
    {
        panelMoves.SetActive(v);
        panelRoot.SetActive(!v);
    }

    void HideAll()
    {
        panelRoot.SetActive(false);
        panelMoves.SetActive(false);
    }
}
