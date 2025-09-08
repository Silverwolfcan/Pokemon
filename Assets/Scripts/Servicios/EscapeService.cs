using UnityEngine;

/// Cálculo de huida con piso garantizado.
/// Firma inalterada para no romper llamadas existentes.
public static class EscapeService
{
    /// Devuelve true si la huida tiene éxito.
    /// attempts: nº de intentos previos en este combate (1 en el primer intento).
    public static bool RollFlee(int playerSpeed, int enemySpeed, int attempts)
    {
        playerSpeed = Mathf.Max(1, playerSpeed);
        enemySpeed = Mathf.Max(1, enemySpeed);
        attempts = Mathf.Max(1, attempts);

        // Probabilidad clásica en espacio 0..256 (Gen I–III aproximada)
        // chance256 = (playerSpeed * 128 / enemySpeed) + 30 * attempts;
        int classic256 = (playerSpeed * 128) / enemySpeed + 30 * attempts;
        classic256 = Mathf.Clamp(classic256, 0, 255);

        // Piso solicitado: 50% + 25% si el jugador es más rápido (0.50 o 0.75)
        float floorP = 0.5f + (playerSpeed > enemySpeed ? 0.25f : 0f); // 0.50 o 0.75
        int floor256 = Mathf.Clamp(Mathf.RoundToInt(floorP * 256f), 0, 255);

        // Usar la más alta para garantizar el piso sin perder la sensación clásica
        int final256 = Mathf.Max(classic256, floor256);

        int roll = Random.Range(0, 256);
        bool success = roll < final256;

        Debug.Log($"[Escape] SPE_P={playerSpeed} SPE_E={enemySpeed} Att={attempts} | " +
                  $"Classic={classic256}/256 ({classic256 / 256f:0.00}) | Floor={floor256}/256 ({floorP:0.00}) | " +
                  $"Final={final256}/256 ({final256 / 256f:0.00}) | Roll={roll} → {(success ? "EXIT" : "FAIL")}");

        return success;
    }
}
