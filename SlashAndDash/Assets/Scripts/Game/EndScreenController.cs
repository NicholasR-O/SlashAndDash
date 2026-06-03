using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
[AddComponentMenu("Game/End Screen Controller")]
public class EndScreenController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] string titleSceneName = "TitleScreen";
    [SerializeField] float fadeDuration = 0.75f;

    [Header("UI")]
    [SerializeField] Canvas screenCanvas;
    [SerializeField] Button returnButton;
    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("UI Audio")]
    [SerializeField] AudioClip[] hoverClips;
    [SerializeField] AudioClip[] clickClips;

    const string GameTitle = "CrashDash";
    const string DemoCompleteText = "DEMO COMPLETE";
    const string RootName = "Generated End Screen";

    static readonly Color HudBackground = new Color(0.018f, 0.025f, 0.034f, 1f);
    static readonly Color HudPanel = new Color(0.035f, 0.046f, 0.065f, 0.96f);
    static readonly Color HudOrange = new Color(0.93f, 0.64f, 0.2f, 1f);
    static readonly Color HudOrangeBright = new Color(1f, 0.79f, 0.32f, 1f);
    static readonly Color HudOrangePressed = new Color(0.68f, 0.42f, 0.08f, 1f);
    static readonly Color HudBlue = new Color(0.07f, 0.3f, 0.43f, 0.9f);
    static readonly Color HudBlueSoft = new Color(0.28f, 0.72f, 0.94f, 0.16f);
    static readonly Color HudTextBlue = new Color(0.56f, 0.86f, 1f, 1f);
    static readonly Color HudTextDim = new Color(0.84f, 0.91f, 0.95f, 1f);
    static readonly Color HudTextDark = new Color(0.08f, 0.065f, 0.035f, 1f);

    Font runtimeFont;
    GameObject root;
    bool loading;
    bool callbacksRegistered;
#if UNITY_EDITOR
    bool editorPreviewQueued;
#endif

    void Awake()
    {
        if (!Application.isPlaying)
        {
            QueueEditorPreviewRebuild();
            return;
        }

        Time.timeScale = 1f;
        GameState.SetPlaying();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        GameOptions.EnsureLoaded();
        RebuildUi();
        RegisterCallbacks();
        EnsureEventSystem();

        if (EventSystem.current != null && returnButton != null)
            EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
            QueueEditorPreviewRebuild();
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            UnregisterCallbacks();
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
            UnregisterCallbacks();
    }

    void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);

        if (!Application.isPlaying)
            QueueEditorPreviewRebuild();
    }

    void Update()
    {
        if (!Application.isPlaying || loading)
            return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.escapeKey.wasPressedThisFrame ||
             keyboard.backspaceKey.wasPressedThisFrame))
        {
            ReturnToTitle();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.Backspace))
        {
            ReturnToTitle();
        }
