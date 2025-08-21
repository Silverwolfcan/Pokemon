using System;
using System.Collections;
using UnityEngine;

public class TurnController : MonoBehaviour
{
    private CombatantController player, enemy;

    // Eventos para la UI
    public event Action OnPlayerTurnStart;
    public event Action OnEnemyTurnStart;

    // Cola decisión jugador
    private int? queuedPlayerMoveIndex = null;
    private bool queuedRun = false;
    private bool queuedCapture = false;
    private PokemonInstance queuedSwitchIn = null;

    // Completar turno externamente (fallo de captura, etc.)
    private bool externalTurnComplete = false;

    // Callback de fin de combate (inyectado por EncounterController)
    private Action<EncounterResult> onEndEncounter;

    // --------- Setup ---------
    public void Setup(CombatantController playerCombatant, CombatantController enemyCombatant)
    {
        Setup(playerCombatant, enemyCombatant, null);
    }

    public void Setup(CombatantController playerCombatant, CombatantController enemyCombatant, Action<EncounterResult> onEnd)
    {
        player = playerCombatant;
        enemy = enemyCombatant;
        onEndEncounter = onEnd;
        ClearQueued();
    }

    // --------- API para UI ---------
    public void QueueMove(int moveIndex) { queuedPlayerMoveIndex = moveIndex; }
    public void QueueRun() { queuedRun = true; }
    public void QueueCapture() { queuedCapture = true; }
    public void QueueSwitch(PokemonInstance switchIn) { queuedSwitchIn = switchIn; }
    public void ConsumePlayerTurn() { externalTurnComplete = true; }

