using UnityEngine;

public enum AttackType { Physical, Special, Status }
public enum ElementType { Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy }

[CreateAssetMenu(fileName = "New Move", menuName = "Pokemon/Moves")]
public class AttackData : ScriptableObject
{
    public string attackName;
    [TextArea] public string description;
    public ElementType type;
    public AttackType attackCategory;
    public int power;         // Only for physical/special
    public int accuracy;      // 0–100
    public int pp;            // Max uses
    public bool hasSecondaryEffect;
    public string secondaryEffectDescription;
}
