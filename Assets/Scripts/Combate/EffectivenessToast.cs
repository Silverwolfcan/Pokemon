using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// Colócalo en un Canvas (Screen Space o World Space). Muestra un texto breve cuando hay efectividad especial.
public class EffectivenessToast : MonoBehaviour
{
    [Header("Refs UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Aparición")]
    [SerializeField, Min(0.1f)] private float showSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;
    [SerializeField] private Vector3 floatUp = new Vector3(0, 16f, 0);

    private RectTransform rt;
    private Coroutine co;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnEnable() { GameEventBus.EffectivenessComputed += OnEffectiveness; }
    private void OnDisable() { GameEventBus.EffectivenessComputed -= OnEffectiveness; }

    private void OnEffectiveness(PokemonInstance atk, PokemonInstance def, ElementType moveType, float mult)
    {
        string msg = null;
        if (mult <= 0f) msg = "Inmune";
        else if (mult > 1.01f) msg = "Muy eficaz";
        else if (mult < 0.99f) msg = "Poco eficaz";

        if (string.IsNullOrEmpty(msg)) return;

        Debug.Log($"[Effectiveness] {moveType} vs {def?.species?.primaryType}/{def?.species?.secondaryType} -> {mult} ({msg})");

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoShow(msg));
    }

    private IEnumerator CoShow(string txt)
    {
        if (label != null) label.text = txt;

        Vector3 startPos = rt != null ? rt.anchoredPosition3D : Vector3.zero;
        Vector3 endPos = startPos + floatUp;

        // Fade in
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeSeconds);
            if (canvasGroup != null) canvasGroup.alpha = a;
            if (rt != null) rt.anchoredPosition3D = Vector3.Lerp(startPos, endPos, a * 0.5f);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // Hold
        float hold = showSeconds;
        while (hold > 0f)
        {
            hold -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeSeconds);
            if (canvasGroup != null) canvasGroup.alpha = a;
            if (rt != null) rt.anchoredPosition3D = Vector3.Lerp(endPos, startPos, 1f - a);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (rt != null) rt.anchoredPosition3D = startPos;
    }
}
