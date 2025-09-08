// Servicios/SaveManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ---------- DTOs de guardado ----------
[Serializable] public class PartyDTO { public List<PokemonInstance> slots = new List<PokemonInstance>(6); public List<bool> occupied = new List<bool>(6); }
[Serializable] public class BoxDTO { public List<PokemonInstance> slots = new List<PokemonInstance>(30); public List<bool> occupied = new List<bool>(30); }
[Serializable] public class PCDTO { public List<BoxDTO> boxes = new List<BoxDTO>(); }

// ---------- Inventario/ajustes ----------
[Serializable]
public class PlayerSettings
{
    public bool autoSaveEnabled = true;
    [Min(0.1f)] public float autoSaveIntervalMinutes = 5f;
}

[Serializable] public class ItemStack { public ItemData item; public int amount; }
[Serializable] public class PlayerInventory { public List<ItemStack> items = new List<ItemStack>(); }

// ---------- SaveData ----------
[Serializable]
public class PlayerSaveData
{
    public PartyDTO partyDTO = new PartyDTO();
    public PCDTO pcDTO = new PCDTO();

    // Legado (lectura/inspección)
    public List<PokemonInstance> party = new List<PokemonInstance>();
    public List<List<PokemonInstance>> pcBoxes = new List<List<PokemonInstance>>();

    public PlayerSettings settings = new PlayerSettings();
    public PlayerInventory inventory = new PlayerInventory();
    public bool hasCaughtFirstPokemon = false;
}

// ---------- Save root ----------
public enum SaveRoot { PersistentDataPath, Documents }

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Archivo de guardado")]
    [SerializeField] private string fileName = "save.json";

    [Header("Destino")]
    [SerializeField] private SaveRoot saveRoot = SaveRoot.Documents;
    [SerializeField] private string documentsSubfolder = "PokemonLike";

    public PlayerSaveData Current { get; private set; } = new PlayerSaveData();

    // Directorio base según opción. Crea la carpeta si no existe.
    private string GetBaseDirectory()
    {
        if (saveRoot == SaveRoot.Documents)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) throw new Exception("MyDocuments vacío");
                string dir = Path.Combine(docs, documentsSubfolder);
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] No se pudo usar Mis Documentos. Fallback a persistentDataPath. Detalle: {e.Message}");
            }
        }

        string p = Application.persistentDataPath;
        Directory.CreateDirectory(p);
        return p;
    }

    private string FilePath => Path.Combine(GetBaseDirectory(), fileName);
    private Coroutine autosaveRoutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Asegura carpeta y traza ruta en arranque
        _ = GetBaseDirectory();
        Debug.Log($"[SaveManager] Ruta de guardado: {FilePath}");
    }

    private void Start()
    {
        Load();
        StartAutosave();
    }

    public void StartAutosave()
    {
        if (autosaveRoutine != null) StopCoroutine(autosaveRoutine);
        autosaveRoutine = StartCoroutine(AutoSaveCoroutine());
    }

    private IEnumerator AutoSaveCoroutine()
    {
        while (true)
        {
            float wait = Mathf.Max(1f, Current.settings.autoSaveIntervalMinutes * 60f);
            yield return new WaitForSeconds(wait);
            if (Current.settings.autoSaveEnabled) Save("AutoSave");
        }
    }

    public void ManualSave() => Save("ManualSave");

    private void Save(string reason)
    {
        // 1) Base
        var snapshot = new PlayerSaveData
        {
            settings = Current.settings,
            inventory = Current.inventory,
            hasCaughtFirstPokemon = PokemonStorageManager.Instance.HasCaughtFirstPokemon
        };

        // 2) PARTY -> DTO
        var party = PokemonStorageManager.Instance.PlayerParty;
        snapshot.partyDTO = new PartyDTO();
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            snapshot.partyDTO.slots.Add(p);
            snapshot.partyDTO.occupied.Add(p != null);
        }
        snapshot.party = party.ToList(); // legado

        // 3) PC -> DTO (cajas desbloqueadas)
        var pc = PokemonStorageManager.Instance.PcStorage;
        snapshot.pcDTO = new PCDTO();
        for (int b = 0; b < pc.UnlockedBoxCount; b++)
        {
            var box = pc.Boxes[b];
            var dto = new BoxDTO();
            for (int i = 0; i < box.MaxCapacity; i++)
            {
                var p = box.GetAt(i);
                dto.slots.Add(p);
                dto.occupied.Add(p != null);
            }
            snapshot.pcDTO.boxes.Add(dto);
        }
        snapshot.pcBoxes = pc.ToSave(); // legado

        Current = snapshot;

        var json = JsonUtility.ToJson(Current, true);
        File.WriteAllText(FilePath, json);
        Debug.Log($"[SaveManager] {reason} -> {FilePath}");
    }

    public void Load()
    {
        if (File.Exists(FilePath))
        {
            var json = File.ReadAllText(FilePath);
            Current = JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData();
            Debug.Log($"[SaveManager] Cargado {FilePath}");

            PokemonStorageManager.Instance.LoadFromSave(Current);
        }
        else
        {
            Current = new PlayerSaveData();
            Debug.Log("[SaveManager] No hay archivo; se creará en el primer guardado.");
            PokemonStorageManager.Instance.LoadFromSave(Current);
        }
    }

    public void ClearSave()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        Current = new PlayerSaveData();
        PokemonStorageManager.Instance.LoadFromSave(Current);
        Debug.Log("[SaveManager] Guardado eliminado y estado reiniciado.");
    }
}
