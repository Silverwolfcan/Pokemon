using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
class TeamData
{
    public List<PokemonInstance> team;
}

public static class SaveSystem
{
    private const string FileName = "playerTeam.json";
    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void SaveTeam(List<PokemonInstance> team)
    {
        TeamData data = new TeamData { team = team };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
        Debug.Log($"[SaveSystem] Equipo guardado en {FilePath}");
    }

    public static List<PokemonInstance> LoadTeam()
    {
        if (!File.Exists(FilePath))
        {
            Debug.Log("[SaveSystem] No existe archivo de guardado, devolviendo lista vacía.");
            return new List<PokemonInstance>();
        }

        string json = File.ReadAllText(FilePath);
        TeamData data = JsonUtility.FromJson<TeamData>(json);
        Debug.Log($"[SaveSystem] Equipo cargado desde {FilePath} (count={data.team.Count})");
        return data.team;
    }

    public static void ClearData()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Debug.Log($"[SaveSystem] Archivo de guardado borrado: {FilePath}");
        }
    }
}
