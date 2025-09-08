using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class PokemonStatsPanelController : MonoBehaviour
{
    [Header("Refs")]
    public UIAssetsRegistry assets;

    [Header("Cabecera")]
    public TMP_Text txtName;
    public TMP_Text txtLevel;        // SOLO número
    public Image imgGender;
    public Image imgType1;
    public Image imgType2;

    [Header("Radar")]
    public PokemonRadarChart radar;

    [Header("Detalles")]
    [Tooltip("Nombre de la habilidad mostrada.")]
    public TMP_Text txtAbility;
    [Tooltip("Nombre del objeto equipado. Si no tiene, debe mostrar 'Ninguno'.")]
    public TMP_Text txtHeldItem;

    private PokemonInstance current;

    public void SetPokemon(PokemonInstance p)
    {
        current = p;
        Render();
    }

    private void OnEnable() { Render(); }

    private void Render()
    {
        if (!gameObject.activeInHierarchy) return;

        if (current == null || current.species == null)
        {
            if (txtName) txtName.text = "";
            if (txtLevel) txtLevel.text = "";
            if (imgGender) { imgGender.enabled = false; imgGender.sprite = null; }
            if (imgType1) { imgType1.enabled = false; imgType1.sprite = null; }
            if (imgType2) { imgType2.enabled = false; imgType2.sprite = null; }
            if (txtAbility) txtAbility.text = "";
            if (txtHeldItem) txtHeldItem.text = "Ninguno";

            if (radar)
            {
                radar.HP = radar.Attack = radar.Defense = radar.SpAttack = radar.SpDefense = radar.Speed = 0f;
                radar.SetVerticesDirty();
            }
            return;
        }

        if (txtName) txtName.text = current.species.pokemonName;
        // SOLO número de nivel
        if (txtLevel) txtLevel.text = current.level.ToString();

        if (imgGender)
        {
            var sp = assets ? assets.GetGenderSprite(current.gender) : null;
            if (sp) { imgGender.enabled = true; imgGender.sprite = sp; }
            else { imgGender.enabled = false; imgGender.sprite = null; }
        }

        if (imgType1)
        {
            var t1 = current.species.primaryType;
            var sp1 = assets ? assets.GetTypeSprite(t1) : null;
            if (t1 != PokemonType.None && sp1)
            {
                imgType1.enabled = true;
                imgType1.sprite = sp1;
                imgType1.color = assets ? assets.GetTypeColor(t1) : Color.white;
            }
            else { imgType1.enabled = false; imgType1.sprite = null; }
        }

        if (imgType2)
        {
            var t2 = current.species.secondaryType;
            if (t2 != PokemonType.None)
            {
                var sp2 = assets ? assets.GetTypeSprite(t2) : null;
                if (sp2)
                {
                    imgType2.enabled = true;
                    imgType2.sprite = sp2;
                    imgType2.color = assets ? assets.GetTypeColor(t2) : Color.white;
                }
                else { imgType2.enabled = false; imgType2.sprite = null; }
            }
            else { imgType2.enabled = false; imgType2.sprite = null; }
        }

        // Habilidad
        if (txtAbility)
        {
            txtAbility.text = GetAbilityName(current);
        }

        // Objeto equipado (placeholder: si no hay, 'Ninguno')
        if (txtHeldItem)
        {
            txtHeldItem.text = GetHeldItemName(current);
        }

        if (radar)
        {
            radar.HP = current.stats.MaxHP;
            radar.Attack = current.stats.Attack;
            radar.Defense = current.stats.Defense;
            radar.SpAttack = current.stats.SpAttack;
            radar.SpDefense = current.stats.SpDefense;
            radar.Speed = current.stats.Speed;
            radar.SetVerticesDirty();
        }
    }

    private static string GetAbilityName(PokemonInstance p)
    {
        // Si el proyecto define una habilidad activa en la instancia, úsala.
        // Fallback: primera possibleAbilities de la especie (por nombre del asset).
        if (p == null || p.species == null) return "";

        // Intenta campo/propiedad 'ability' o 'currentAbility'
        var t = p.GetType();
        var f = t.GetField("ability", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
             ?? t.GetField("currentAbility", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        object abilityObj = f != null ? f.GetValue(p) : null;

        if (abilityObj == null)
        {
            var prop = t.GetProperty("Ability", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? t.GetProperty("CurrentAbility", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null) abilityObj = prop.GetValue(p, null);
        }

        if (abilityObj == null && p.species.possibleAbilities != null && p.species.possibleAbilities.Length > 0)
            abilityObj = p.species.possibleAbilities[0];

        if (abilityObj == null) return "";

        // Intenta propiedad/campo 'abilityName'; si no, usa nombre del asset
        var at = abilityObj.GetType();
        var nameField = at.GetField("abilityName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (nameField != null)
        {
            var val = nameField.GetValue(abilityObj) as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }
        var nameProp = at.GetProperty("abilityName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (nameProp != null)
        {
            var val = nameProp.GetValue(abilityObj, null) as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }
        // Fallback genérico: nombre del asset ScriptableObject
        if (abilityObj is Object uobj) return uobj.name;
        return abilityObj.ToString();
    }

    private static string GetHeldItemName(PokemonInstance p)
    {
        if (p == null) return "Ninguno";

        // Intentar descubrir algún campo 'heldItem' o 'item' en la instancia.
        var t = p.GetType();
        var f = t.GetField("heldItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
             ?? t.GetField("item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        object itemObj = f != null ? f.GetValue(p) : null;

        if (itemObj == null)
        {
            var prop = t.GetProperty("HeldItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null) itemObj = prop.GetValue(p, null);
        }

        if (itemObj == null) return "Ninguno";

        // Intentar 'displayName' o 'itemName'; si no, usar nombre del asset
        var it = itemObj.GetType();
        var dispField = it.GetField("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? it.GetField("itemName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (dispField != null)
        {
            var s = dispField.GetValue(itemObj) as string;
            if (!string.IsNullOrEmpty(s)) return s;
        }
        var dispProp = it.GetProperty("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? it.GetProperty("itemName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (dispProp != null)
        {
            var s = dispProp.GetValue(itemObj, null) as string;
            if (!string.IsNullOrEmpty(s)) return s;
        }

        if (itemObj is Object uobj) return uobj.name;
        return itemObj.ToString();
    }

    // API simple por si en el futuro otro sistema quiere forzar el texto del objeto:
    public void SetHeldItemText(string displayName)
    {
        if (txtHeldItem) txtHeldItem.text = string.IsNullOrEmpty(displayName) ? "Ninguno" : displayName;
    }
}