#endif
    }

    public void ReturnToTitle()
    {
        if (loading)
            return;

        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning("[EndScreenController] Title scene name is missing.", this);
            return;
        }

        loading = true;
        if (returnButton != null)
            returnButton.interactable = false;

        SceneTransitionFader.LoadScene(titleSceneName, LoadSceneMode.Single, fadeDuration);
    }

    void RebuildUi()
    {
        ResolveCanvas();
        runtimeFont = GetRuntimeFont();

        RecreateScreenCanvas();

        ConfigureCanvas(screenCanvas);
        ClearCanvasChildren(screenCanvas.transform);
        returnButton = null;

        root = CreateRoot(RootName, screenCanvas.transform);
        BuildBackground(root.transform);
        BuildEndPanel(root.transform);
    }

    void BuildBackground(Transform parent)
    {
        CreateStretchImage(parent, "Background", HudBackground);
        CreateBand(parent, "Top Finish Band", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(1920f, 76f), new Color(0.9f, 0.58f, 0.16f, 0.92f));
        CreateBand(parent, "Lower Finish Band", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1920f, 68f), HudBlue);

        for (int i = 0; i < 7; i++)
        {
            float x = -820f + i * 275f;
            Color color = i % 2 == 0
                ? new Color(0.96f, 0.83f, 0.36f, 0.16f)
                : HudBlueSoft;
            CreateBand(parent, "Track Stripe " + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(36f, 1240f), color, -18f);
        }
    }

    void BuildEndPanel(Transform parent)
    {
        Vector2 desiredPanelSize = new Vector2(760f, 430f);
        Vector2 panelSize = GetAdaptiveOverlayPanelSize(screenCanvas, desiredPanelSize, new Vector2(360f, 260f));
        GameObject panel = CreatePanel(parent, "End Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panelSize, HudPanel);
        CreateBand(panel.transform, "Panel Accent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(panelSize.x, 16f), HudOrange);
        CreateBand(panel.transform, "Panel Blue Accent", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(panelSize.x, 12f), new Color(0.27f, 0.72f, 0.94f, 0.72f));

        Vector2 viewportSize = new Vector2(Mathf.Max(280f, panelSize.x - 42f), Mathf.Max(220f, panelSize.y - 34f));
        Vector2 contentSize = new Vector2(Mathf.Max(viewportSize.x, desiredPanelSize.x - 42f), Mathf.Max(viewportSize.y, desiredPanelSize.y - 34f));
        RectTransform content = CreateScrollContent(panel.transform, "End Scroll", viewportSize, contentSize);

        CreateLabel(content, "Run Complete", GameTitle, new Vector2(0f, 126f), new Vector2(Mathf.Min(660f, contentSize.x - 60f), 86f), 58, FontStyle.Bold, HudOrangeBright, TextAnchor.MiddleCenter);
        CreateLabel(content, "Course Clear", DemoCompleteText, new Vector2(0f, 58f), new Vector2(Mathf.Min(620f, contentSize.x - 60f), 46f), 28, FontStyle.Bold, HudTextBlue, TextAnchor.MiddleCenter);
        CreateLabel(content, "Closing Line", "Thanks for playing the demo.", new Vector2(0f, 3f), new Vector2(Mathf.Min(560f, contentSize.x - 60f), 42f), 21, FontStyle.Normal, HudTextDim, TextAnchor.MiddleCenter);

        returnButton = CreateButton(content, "Return To Title Button", "RETURN TO TITLE", new Vector2(0f, -118f), new Vector2(Mathf.Min(360f, contentSize.x - 80f), 64f), HudOrange, HudTextDark);
    }

    void RegisterCallbacks()
    {
        if (callbacksRegistered)
            return;

        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToTitle);

        callbacksRegistered = true;
    }

    void UnregisterCallbacks()
    {
        if (!callbacksRegistered)
            return;

        if (returnButton != null)
            returnButton.onClick.RemoveListener(ReturnToTitle);

        callbacksRegistered = false;
    }

    void ConfigureCanvas(Canvas canvas)
    {
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.localPosition = Vector3.zero;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        if (!canvas.TryGetComponent(out GraphicRaycaster _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    Vector2 GetAdaptiveOverlayPanelSize(Canvas canvas, Vector2 desiredSize, Vector2 minimumSize)
    {
        Vector2 canvasSize = GetOverlayCanvasSize(canvas);
        Vector2 availableSize = new Vector2(
            Mathf.Max(minimumSize.x, canvasSize.x - 48f),
            Mathf.Max(minimumSize.y, canvasSize.y - 48f));

        return new Vector2(
            Mathf.Clamp(desiredSize.x, minimumSize.x, availableSize.x),
            Mathf.Clamp(desiredSize.y, minimumSize.y, availableSize.y));
    }

    Vector2 GetOverlayCanvasSize(Canvas canvas)
    {
        if (canvas != null)
        {
            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect != null && rect.rect.width > 1f && rect.rect.height > 1f)
                return rect.rect.size;
        }

        return referenceResolution;
    }

    GameObject CreateRoot(string name, Transform parent)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj;
    }

    GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panel = CreateUiObject(name, parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    RectTransform CreateScrollContent(Transform parent, string name, Vector2 viewportSize, Vector2 contentSize)
    {
        GameObject viewportObject = CreateUiObject(name, parent);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.one * 0.5f;
        viewportRect.anchorMax = Vector2.one * 0.5f;
        viewportRect.pivot = Vector2.one * 0.5f;
        viewportRect.anchoredPosition = Vector2.zero;
        viewportRect.sizeDelta = viewportSize;

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        viewportObject.AddComponent<RectMask2D>();

        ScrollRect scrollRect = viewportObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.viewport = viewportRect;

        GameObject contentObject = CreateUiObject(name + " Content", viewportObject.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.one * 0.5f;
        contentRect.anchorMax = Vector2.one * 0.5f;
        contentRect.pivot = Vector2.one * 0.5f;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = contentSize;

        scrollRect.content = contentRect;
        scrollRect.verticalScrollbar = CreateVerticalScrollbar(viewportObject.transform, name + " Scrollbar");
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalNormalizedPosition = 1f;
        return contentRect;
    }

    Scrollbar CreateVerticalScrollbar(Transform parent, string name)
    {
        GameObject scrollbarObject = CreateUiObject(name, parent);
        RectTransform rect = scrollbarObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-2f, 0f);
        rect.sizeDelta = new Vector2(10f, -8f);

        Image trackImage = scrollbarObject.AddComponent<Image>();
        trackImage.color = new Color(0.03f, 0.07f, 0.1f, 0.78f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform handle = CreateRectChild(scrollbarObject.transform, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = HudOrangeBright;

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;
        return scrollbar;
    }

    Image CreateStretchImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = CreateUiObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Image CreateBand(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color, float rotation = 0f)
    {
        GameObject band = CreateUiObject(name, parent);
        RectTransform rect = band.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = band.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject labelObject = CreateUiObject(name, parent);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = labelObject.AddComponent<Text>();
        text.text = value;
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size, Color backgroundColor, Color textColor)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = HudOrangeBright;
        colors.pressedColor = HudOrangePressed;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CreateLabel(buttonObject.transform, "Label", text, Vector2.zero, size, 22, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        AddButtonSounds(button);
        return button;
    }

    void AddButtonSounds(Button button)
    {
        if (button == null)
            return;

        CanvasUIHoverSound hoverSound = button.GetComponent<CanvasUIHoverSound>();
        if (hoverSound == null)
            hoverSound = button.gameObject.AddComponent<CanvasUIHoverSound>();

        hoverSound.Configure(hoverClips);
        button.onClick.AddListener(PlayUiClickSound);
    }

    void PlayUiClickSound()
    {
        UISoundPlayer.PlayRandomClick(clickClips);
    }

    RectTransform CreateRectChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject child = CreateUiObject(name, parent);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    void ClearCanvasChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyUiObject(parent.GetChild(i).gameObject);
    }

    void DestroyUiObject(GameObject child)
    {
        if (Application.isPlaying)
            Destroy(child);
        else
            DestroyImmediate(child);
    }

    void ResolveCanvas()
    {
        if (screenCanvas != null)
            return;

        Transform canvasTransform = transform.Find("End Screen Canvas");
        if (canvasTransform != null)
            screenCanvas = canvasTransform.GetComponent<Canvas>();
    }

    void RecreateScreenCanvas()
    {
        if (screenCanvas != null && screenCanvas.gameObject != gameObject)
            DestroyUiObject(screenCanvas.gameObject);

        GameObject canvasObject = new GameObject("End Screen Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        screenCanvas = canvasObject.GetComponent<Canvas>();
    }

    void EnsureEventSystem()
    {
        EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem activeEventSystem = EventSystem.current != null ? EventSystem.current : eventSystems.Length > 0 ? eventSystems[0] : null;

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null && eventSystem != activeEventSystem)
                DestroyUiObject(eventSystem.gameObject);
        }

        if (activeEventSystem == null)
            activeEventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
        else if (activeEventSystem.GetComponent<BaseInputModule>() == null)
            activeEventSystem.gameObject.AddComponent<StandaloneInputModule>();
    }

    Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    void QueueEditorPreviewRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || editorPreviewQueued)
            return;

        editorPreviewQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            editorPreviewQueued = false;
            if (this == null || Application.isPlaying)
                return;

            RebuildUi();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        };
#endif
    }
}
