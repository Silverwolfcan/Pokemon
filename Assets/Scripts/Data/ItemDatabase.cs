using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Base de datos central de ítems. Mantén aquí la lista completa y
/// deja que el DB construya índices para accesos rápidos.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Pokemon/Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("Todos los items del juego. Puedes rellenar a mano o con el botón 'Auto-Find' del inspector.")]
    public List<ItemData> allItems = new List<ItemData>();

    // Índices en memoria (no serializados)
    [System.NonSerialized] private Dictionary<string, ItemData> _byId;
    [System.NonSerialized] private Dictionary<string, ItemData> _byName;
    [System.NonSerialized] private Dictionary<ItemCategory, List<ItemData>> _byCategory;

    private bool _indexed;

    private void OnEnable()
    {
        BuildIndex();
    }

    /// <summary>Reconstruye todos los índices en memoria y valida duplicados.</summary>
    public void BuildIndex()
    {
        _byId = new Dictionary<string, ItemData>();
        _byName = new Dictionary<string, ItemData>();
        _byCategory = new Dictionary<ItemCategory, List<ItemData>>();

        foreach (ItemData item in allItems.Where(i => i != null))
        {
            // byId
            var keyId = string.IsNullOrEmpty(item.id) ? item.GetInstanceID().ToString() : item.id;
            if (!_byId.ContainsKey(keyId)) _byId.Add(keyId, item);
            else Debug.LogWarning($"[ItemDatabase] ID duplicado: {keyId} -> {item.name} (ya existía {_byId[keyId].name})", item);

            // byName (solo como referencia/UX; NO usar para guardado)
            if (!string.IsNullOrEmpty(item.itemName))
            {
                if (!_byName.ContainsKey(item.itemName)) _byName.Add(item.itemName, item);
                else Debug.LogWarning($"[ItemDatabase] Nombre duplicado: {item.itemName}", item);
            }

            // byCategory 
            if (!_byCategory.TryGetValue(item.category, out var list))
            {
                list = new List<ItemData>();
                _byCategory[item.category] = list;
            }
            list.Add(item);
        }

        // Ordenar por categoría aplicando criterio de calidad (curativos/balls) o alfabético
        foreach (var kv in _byCategory)
        {
            var list = kv.Value;
            list.Sort((a, b) => InventorySortUtility.CompareItems(a, b));
        }

        _indexed = true;
    }

    private void EnsureIndex()
    {
        if (!_indexed) BuildIndex();
    }

    // ----------------- API de consulta -----------------

    public ItemData GetItemById(string id)
    {
        EnsureIndex();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var item) ? item : null;
    }

    public bool TryGetItemById(string id, out ItemData item)
    {
        EnsureIndex();
        return _byId.TryGetValue(id, out item);
    }

    /// <summary>Para tooling/UX. Evita usar nombres como clave en guardado.</summary>
    public ItemData GetItemByName(string itemName)
    {
        EnsureIndex();
        if (string.IsNullOrEmpty(itemName)) return null;
        return _byName.TryGetValue(itemName, out var item) ? item : null;
    }

    public List<ItemData> GetItemsByCategory(ItemCategory category)
    {
        EnsureIndex();
        return _byCategory.TryGetValue(category, out var list)
            ? new List<ItemData>(list)    // devolver copia para evitar mutaciones externas
            : new List<ItemData>();
    }

    /// <summary>Devuelve la lista pre-ordenada por categoría y calidad.</summary>
    public List<ItemData> GetItemsByCategoryOrdered(ItemCategory category) => GetItemsByCategory(category);

    // ----------------- Utilidades de mantenimiento -----------------

    /// <summary>Ordena allItems por (category, calidad) para consistencia visual en el inspector.</summary>
    public void ReorderAllByCategoryAndQuality()
    {
        allItems = allItems.Where(i => i != null).ToList();
        allItems.Sort((a, b) =>
        {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            return InventorySortUtility.CompareItems(a, b);
        });
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        BuildIndex();
    }

#if UNITY_EDITOR
    /// <summary>Auto-descubre todos los ItemData del proyecto (AssetDatabase) y rellena allItems.</summary>
    public void AutoFindAllItemsInProject()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        var items = new List<ItemData>(guids.Length);
        foreach (var g in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            var obj = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (obj) items.Add(obj);
        }
        allItems = items.Distinct().ToList();
        UnityEditor.EditorUtility.SetDirty(this);
        BuildIndex();
    }
#endif
}
