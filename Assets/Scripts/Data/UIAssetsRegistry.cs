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
    public struct StatusSprite
    {
        public PrimaryStatus status;
        public Sprite sprite;
    }

    [Header("Sprites por PokemonType (especie)")]
    public List<PokemonTypeSprite> pokemonTypeSprites = new List<PokemonTypeSprite>();

    [Header("Sprites por ElementType (movimientos)")]
    public List<ElementTypeSprite> elementTypeSprites = new List<ElementTypeSprite>();

    [Header("Estados primarios")]
    public List<StatusSprite> statusSprites = new List<StatusSprite>();

    [Header("Sexo")]
    public Sprite maleSprite;
    public Sprite femaleSprite;
    public Sprite unknownSprite;

    [Header("Fondos del slot de EQUIPO")]
    public Sprite partyBgUnselected;
    public Sprite partyBgSelected;

    private Dictionary<PokemonType, PokemonTypeSprite> _mapPokemonType;
    private Dictionary<ElementType, ElementTypeSprite> _mapElementType;
    private Dictionary<PrimaryStatus, Sprite> _mapStatus;

    void OnEnable()
    {
        _mapPokemonType = new Dictionary<PokemonType, PokemonTypeSprite>();
        foreach (var ts in pokemonTypeSprites) _mapPokemonType[ts.type] = ts;

        _mapElementType = new Dictionary<ElementType, ElementTypeSprite>();
        foreach (var ts in elementTypeSprites) _mapElementType[ts.type] = ts;

        _mapStatus = new Dictionary<PrimaryStatus, Sprite>();
        foreach (var ss in statusSprites) _mapStatus[ss.status] = ss.sprite;
    }

    // --- Para especie (PokemonType) ---
    public Sprite GetTypeSprite(PokemonType t)
    {
        if (t == PokemonType.None) return null;
        if (_mapPokemonType != null && _mapPokemonType.TryGetValue(t, out var ts) && ts.sprite)
            return ts.sprite;
        return null;
    }

    public Color GetTypeColor(PokemonType t)
    {
        if (t == PokemonType.None) return Color.white;
        if (_mapPokemonType != null && _mapPokemonType.TryGetValue(t, out var ts))
            return ts.color;
        return Color.white;
    }

    // --- Para movimientos (ElementType) ---
    public Sprite GetTypeSprite(ElementType t)
    {
        if (_mapElementType != null && _mapElementType.TryGetValue(t, out var ts) && ts.sprite)
            return ts.sprite;
        return null;
    }

    public Color GetTypeColor(ElementType t)
    {
        if (_mapElementType != null && _mapElementType.TryGetValue(t, out var ts))
            return ts.color;
        return Color.white;
    }

    // --- Estados primarios ---
    public Sprite GetStatusSprite(PrimaryStatus s)
    {
        if (s == PrimaryStatus.None) return null;
        if (_mapStatus != null && _mapStatus.TryGetValue(s, out var sp)) return sp;
        return null;
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
