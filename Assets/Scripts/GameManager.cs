using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        Ensure<SaveManager>("SaveManager");
        Ensure<PokemonStorageManager>("PokemonStorageManager");
        Ensure<DragDropController>("DragDropController");
    }

    private static T Ensure<T>(string goName) where T : Component
    {
        var inst = FindObjectOfType<T>();
        if (inst == null)
        {
            var go = new GameObject(goName);
            inst = go.AddComponent<T>();
        }
        return inst;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            SaveManager.Instance?.ManualSave();
    }
}
