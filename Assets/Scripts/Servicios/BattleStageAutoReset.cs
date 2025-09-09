using UnityEngine;

/// Adjunta este componente al prefab del Encounter (mismo GO que EncounterController).
/// Al destruirse el encuentro, limpia las etapas de los pokémon implicados.
public class BattleStageAutoReset : MonoBehaviour
{
    private void OnDestroy()
    {
        // Busca combatientes aún vivos en escena y limpia
        var combatants = FindObjectsByType<CombatantController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in combatants)
        {
            if (c != null && c.Model != null)
                BattleStageService.ResetFor(c.Model);
        }
    }
}
