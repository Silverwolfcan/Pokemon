using UnityEngine;

[CreateAssetMenu(fileName = "New Pokeball", menuName = "Pokémon/Items/Pokéball")]
public class PokeballData : ItemData
{
    public float catchMultiplier = 1f;

    [Header("Configuración")]
    public bool unlockedByDefault = true;
}
