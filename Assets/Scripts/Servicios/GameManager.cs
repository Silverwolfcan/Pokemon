using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        Ensure<SaveManager>("SaveManager");
        Ensure<PokemonStorageManager>("PokemonStorageManager");
        Ensure<DragDropController>("DragDropController");
        Debug.Log("[Bootstrap] Servicios verificados.");
    }

    private static T Ensure<T>(string goName) where T : Component
    {
        var inst = UnityEngine.Object.FindAnyObjectByType<T>();
        if (inst == null)
        {
            var go = new GameObject(goName);
            inst = go.AddComponent<T>();
        }
        DontDestroyOnLoad(inst.gameObject);
        return inst;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            SaveManager.Instance?.ManualSave();
    }
}
