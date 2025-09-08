using UnityEngine;

public static class Logger
{
    public static void Encounter(string msg) => Debug.Log($"[Encounter] {msg}");
    public static void Turn(string msg) => Debug.Log($"[Turn] {msg}");
    public static void Inventory(string msg) => Debug.Log($"[Inventory] {msg}");
    public static void Capture(string msg) => Debug.Log($"[Capture] {msg}");
    public static void Storage(string msg) => Debug.Log($"[Storage] {msg}");
}
