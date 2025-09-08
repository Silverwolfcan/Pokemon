using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// Muestra mensajes de captura con nº de shakes y probabilidad.
public class CaptureToast : MonoBehaviour
{
    [Header("Refs UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Aparición")]
    [SerializeField, Min(0.1f)] private float showSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;

    private Coroutine co;

    private void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnEnable() { GameEventBus.CaptureShakesComputed += OnCaptureShakes; }
    private void OnDisable() { GameEventBus.CaptureShakesComputed -= OnCaptureShakes; }

    private void OnCaptureShakes(int shakes, float chance, bool success)
    {
        string msg = success ? $"Capturado. Shakes: {shakes}" : $"Shakes: {shakes}  Prob.: {(chance * 100f):0.#}%";
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoShow(msg));
        Debug.Log($"[Capture] {msg}");
    }

    private System.Collections.IEnumerator CoShow(string txt)
    {
        if (label != null) label.text = txt;

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        float hold = showSeconds;
        while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
}
