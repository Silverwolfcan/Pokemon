using UnityEngine;
using TMPro;

/// Colócalo en un GameObject con un Collider marcado como "Is Trigger".
/// Al entrar con el jugador y pulsar E, cura el equipo al 100% (y opcionalmente PP).
public class HealerStation : MonoBehaviour
{
    [Header("Interacción")]
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("Texto que se muestra al acercarse.")]
    public string promptText = "Pulsa E para curar el equipo";

    [Header("UI opcional (puedes dejarlo en null)")]
    public CanvasGroup promptCanvas;   // Panel world-space o screen-space
    public TMP_Text promptLabel;       // Texto del aviso/feedback

    [Header("Opciones")]
    [Tooltip("Si está activo, también restaura los PP de todos los movimientos.")]
    public bool restorePP = false;

    private bool playerInRange = false;
    private PlayerController player;
    private ItemSelectorUI itemSelector;

    private void Awake()
    {
        itemSelector = FindObjectOfType<ItemSelectorUI>();
        SetPromptVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        player = pc;
        playerInRange = true;
        ShowPromptText(promptText);
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>(); // << genéricos correctos
        if (pc == null || pc != player) return;

        playerInRange = false;
        player = null;
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (!playerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            HealParty();
        }
    }

    public void HealParty()
    {
        var party = PokemonStorageManager.Instance?.PlayerParty;
        if (party == null)
        {
            ShowPromptText("No hay party.");
            return;
        }

        var list = party.ToList(); // 6 slots con posibles nulls
        int healedCount = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p == null) continue;

            // HP al máximo
            p.currentHP = p.stats.MaxHP;

            // (Opcional) PP al máximo
            if (restorePP && p.Moves != null)
            {
                for (int m = 0; m < p.Moves.Count; m++)
                {
                    var mv = p.Moves[m];
                    if (mv == null) continue;
                    mv.currentPP = mv.maxPP;
                }
            }

            healedCount++;
        }

        // Refresca selector para que “Debilitado” desaparezca y vuelvan a invocarse
        itemSelector?.RefreshCapturedPokemon();

        ShowPromptText(healedCount > 0 ? "¡Equipo curado!" : "No hay Pokémon que curar");
        SetPromptVisible(true);
    }

    // ---------- Helpers UI ----------
    private void SetPromptVisible(bool visible)
    {
        if (promptCanvas != null)
        {
            promptCanvas.alpha = visible ? 1f : 0f;
            promptCanvas.blocksRaycasts = visible;
            promptCanvas.interactable = visible;
        }
    }

    private void ShowPromptText(string text)
    {
        if (promptLabel != null) promptLabel.text = text;
    }
}
