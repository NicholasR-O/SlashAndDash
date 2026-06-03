using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public static class UIScaleUtility
{
    static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);

    public static void ApplyToDocument(UIDocument document)
    {
        if (document == null || document.panelSettings == null)
            return;

        ApplyToPanelSettings(document.panelSettings, DefaultReferenceResolution);
    }

    public static void ApplyToPanelSettings(PanelSettings settings, Vector2 baseReferenceResolution)
    {
        if (settings == null)
            return;

        Vector2 scaledReference = GetScaledReferenceResolution(baseReferenceResolution);
        settings.referenceResolution = new Vector2Int(
            Mathf.RoundToInt(scaledReference.x),
            Mathf.RoundToInt(scaledReference.y));
    }

    public static void ApplyToCanvasScaler(CanvasScaler scaler, Vector2 baseReferenceResolution)
    {
        if (scaler == null)
            return;

        scaler.referenceResolution = GetScaledReferenceResolution(baseReferenceResolution);
    }

    public static Vector2 GetScaledReferenceResolution(Vector2 baseReferenceResolution)
    {
        GameOptions.EnsureLoaded();
        float scale = Mathf.Max(0.01f, GameOptions.UiScaleFactor);

        if (baseReferenceResolution.x <= 1f || baseReferenceResolution.y <= 1f)
            baseReferenceResolution = DefaultReferenceResolution;

        return new Vector2(
            Mathf.Max(1f, baseReferenceResolution.x / scale),
            Mathf.Max(1f, baseReferenceResolution.y / scale));
    }
}
