using UnityEngine;

/// Métodos de compatibilidad para código que espera CanUse/UseItem con objetivo.
/// No modifica InventoryManager existente. Consumir unidades sigue usando InventoryManager.UseItem(item).
public static class InventoryManagerExtensions
{
    /// Comprueba si puede usarse el objeto sobre el objetivo. Provee un motivo si no.
    public static bool CanUse(this InventoryManager inv, ItemData item, PokemonInstance target, out string reason)
    {
        bool inCombat = ServiceLocator.Get<CombatStateService>()?.IsActive ?? false;
        return ItemEffectsService.CanUse(item, target, inCombat, out reason);
    }

    /// Aplica el objeto sobre el objetivo. Si se aplica, descuenta 1 unidad del inventario.
    public static bool UseItem(this InventoryManager inv, ItemData item, PokemonInstance target, int moveIndex = -1)
    {
        if (inv == null || item == null || target == null) return false;

        var result = ItemEffectsService.Apply(item, target, moveIndex);
        if (result == ItemUseResult.Applied)
        {
            bool ok = inv.UseItem(item); // consume 1 unidad
            if (ok) GameEventBus.RaiseInventoryChanged();
            return ok;
        }
        return false;
    }
}
