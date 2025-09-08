using System;
using System.Reflection;
using UnityEngine;

/// Lee por reflexión la fracción de EXP hacia el siguiente nivel, si existe.
/// Evita romper compilación aunque cambie el modelo.
public static class PokemonExpAdapter
{
    public static float GetFraction(PokemonInstance p)
    {
        if (p == null) return 0f;
        try
        {
            var t = p.GetType();

            var prop = t.GetProperty("experienceToNextLevelFraction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return ToFloat(prop.GetValue(p));

            var m = t.GetMethod("GetExperienceToNextLevelFraction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m != null) return ToFloat(m.Invoke(p, null));

            var field = t.GetField("experienceToNextLevelFraction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return ToFloat(field.GetValue(p));

            // Fallback: currentExp / nextLevelExp si existen
            var cur = t.GetProperty("currentExp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(p)
                   ?? t.GetField("currentExp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(p);
            var req = t.GetProperty("nextLevelExp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(p)
                   ?? t.GetField("nextLevelExp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(p);
            if (cur != null && req != null)
            {
                float c = ToFloat(cur);
                float r = Mathf.Max(1f, ToFloat(req));
                return Mathf.Clamp01(c / r);
            }
        }
        catch { }
        return 0f;
    }

    static float ToFloat(object v)
    {
        if (v == null) return 0f;
        if (v is float f) return f;
        if (v is double d) return (float)d;
        if (v is int i) return i;
        if (v is long l) return l;
        float res; return float.TryParse(v.ToString(), out res) ? res : 0f;
    }
}
