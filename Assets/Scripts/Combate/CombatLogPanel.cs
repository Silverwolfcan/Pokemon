using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CombatLogPanel : MonoBehaviour
{
    public static CombatLogPanel Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private CombatLogItemUI itemPrefab;
    [SerializeField, Min(1)] private int maxItems = 40;

    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void LogMove(PokemonInstance user, string moveName, int damage, bool isAlly, bool hit = true)
    {
        var ic = user?.species?.pokemonSprite;
        string act = hit ? $"► {moveName}" : $"► {moveName} (falló)";
        string dmg = hit && damage > 0 ? $"-{damage}" : "";
        Instance?.AddEntry(ic, user?.DisplayName ?? "?", act, dmg, isAlly);
    }

    public static void LogStatus(PokemonInstance target, string statusName, bool applied, bool isAllySource, int damage = 0)
    {
        var ic = target?.species?.pokemonSprite;
        string act = $"Estado: {statusName}" + (applied ? "" : " (falló)");
        string dmg = damage > 0 ? $"-{damage}" : "";
        Instance?.AddEntry(ic, target?.DisplayName ?? "?", act, dmg, isAllySource);
    }

    public static void LogSwitch(PokemonInstance inMon, bool isAlly)
    {
        var ic = inMon?.species?.pokemonSprite;
        Instance?.AddEntry(ic, inMon?.DisplayName ?? "?", "► Entra al combate", "", isAlly);
    }

    public static void LogCustom(Sprite icon, string monName, string text, string dmg, bool isAlly)
    {
        Instance?.AddEntry(icon, monName, text, dmg, isAlly);
    }

    public void Clear()
    {
        if (!content) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        pool.Clear();
        Canvas.ForceUpdateCanvases();
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    private void AddEntry(Sprite icon, string monName, string action, string damage, bool isAlly)
    {
        if (!itemPrefab || !content) return;

        while (content.childCount >= maxItems)
        {
            var first = content.GetChild(0).gameObject;
            first.SetActive(false);
            pool.Enqueue(first);
            first.transform.SetParent(null, false);
        }

        GameObject go = pool.Count > 0 ? pool.Dequeue() : Instantiate(itemPrefab.gameObject);
        go.transform.SetParent(content, false);
        go.SetActive(true);

        var ui = go.GetComponent<CombatLogItemUI>();
        ui.Bind(new CombatLogItemUI.Data
        {
            isAlly = isAlly,
            icon = icon,
            monName = monName,
            actionText = action,
            damageText = damage
        });

        Canvas.ForceUpdateCanvases();
        if (scroll) scroll.verticalNormalizedPosition = 0f;
    }
}
