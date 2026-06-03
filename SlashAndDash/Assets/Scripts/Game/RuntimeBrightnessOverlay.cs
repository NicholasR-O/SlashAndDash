using UnityEngine;
using UnityEngine.UI;

public sealed class RuntimeBrightnessOverlay : MonoBehaviour
{
    const string OverlayName = "Runtime Brightness Overlay";
    const int OverlaySortingOrder = 32000;

    static RuntimeBrightnessOverlay instance;

    Canvas canvas;
    Image image;

    public static void SetBrightness(float brightness)
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        if (instance != null)
            instance.ApplyBrightness(brightness);
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        RuntimeBrightnessOverlay existing = FindFirstObjectByType<RuntimeBrightnessOverlay>();
        if (existing != null)
        {
            instance = existing;
            instance.BuildOverlayIfNeeded();
            DontDestroyOnLoad(instance.gameObject);
            return;
        }

        GameObject overlayObject = new GameObject(OverlayName);
        instance = overlayObject.AddComponent<RuntimeBrightnessOverlay>();
        DontDestroyOnLoad(overlayObject);
        instance.BuildOverlayIfNeeded();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlayIfNeeded();
    }

    void BuildOverlayIfNeeded()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (image != null)
            return;

        GameObject imageObject = new GameObject("Brightness Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
    }

    void ApplyBrightness(float brightness)
    {
        BuildOverlayIfNeeded();
        brightness = Mathf.Clamp(brightness, 0.5f, 1.5f);

        Color color = Color.clear;
        if (brightness < 1f)
        {
            float alpha = Mathf.InverseLerp(1f, 0.5f, brightness) * 0.55f;
            color = new Color(0f, 0f, 0f, alpha);
        }
        else if (brightness > 1f)
        {
            float alpha = Mathf.InverseLerp(1f, 1.5f, brightness) * 0.28f;
            color = new Color(1f, 1f, 1f, alpha);
        }

        image.color = color;
        canvas.enabled = color.a > 0.001f;
    }
}