    // --------- Bucle de turnos ---------
    public IEnumerator DoPlayerTurn(Vector3 ringCenter, float offsetFromCenter)
    {
        OnPlayerTurnStart?.Invoke();

        // Espera a que la UI encole una acción o a que se complete externamente (p.ej. fallo de captura)
        while (!queuedPlayerMoveIndex.HasValue && !queuedRun && !queuedCapture && queuedSwitchIn == null && !externalTurnComplete)
            yield return null;

        if (externalTurnComplete)
        {
            Debug.Log("[TurnController] Turno del jugador consumido externamente (p.ej., fallo de captura).");
            ClearQueued();
            yield break; // pasa a turno enemigo
        }

        if (queuedRun)
        {
            Debug.Log("[TurnController] El jugador eligió Huir.");
            ClearQueued();
            onEndEncounter?.Invoke(EncounterResult.Run);
            yield break;
        }

        if (queuedCapture)
        {
            // Compatibilidad si alguien siguiera usando este flujo: consumimos turno
            Debug.Log("[TurnController] El jugador eligió Capturar (flujo legacy): se consume turno.");
            ClearQueued();
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        if (queuedSwitchIn != null)
        {
            Debug.Log("[TurnController] (MVP) Cambio de Pokémon aún no implementado: se consume turno.");
            ClearQueued();
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        // Ejecutar movimiento del jugador
        int moveIndex = queuedPlayerMoveIndex.Value;
        ClearQueued();

        yield return ExecuteMove(player, enemy, moveIndex);
    }

    public IEnumerator DoEnemyTurn(Vector3 ringCenter, float offsetFromCenter)
    {
        OnEnemyTurnStart?.Invoke();

        // IA: primer movimiento preferido
        int preferred = WildAI.ChooseMoveIndex(enemy?.Model);

        // Validación robusta
        int safeIdx = FindUsableMoveIndex(enemy?.Model, preferredIndex: preferred);
        if (safeIdx < 0)
        {
            Debug.LogWarning("[TurnController] El enemigo no tiene movimientos usables. Pasa turno.");
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        yield return ExecuteMove(enemy, player, safeIdx);
    }

    // --------- Ejecución de movimientos ---------
    private IEnumerator ExecuteMove(CombatantController attacker, CombatantController defender, int moveIndex)
    {
        if (attacker == null || defender == null || attacker.Model == null)
        {
            Debug.LogWarning("[TurnController] ExecuteMove sin attacker/defender/model válido.");
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        // Datos del movimiento
        var mv = (attacker.Model.Moves != null && moveIndex >= 0 && moveIndex < attacker.Model.Moves.Count)
               ? attacker.Model.Moves[moveIndex]
               : null;

        string who = AttackerLabel(attacker);
        string target = AttackerLabel(defender);
        string moveName = (mv != null && mv.data != null && !string.IsNullOrEmpty(mv.data.moveName)) ? mv.data.moveName : "(vacío)";

        if (mv == null || mv.data == null)
        {
            Debug.LogWarning($"[TurnController] {who} intentó usar un slot vacío (índice {moveIndex}). Se salta el turno.");
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        // PP check
        if (mv.currentPP <= 0)
        {
            Debug.Log($"[TurnController] {who} intentó usar {moveName} pero no tiene PP.");
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        // Gastar PP
        mv.currentPP = Mathf.Max(0, mv.currentPP - 1);

        // Precisión
        if (!MoveExecutor.CheckAccuracy(attacker.Model, defender.Model, mv.data.accuracy))
        {
            Debug.Log($"[TurnController] {who} usó {moveName} → FALLÓ (Acc: {mv.data.accuracy}).");
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        // Solo daño directo para MVP (Status más adelante)
        int dmgApplied = 0;
        if (mv.data.attackCategory != AttackType.Status && mv.data.power > 0)
        {
            int dmg = MoveExecutor.ComputeDamage(attacker.Model, defender.Model, mv.data);
            dmg = Mathf.Max(0, dmg);

            int before = defender.Model != null ? defender.Model.currentHP : 0;

            // Preferir API del CombatantController si existe
            bool usedControllerAPI = false;
            try
            {
                defender.ApplyDamage(dmg);
                usedControllerAPI = true;
            }
            catch (MissingMethodException)
            {
                // Fallback si el método no existiera en tu variante
                if (defender.Model != null)
                    defender.Model.currentHP = Mathf.Max(0, defender.Model.currentHP - dmg);
            }

            int after = defender.Model != null ? defender.Model.currentHP : 0;
            dmgApplied = Mathf.Max(0, before - after);

            Debug.Log($"[TurnController] {who} usó {moveName} → DAÑO {dmgApplied} (HP {target}: {before} → {after})"
                      + (usedControllerAPI ? "" : " [fallback HP directo]"));
        }
        else
        {
            Debug.Log($"[TurnController] {who} usó {moveName} (Status/Power=0). Sin daño aplicado en MVP.");
        }

        yield return new WaitForSeconds(0.25f);
    }

    // --------- Helpers ---------
    private int FindUsableMoveIndex(PokemonInstance inst, int preferredIndex)
    {
        if (inst == null || inst.Moves == null || inst.Moves.Count == 0) return -1;

        // Preferido válido
        if (preferredIndex >= 0 && preferredIndex < inst.Moves.Count)
        {
            var m = inst.Moves[preferredIndex];
            if (m != null && m.data != null && m.currentPP > 0) return preferredIndex;
        }

        // Buscar cualquiera usable
        for (int i = 0; i < inst.Moves.Count; i++)
        {
            var m = inst.Moves[i];
            if (m != null && m.data != null && m.currentPP > 0) return i;
        }

        return -1;
    }

    private static string SpeciesName(PokemonInstance pi)
    {
        if (pi == null || pi.species == null) return "?";
        var n = pi.species.pokemonName;
        return string.IsNullOrEmpty(n) ? "?" : n;
    }

    private static string AttackerLabel(CombatantController cbt)
    {
        if (cbt == null) return "?";
        string species = SpeciesName(cbt.Model);
        return cbt.IsPlayer ? $"Jugador ({species})" : $"Enemigo ({species})";
    }

    private void ClearQueued()
    {
        queuedPlayerMoveIndex = null;
        queuedRun = false;
        queuedCapture = false;
        queuedSwitchIn = null;
        externalTurnComplete = false;
    }
}
