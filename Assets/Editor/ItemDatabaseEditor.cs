#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var db = (ItemDatabase)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Herramientas", EditorStyles.boldLabel);

        if (GUILayout.Button("Reindex & Validar"))
        {
            db.BuildIndex();
            Debug.Log("[ItemDatabase] Índices reconstruidos y categorías ordenadas.");
        }

        if (GUILayout.Button("Auto-Find (buscar todos los ItemData del proyecto)"))
        {
            db.AutoFindAllItemsInProject();
            Debug.Log("[ItemDatabase] allItems repoblado desde el proyecto.");
        }

        if (GUILayout.Button("Reordenar allItems por Categoría y Calidad"))
        {
            db.ReorderAllByCategoryAndQuality();
            Debug.Log("[ItemDatabase] Lista principal reordenada.");
        }

        EditorGUILayout.HelpBox(
            "- 'Auto-Find' pobla la lista con todos los ScriptableObjects ItemData.\n" +
            "- 'Reindex' reconstruye diccionarios y ordena por categoría.\n" +
            "- 'Reordenar' ordena la lista en el inspector para inspección consistente.",
            MessageType.Info);
    }
}
#endif
