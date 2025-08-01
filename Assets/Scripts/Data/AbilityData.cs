using UnityEngine;

[CreateAssetMenu(fileName = "New Ability", menuName = "Pokemon/Abilityes")]
public class AbilityData : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description;
    // Puedes agregar efectos aquí más adelante
}
