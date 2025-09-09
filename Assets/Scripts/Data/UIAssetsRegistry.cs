// UI/UIAssetsRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/UI Assets Registry", fileName = "UIAssetsRegistry")]
public class UIAssetsRegistry : ScriptableObject
{
    [Serializable]
    public struct PokemonTypeSprite
    {
        public PokemonType type;
        public Sprite sprite;
        public Color color;
    }

    [Serializable]
    public struct ElementTypeSprite
    {
        public ElementType type;
        public Sprite sprite;
        public Color color;
    }

    [Serializable]
    public struct PrimaryStatusSprite
    {
        public StatusService.PrimaryStatus status; // Burn/Poison/Sleep/Paralysis/Freeze/None
        public Sprite sprite;
        public Color color;
    }

    [Header("Sprites por PokemonType (especie)")]
    public List<PokemonTypeSprite> pokemonTypeSprites = new List<PokemonTypeSprite>();

    [Header("Sprites por ElementType (movimientos)")]
    public List<ElementTypeSprite> elementTypeSprites = new List<ElementTypeSprite>();

    [Header("Estados primarios")]
    public List<PrimaryStatusSprite> primaryStatusSprites = new List<PrimaryStatusSprite>();

    [Header("Sexo")]
    public Sprite maleSprite;
    public Sprite femaleSprite;
    public Sprite unknownSprite;

    [Header("Fondos del slot de EQUIPO")]
    public Sprite partyBgUnselected;
    public Sprite partyBgSelected;

    private Dictionary<PokemonType, PokemonTypeSprite> _mapPokemonType;
    private Dictionary<ElementType, ElementTypeSprite> _mapElementType;
    private Dictionary<StatusService.PrimaryStatus, PrimaryStatusSprite> _mapPrimaryStatus;

    void OnEnable()
    {
        _mapPokemonType = new Dictionary<PokemonType, PokemonTypeSprite>();
        foreach (var ts in pokemonTypeSprites) _mapPokemonType[ts.type] = ts;

        _mapElementType = new Dictionary<ElementType, ElementTypeSprite>();
        foreach (var ts in elementTypeSprites) _mapElementType[ts.type] = ts;

        _mapPrimaryStatus = new Dictionary<StatusService.PrimaryStatus, PrimaryStatusSprite>();
        foreach (var ps in primaryStatusSprites) _mapPrimaryStatus[ps.status] = ps;
    }

    // --- Para especie (PokemonType) ---
    public Sprite GetTypeSprite(PokemonType t)
    {
        if (t == PokemonType.None) return null;
        return (_mapPokemonType != null && _mapPokemonType.TryGetValue(t, out var ts)) ? ts.sprite : null;
    }

    public Color GetTypeColor(PokemonType t)
    {
        if (t == PokemonType.None) return Color.white;
        return (_mapPokemonType != null && _mapPokemonType.TryGetValue(t, out var ts)) ? ts.color : Color.white;
    }

    // --- Para movimientos (ElementType) ---
    public Sprite GetTypeSprite(ElementType t)
    {
        return (_mapElementType != null && _mapElementType.TryGetValue(t, out var ts)) ? ts.sprite : null;
    }

    public Color GetTypeColor(ElementType t)
    {
        return (_mapElementType != null && _mapElementType.TryGetValue(t, out var ts)) ? ts.color : Color.white;
    }

    // --- Para estados primarios ---
    public Sprite GetStatusSprite(StatusService.PrimaryStatus s)
    {
        if (s == StatusService.PrimaryStatus.None) return null;
        return (_mapPrimaryStatus != null && _mapPrimaryStatus.TryGetValue(s, out var ps)) ? ps.sprite : null;
    }

    public Color GetStatusColor(StatusService.PrimaryStatus s)
    {
        if (s == StatusService.PrimaryStatus.None) return Color.white;
        return (_mapPrimaryStatus != null && _mapPrimaryStatus.TryGetValue(s, out var ps)) ? ps.color : Color.white;
    }

    public Sprite GetGenderSprite(Gender g)
    {
        switch (g)
        {
            case Gender.Male: return maleSprite;
            case Gender.Female: return femaleSprite;
            default: return unknownSprite;
        }
    }
}

/*
Asignaciones en el Inspector:
- Completar la lista “Estados primarios” con tus sprites:
  * Burn, Poison, Sleep, Paralysis, Freeze. Color opcional por estado.
- El resto de listas (tipos/elementos) como ya las usabas.
*/
