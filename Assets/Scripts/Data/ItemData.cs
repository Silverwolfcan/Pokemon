using UnityEngine;

public enum ItemCategory
{
    // EXISTENTES (compatibilidad con tus assets actuales)
    Pokeball = 0,
    Healing = 1,  // Botiquín
    Battle = 2,  // Objetos de combate
    KeyItem = 3,  // Objetos clave

    // NUEVAS (para las 8 pestañas estilo Pokémon; puedes usarlas ya)
    Berries = 10,
    GeneralGoods = 11, // Objetos varios
    TM = 12, // MTs
    Treasure = 13  // Tesoros
}

public abstract class ItemData : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("ID estable para guardado y referencias. Se autogenera desde el GUID del asset.")]
    public string id;

    [Header("Presentación")]
    public string itemName;
    public Sprite icon;
    public ItemCategory category;
    [TextArea] public string description;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Autorellenar ID con el GUID del asset si está vacío
        if (string.IsNullOrEmpty(id))
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    id = guid;
            }
        }
    }
#endif
}
