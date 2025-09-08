using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CombatUIController : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("Panel raíz del menú principal (NO pongas aquí el Canvas).")]
    public GameObject panelRoot;
    public Button btnAttack, btnCapture, btnSwitch, btnItems, btnRun;

    [Header("Moves")]
    public GameObject panelMoves;
    public Button[] moveButtons = new Button[4];
    public TMP_Text[] moveNameLabels = new TMP_Text[4];
    public TMP_Text[] movePPLabels = new TMP_Text[4];
    public TMP_Text[] moveTypeLabels = new TMP_Text[4];

    [Header("Colores")]
    public Color textColor = Color.black;
    public Color ppOkColor = Color.black;
    public Color ppZeroColor = new Color(0.85f, 0f, 0f, 1f);
    public Color typeColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool verbose = false;

    private TurnController turn;
    private PlayerController playerController;
    private ItemSelectorUI selector;
    private EncounterController encounter;

    private bool captureMode = false;

    private void OnEnable()
    {
        HideAll();
        captureMode = false;

        playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        selector = UnityEngine.Object.FindAnyObjectByType<ItemSelectorUI>();
        encounter = UnityEngine.Object.FindAnyObjectByType<EncounterController>();

        TryBind();
        InvokeRepeating(nameof(RefreshContext), 0.1f, 0.5f);
        GameEventBus.InventoryChanged += OnInventoryChanged;
        GameEventBus.EncounterStateChanged += OnEncounterStateChanged;
    }

    private void OnDisable()
    {
        if (turn != null)
        {
            turn.OnPlayerTurnStart -= OnPlayerTurnStart;
            turn.OnEnemyTurnStart -= OnEnemyTurnStart;
            turn = null;
        }
        CancelInvoke(nameof(RefreshContext));
        captureMode = false;

        GameEventBus.InventoryChanged -= OnInventoryChanged;
        GameEventBus.EncounterStateChanged -= OnEncounterStateChanged;
    }

    private void OnEncounterStateChanged(bool active)
    {
        if (active) encounter = UnityEngine.Object.FindAnyObjectByType<EncounterController>();
        UpdateButtonsByRules();
    }

    private void RefreshContext()
    {
        if (turn == null) TryBind();
        if (encounter == null) encounter = UnityEngine.Object.FindAnyObjectByType<EncounterController>();
        UpdateButtonsByRules();
    }

    private void TryBind()
    {
        if (turn != null) return;

        var t = UnityEngine.Object.FindAnyObjectByType<TurnController>();
        if (t == null) return;

        turn = t;
        turn.OnPlayerTurnStart += OnPlayerTurnStart;
        turn.OnEnemyTurnStart += OnEnemyTurnStart;

        if (btnAttack)
        {
            btnAttack.onClick.RemoveAllListeners();
            btnAttack.onClick.AddListener(() => { ShowMoves(true); SetCursorForUI(); });
        }

        if (btnCapture)
        {
            btnCapture.onClick.RemoveAllListeners();
            btnCapture.onClick.AddListener(OnClickCapture);
        }

        if (btnRun)
        {
            btnRun.onClick.RemoveAllListeners();
            btnRun.onClick.AddListener(() =>
            {
                if (encounter != null && !encounter.CanRun) { Debug.Log("[UI] Huir deshabilitado."); return; }
                turn.QueueRun();
                HideAll();
                SetCursorForGameplay();
            });
        }

        if (btnSwitch) { btnSwitch.onClick.RemoveAllListeners(); btnSwitch.onClick.AddListener(() => { }); }
        if (btnItems) { btnItems.onClick.RemoveAllListeners(); btnItems.onClick.AddListener(() => { }); }

        for (int i = 0; i < moveButtons.Length; i++)
        {
            int idx = i;
            var b = (moveButtons != null && i < moveButtons.Length) ? moveButtons[i] : null;
            if (!b) continue;

            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                turn.QueueMove(idx);
                HideAll();
                SetCursorForGameplay();
            });
        }

        UpdateButtonsByRules();
        PopulateMovesIfPossible();
    }

    private void OnInventoryChanged() => UpdateButtonsByRules();

    private void UpdateButtonsByRules()
    {
        bool allowCapture = (encounter == null || encounter.IsCaptureAllowed) && HasAnyPokeball();
        bool allowRun = (encounter == null || encounter.CanRun);

        if (btnCapture) btnCapture.interactable = allowCapture;
        if (btnRun) btnRun.interactable = allowRun;

        if (verbose) Debug.Log($"[CombatUI] Buttons → Capture:{allowCapture} Run:{allowRun}");
    }

    // ---------- Turnos ----------
    private void OnPlayerTurnStart()
    {
        captureMode = false;
        playerController?.EnableControls(false);

        PopulateMovesIfPossible();
        ShowMainMenu();
        UpdateButtonsByRules();
        SetCursorForUI();
    }

    private void OnEnemyTurnStart()
    {
        captureMode = false;
        playerController?.EnableControls(false);
        HideAll();
        SetCursorForGameplay();
    }

    public void ShowMainMenu()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (panelMoves) panelMoves.SetActive(false);
    }

    public void HideAll()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (panelMoves) panelMoves.SetActive(false);
    }

    private void ShowMoves(bool v)
    {
        if (panelMoves) panelMoves.SetActive(v);
        if (panelRoot) panelRoot.SetActive(!v);
    }

    private void PopulateMovesIfPossible()
    {
        var all = UnityEngine.Object.FindObjectsByType<CombatantController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        CombatantController player = null;
        for (int i = 0; i < all.Length; i++) if (all[i] && all[i].IsPlayer) { player = all[i]; break; }
        if (player == null || player.Model == null) { ClearAllMoveSlots(); return; }

        var moves = player.Model.Moves;
        for (int i = 0; i < 4; i++)
        {
            bool has = (moves != null && i < moves.Count && moves[i] != null && moves[i].data != null);

            if (moveNameLabels != null && i < moveNameLabels.Length && moveNameLabels[i])
            { moveNameLabels[i].text = has ? moves[i].data.moveName : "-"; moveNameLabels[i].color = textColor; }

            if (movePPLabels != null && i < movePPLabels.Length && movePPLabels[i])
            {
                if (has)
                {
                    int cur = Mathf.Max(0, moves[i].currentPP);
                    int max = Mathf.Max(0, moves[i].maxPP);
                    movePPLabels[i].text = $"{cur}/{max}";
                    movePPLabels[i].color = (cur > 0) ? ppOkColor : ppZeroColor;
                }
                else { movePPLabels[i].text = ""; movePPLabels[i].color = ppOkColor; }
            }

            if (moveTypeLabels != null && i < moveTypeLabels.Length && moveTypeLabels[i])
            { moveTypeLabels[i].text = has ? moves[i].data.type.ToString() : ""; moveTypeLabels[i].color = typeColor; }

            if (moveButtons != null && i < moveButtons.Length && moveButtons[i])
                moveButtons[i].interactable = has && moves[i].currentPP > 0;
        }
    }

    private void ClearAllMoveSlots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (moveNameLabels != null && i < moveNameLabels.Length && moveNameLabels[i]) { moveNameLabels[i].text = "-"; moveNameLabels[i].color = textColor; }
            if (movePPLabels != null && i < movePPLabels.Length && movePPLabels[i]) { movePPLabels[i].text = ""; movePPLabels[i].color = ppOkColor; }
            if (moveTypeLabels != null && i < moveTypeLabels.Length && moveTypeLabels[i]) { moveTypeLabels[i].text = ""; moveTypeLabels[i].color = typeColor; }
            if (moveButtons != null && i < moveButtons.Length && moveButtons[i]) moveButtons[i].interactable = false;
        }
    }

    // ---------- Captura ----------
    private void OnClickCapture()
    {
        if (encounter != null && !encounter.IsCaptureAllowed) { Debug.Log("[UI] Capturar deshabilitado."); return; }
        if (!HasAnyPokeball())
        {
            GameEventBus.RaiseCaptureShakes(0, 0f, false);
            return;
        }

        var enemy = FindCombatant(false)?.Model;
        if (enemy != null)
        {
            var ball = selector ? selector.GetSelectedBallData() : null;
            if (ball == null) ball = FindAnyBallFromInventory();
            if (ball != null)
            {
                float chance = CaptureService.ComputeCaptureChance(enemy, ball);
                int shakesIfFail = CaptureService.ComputeShakeCount(false, chance);
                GameEventBus.RaiseCaptureShakes(shakesIfFail, chance, false);
                if (verbose) Debug.Log($"[CombatUI] Captura preview chance={chance:0.###} shakesIfFail={shakesIfFail}");
            }
        }

        captureMode = true;
        playerController?.EnableControls(true);
        HideAll();
        SetCursorForGameplay();
    }

    private CombatantController FindCombatant(bool isPlayer)
    {
        var list = UnityEngine.Object.FindObjectsByType<CombatantController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++) if (list[i] && list[i].IsPlayer == isPlayer) return list[i];
        return null;
    }

    private bool HasAnyPokeball()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return false;

        var inventoryField = inv.GetType().GetField("inventory");
        var list = inventoryField != null ? inventoryField.GetValue(inv) as System.Collections.IEnumerable : null;
        if (list == null) return false;

        foreach (var entry in list)
        {
            if (entry == null) continue;
            object item = TryGet(entry, "item") ?? TryGet(entry, "Item");
            int qty = ToInt(TryGet(entry, "quantity"));
            bool unlocked = ToBool(TryGet(entry, "unlocked"));
            if (item is PokeballData && unlocked && qty > 0) return true;
        }
        return false;
    }

    private static object TryGet(object obj, string name)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var pi = t.GetProperty(name);
        if (pi != null) { try { return pi.GetValue(obj); } catch { } }
        var fi = t.GetField(name);
        if (fi != null) { try { return fi.GetValue(obj); } catch { } }
        return null;
    }

    private static int ToInt(object o) { try { return System.Convert.ToInt32(o); } catch { return 0; } }
    private static bool ToBool(object o) { try { return System.Convert.ToBoolean(o); } catch { return false; } }

    private PokeballData FindAnyBallFromInventory()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return null;

        var inventoryField = inv.GetType().GetField("inventory");
        var list = inventoryField != null ? inventoryField.GetValue(inv) as System.Collections.IEnumerable : null;
        if (list == null) return null;

        foreach (var entry in list)
        {
            if (entry == null) continue;
            object item = TryGet(entry, "item") ?? TryGet(entry, "Item");
            int qty = ToInt(TryGet(entry, "quantity"));
            bool unlocked = ToBool(TryGet(entry, "unlocked"));
            if (item is PokeballData pb && unlocked && qty > 0) return pb;
        }
        return null;
    }

    private static void SetCursorForUI() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    private static void SetCursorForGameplay() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
}
