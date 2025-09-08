using UnityEngine;

/// Servicio central para aplicar efectos de objetos.
/// Mantiene compatibilidad delegando en ItemsEffectsUtility para curativos.
/// No gestiona Pokéballs; se lanzan con el selector dedicado en combate.
public static class ItemEffectsService
{
    /// Valida si el objeto es utilizable sobre el objetivo.
    /// Devuelve true/false y un motivo en caso de no poder usarse.
    public static bool CanUse(ItemData item, PokemonInstance target, bool inCombat, out string reason)
    {
        reason = null;
        if (item == null || target == null) { reason = "Objeto o objetivo nulo."; return false; }

        // Pokéballs: fuera de este flujo
        if (item is PokeballData)
        {
            reason = "Las Pokéballs se usan desde el selector de captura.";
            return false;
        }

        // Curativos y afines
        if (item is HealingItemData heal)
        {
            // Reglas básicas sin conocer estados avanzados
            if (heal.IsRevive())
            {
                if (target.currentHP > 0) { reason = "El objetivo no está debilitado."; return false; }
                return true;
            }

            // Si no es revive y no es restaurador de PP ni estados, requerimos que esté vivo
            if (!heal.IsPPRestore() && target.currentHP <= 0)
            {
                reason = "El objetivo está debilitado.";
                return false;
            }

            // En combate puedes usar curas estándar; si quieres restringir algo, hazlo aquí
            return true;
        }

        reason = "Ese objeto no se puede usar desde la mochila.";
        return false;
    }

    /// Aplica el efecto. Devuelve ItemUseResult.
    /// Para objetos que requieren movimiento concreto (Éter), pasa moveIndex, si no, -1.
    public static ItemUseResult Apply(ItemData item, PokemonInstance target, int moveIndex = -1)
    {
        if (item == null || target == null) return ItemUseResult.InvalidTarget;

        if (item is HealingItemData heal)
        {
            // Delegamos en la utilidad existente para mantener el comportamiento actual.
            return ItemEffectsUtility.ApplyHealingItem(target, heal, moveIndex);
        }

        // Extensible: estados avanzados, vitaminas, etc.
        return ItemUseResult.InvalidTarget;
    }
}
