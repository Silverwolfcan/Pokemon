using UnityEngine;

[CreateAssetMenu(fileName = "New Pokeball", menuName = "Pok�mon/Items/Pok�ball")]
public class PokeballData : ItemData
{
    public float catchMultiplier = 1f;

    [Header("Configuraci�n")]
    public bool unlockedByDefault = true;
}
