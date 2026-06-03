using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
[AddComponentMenu("Game/Title Screen Controller")]
public class TitleScreenController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] string targetSceneName = "Level1";
    [SerializeField] Transform titleCar;

    [Header("Title UI")]
    [SerializeField] Canvas worldCanvas;
    [SerializeField] Button startButton;
    [SerializeField] Canvas optionsCanvas;
    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] Vector2 titlePanelSize = new Vector2(760f, 330f);
    [SerializeField] Vector2 titlePanelOffset = Vector2.zero;
    [SerializeField] Vector2 optionsPanelSize = new Vector2(720f, 760f);
    [SerializeField] bool previewOptionsInEditor;

    [Header("Custom Title UI Images")]
    [SerializeField] Sprite screenBackdropSprite;
    [SerializeField] Sprite titlePlateSprite;
    [SerializeField] Sprite menuPanelSprite;
    [SerializeField] Sprite buttonSprite;
    [SerializeField] Sprite optionsPanelSprite;
    [SerializeField] Sprite optionRowSprite;
    [SerializeField] Sprite toggleSprite;

    [Header("UI Audio")]
    [SerializeField] AudioClip[] hoverClips;
    [SerializeField] AudioClip[] clickClips;
    [SerializeField] AudioClip sliderClip;

    [Header("World Canvas Defaults")]
    [SerializeField] Vector3 uiPosition = new Vector3(0f, 3.35f, 3.2f);
    [SerializeField] Vector3 uiEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] Vector2 uiSize = new Vector2(860f, 360f);
    [SerializeField] float uiWorldScale = 0.006f;

    [Header("Car Drive-Off")]
    [SerializeField] GameObject wheelPrefab;
    [SerializeField] AudioClip idleEngineClip;
    [SerializeField] AudioClip driveEngineClip;
    [SerializeField] Vector3 driveDirection = Vector3.forward;
    [SerializeField] float driveDistance = 18f;
    [SerializeField] float driveDuration = 1.8f;
    [SerializeField] float wheelSpinDegreesPerSecond = 780f;
    [SerializeField] Vector3[] wheelLocalPositions =
    {
        new Vector3(-0.99f, -0.002f, 1.606f),
        new Vector3(0.99f, -0.002f, 1.61f),
        new Vector3(-0.986f, -0.002f, -1.611f),
        new Vector3(0.99f, -0.002f, -1.61f)
    };

    [Header("Transition")]
    [SerializeField] float fadeDuration = 0.85f;

    const float MinSensitivity = 0.1f;
    const float MaxSensitivity = 3f;
    const float MinFieldOfView = 55f;
    const float MaxFieldOfView = 95f;
    const float MinBrightness = 0.5f;
    const float MaxBrightness = 1.5f;

    const string GameTitle = "CrashDash";
    const string TitleRootName = "Generated Title Menu";
    const string OptionsRootName = "Generated Title Options";

    static readonly Color HudPanelColor = new Color(0.035f, 0.046f, 0.065f, 0.94f);
    static readonly Color HudPanelStrongColor = new Color(0.035f, 0.046f, 0.065f, 0.97f);
    static readonly Color HudBackdropColor = new Color(0.005f, 0.012f, 0.02f, 0.74f);
    static readonly Color HudRowColor = new Color(0.04f, 0.11f, 0.16f, 0.64f);
    static readonly Color HudTitlePlateColor = new Color(0.02f, 0.08f, 0.12f, 0.82f);
    static readonly Color HudBorderTop = new Color(1f, 1f, 1f, 0.18f);
    static readonly Color HudBorderRight = new Color(1f, 1f, 1f, 0.14f);
    static readonly Color HudBorderBlue = new Color(0.27f, 0.72f, 0.94f, 0.46f);
    static readonly Color HudBorderBlueStrong = new Color(0.27f, 0.72f, 0.94f, 0.72f);
    static readonly Color HudBorderOrange = new Color(1f, 0.86f, 0.44f, 0.72f);
    static readonly Color HudOrange = new Color(0.95f, 0.67f, 0.19f, 0.96f);
    static readonly Color HudOrangeBright = new Color(1f, 0.79f, 0.32f, 1f);
    static readonly Color HudOrangePressed = new Color(0.78f, 0.48f, 0.08f, 1f);
    static readonly Color HudBlue = new Color(0.09f, 0.2f, 0.29f, 0.96f);
    static readonly Color HudBlueBright = new Color(0.18f, 0.43f, 0.58f, 1f);
    static readonly Color HudBluePressed = new Color(0.05f, 0.12f, 0.18f, 1f);
    static readonly Color HudText = new Color(0.96f, 0.98f, 1f, 1f);
    static readonly Color HudTextDim = new Color(0.84f, 0.91f, 0.95f, 1f);
    static readonly Color HudTextBlue = new Color(0.56f, 0.86f, 1f, 1f);
    static readonly Color HudTextDark = new Color(0.11f, 0.08f, 0.03f, 1f);

    readonly List<Transform> wheels = new List<Transform>();
    Transform carRig;
    AudioSource engineSource;
    GameObject titleMenuRoot;
    GameObject optionsMenuRoot;
    Button optionsButton;
    Button quitButton;
    Button backButton;
    Slider sensitivitySlider;
    Slider fieldOfViewSlider;
    Slider masterVolumeSlider;
    Slider musicVolumeSlider;
    Slider soundEffectsVolumeSlider;
    Slider brightnessSlider;
    Toggle autoAimToggle;
    readonly Button[] uiScaleButtons = new Button[GameOptions.MaxUiScaleLevel];
    Text sensitivityValueText;
    Text fieldOfViewValueText;
    Text masterVolumeValueText;
    Text musicVolumeValueText;
    Text soundEffectsVolumeValueText;
    Text brightnessValueText;
    Font runtimeFont;
    bool loading;
    bool callbacksRegistered;
#if UNITY_EDITOR
    bool editorPreviewQueued;
#endif
    float engineBaseVolume = 0.45f;

    void Awake()
    {
        if (!Application.isPlaying)
        {
            QueueEditorPreviewRebuild();
            return;
        }

        Time.timeScale = 1f;
        GameState.SetPlaying();
        GameOptions.EnsureLoaded();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ResolveTitleCar();
        RebuildUi();
        RegisterCallbacks();
        EnsureEventSystem();
        EnsureCarRig();
        PlayIdleEngine();
        SyncTitleOptionsFromState();
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            QueueEditorPreviewRebuild();
            return;
        }

        GameOptions.Changed += SyncTitleOptionsFromState;
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            GameOptions.Changed -= SyncTitleOptionsFromState;
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
            UnregisterCallbacks();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            QueueEditorPreviewRebuild();
    }

    void Update()
    {
        if (!Application.isPlaying || loading)
            return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (IsOptionsOpen())
        {
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                ShowOptions(false);
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            StartGame();
#endif
    }

    public void StartGame()
    {
        if (loading)
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[TitleScreenController] Target scene name is missing.", this);
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    IEnumerator StartGameRoutine()
    {
        loading = true;

        if (startButton != null)
            startButton.interactable = false;
        if (optionsButton != null)
            optionsButton.interactable = false;

        if (worldCanvas != null)
            worldCanvas.enabled = false;
        if (optionsCanvas != null)
            optionsCanvas.enabled = false;

        yield return DriveCarOffRoutine();
        yield return SceneTransitionFader.FadeOut(fadeDuration);
        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    IEnumerator DriveCarOffRoutine()
    {
        EnsureCarRig();
        if (carRig == null)
            yield break;

        if (engineSource != null && driveEngineClip != null)
        {
            engineSource.clip = driveEngineClip;
            engineSource.loop = true;
            engineSource.pitch = 1f;
            engineBaseVolume = 0.85f;
            UpdateTitleEngineVolume();
            engineSource.Play();
        }

        Vector3 direction = GetCarFacingDriveDirection();
        Vector3 startPosition = carRig.position;
        Vector3 endPosition = startPosition + direction * Mathf.Max(0f, driveDistance);
        float duration = Mathf.Max(0.01f, driveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            carRig.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);
            SpinWheels(Time.unscaledDeltaTime);

            if (engineSource != null)
                engineSource.pitch = Mathf.Lerp(1f, 1.4f, t);

            yield return null;
        }

        carRig.position = endPosition;
    }

    void RebuildUi()
    {
        ResolveCanvasReferences();
        runtimeFont = GetRuntimeFont();

        bool createdWorldCanvas = worldCanvas == null;
        if (createdWorldCanvas)
        {
            GameObject canvasObject = new GameObject("World Title Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            worldCanvas = canvasObject.GetComponent<Canvas>();
        }

        ConfigureWorldCanvas(worldCanvas, createdWorldCanvas);
        ClearCanvasChildren(worldCanvas.transform);
        ResetMainMenuReferences();
        BuildWorldTitleMenu();

        RecreateOptionsCanvas();

        ConfigureOptionsCanvas(optionsCanvas);
        ClearCanvasChildren(optionsCanvas.transform);
        ResetOptionsReferences();
        BuildOptionsMenu();

        bool showOptions = !Application.isPlaying && previewOptionsInEditor;
        SetOptionsVisible(showOptions);
    }

    void BuildWorldTitleMenu()
    {
        Vector2 canvasSize = GetWorldCanvasSize();
        Vector2 panelSize = GetTitlePanelSize(canvasSize);

        titleMenuRoot = CreateRoot(TitleRootName, worldCanvas.transform, active: true);
        GameObject panel = CreatePanel(
            titleMenuRoot.transform,
            "Title Background",
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            titlePanelOffset,
            panelSize,
            menuPanelSprite,
            HudPanelColor);
        CreateHudFrame(panel.transform, panelSize, "Title");

        Vector2 titlePlateSize = new Vector2(panelSize.x - 76f, 86f);
        Image titlePlate = CreateImage(panel.transform, "Title Plate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 94f), titlePlateSize, titlePlateSprite, titlePlateSprite != null ? Color.white : HudTitlePlateColor);
        CreateHudFrame(titlePlate.transform, titlePlateSize, "Title Plate");
        CreateDecorativeBlock(titlePlate.transform, "Title Plate Blue Underline", new Vector2(0f, -titlePlateSize.y * 0.5f + 2f), new Vector2(titlePlateSize.x - 18f, 3f), HudBorderBlueStrong);
        CreateLabel(panel.transform, "Title Text", GameTitle, new Vector2(0f, 94f), new Vector2(panelSize.x - 92f, 82f), 54, FontStyle.Bold, HudOrangeBright, TextAnchor.MiddleCenter);

        float buttonWidth = Mathf.Min(430f, panelSize.x - 120f);
        startButton = CreateMenuButton(panel.transform, "Start Button", "START", new Vector2(0f, 24f), new Vector2(buttonWidth, 52f), HudOrange, HudTextDark, true);
        optionsButton = CreateMenuButton(panel.transform, "Options Button", "OPTIONS", new Vector2(0f, -44f), new Vector2(buttonWidth, 52f), HudBlue, HudText, false);
        quitButton = CreateMenuButton(panel.transform, "Quit Button", "QUIT", new Vector2(0f, -112f), new Vector2(buttonWidth, 52f), new Color(0.07f, 0.11f, 0.16f, 0.96f), HudText, false);
    }

    void BuildOptionsMenu()
    {
        optionsMenuRoot = CreateRoot(OptionsRootName, optionsCanvas.transform, active: false);
        CreateImage(optionsMenuRoot.transform, "Options Dimmer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, screenBackdropSprite, screenBackdropSprite != null ? new Color(1f, 1f, 1f, 0.22f) : HudBackdropColor);

        Vector2 desiredPanelSize = new Vector2(Mathf.Max(620f, optionsPanelSize.x), Mathf.Max(790f, optionsPanelSize.y));
        Vector2 panelSize = GetAdaptiveOverlayPanelSize(optionsCanvas, desiredPanelSize, new Vector2(460f, 340f));
        GameObject panel = CreatePanel(
            optionsMenuRoot.transform,
            "Options Panel",
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.one * 0.5f,
            Vector2.zero,
            panelSize,
            optionsPanelSprite != null ? optionsPanelSprite : menuPanelSprite,
            HudPanelStrongColor);
        CreateHudFrame(panel.transform, panelSize, "Options");

        Vector2 viewportSize = new Vector2(Mathf.Max(280f, panelSize.x - 48f), Mathf.Max(260f, panelSize.y - 48f));
        Vector2 contentSize = new Vector2(Mathf.Max(viewportSize.x, desiredPanelSize.x - 48f), Mathf.Max(viewportSize.y, desiredPanelSize.y - 48f));
        RectTransform content = CreateScrollContent(panel.transform, "Options Scroll", viewportSize, contentSize);

        float rowWidth = Mathf.Max(320f, contentSize.x - 28f);
        CreateLabel(content, "Options Title", "Options", new Vector2(0f, 326f), new Vector2(rowWidth, 54f), 38, FontStyle.Bold, HudText, TextAnchor.MiddleLeft);
        CreateDecorativeBlock(content, "Options Header Accent", new Vector2(-rowWidth * 0.5f + 1.5f, 297f), new Vector2(3f, 34f), HudBorderOrange);
        CreateDecorativeBlock(content, "Options Header Underline", new Vector2(0f, 297f), new Vector2(rowWidth, 1f), HudBorderBlue);

        sensitivitySlider = CreateSliderRow(content, "Sensitivity", new Vector2(0f, 248f), rowWidth, MinSensitivity, MaxSensitivity, GameOptions.Sensitivity, FormatDecimal, OnSensitivityChanged, out sensitivityValueText);
        fieldOfViewSlider = CreateSliderRow(content, "FOV", new Vector2(0f, 192f), rowWidth, MinFieldOfView, MaxFieldOfView, GameOptions.FieldOfView, FormatWholeNumber, OnFieldOfViewChanged, out fieldOfViewValueText);
        brightnessSlider = CreateSliderRow(content, "Brightness", new Vector2(0f, 136f), rowWidth, MinBrightness, MaxBrightness, GameOptions.Brightness, FormatMultiplier, OnBrightnessChanged, out brightnessValueText);
        CreateUiScaleButtonRow(content, "UI Scale", new Vector2(0f, 80f), rowWidth);
        autoAimToggle = CreateToggleRow(content, "Auto-Aim", new Vector2(0f, 24f), rowWidth, GameOptions.AutoAim, OnAutoAimChanged);

        CreateLabel(content, "Volume Label", "Volume", new Vector2(0f, -32f), new Vector2(rowWidth, 28f), 17, FontStyle.Bold, HudOrangeBright, TextAnchor.MiddleLeft);
        masterVolumeSlider = CreateSliderRow(content, "Master", new Vector2(0f, -88f), rowWidth, 0f, 1f, GameOptions.MasterVolume, FormatPercent, OnMasterVolumeChanged, out masterVolumeValueText);
        musicVolumeSlider = CreateSliderRow(content, "Music", new Vector2(0f, -144f), rowWidth, 0f, 1f, GameOptions.MusicVolume, FormatPercent, OnMusicVolumeChanged, out musicVolumeValueText);
        soundEffectsVolumeSlider = CreateSliderRow(content, "Sound Effects", new Vector2(0f, -200f), rowWidth, 0f, 1f, GameOptions.SoundEffectsVolume, FormatPercent, OnSoundEffectsVolumeChanged, out soundEffectsVolumeValueText);

        backButton = CreateMenuButton(content, "Back Button", "BACK", new Vector2(0f, -336f), new Vector2(220f, 48f), HudBlue, HudText, false);
    }

    void ConfigureWorldCanvas(Canvas canvas, bool applyDefaultTransform)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect != null && applyDefaultTransform)
        {
            rect.SetPositionAndRotation(uiPosition, Quaternion.Euler(uiEulerAngles));
            rect.localScale = Vector3.one * Mathf.Max(0.0001f, uiWorldScale);
            rect.sizeDelta = GetWorldCanvasSize();
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        if (!canvas.TryGetComponent(out GraphicRaycaster _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    void ConfigureOptionsCanvas(Canvas canvas)
    {
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
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

    Vector2 GetWorldCanvasSize()
    {
        if (worldCanvas != null)
        {
            RectTransform rect = worldCanvas.GetComponent<RectTransform>();
            if (rect != null && rect.sizeDelta.x > 1f && rect.sizeDelta.y > 1f)
                return rect.sizeDelta;
        }

        return new Vector2(Mathf.Max(860f, uiSize.x), Mathf.Max(360f, uiSize.y));
    }

    Vector2 GetTitlePanelSize(Vector2 canvasSize)
    {
        return new Vector2(
            Mathf.Clamp(titlePanelSize.x, 640f, Mathf.Max(640f, canvasSize.x - 28f)),
            Mathf.Clamp(titlePanelSize.y, 320f, Mathf.Max(320f, canvasSize.y - 20f)));
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

    void RegisterCallbacks()
    {
        if (callbacksRegistered)
            return;

        if (startButton != null)
            startButton.onClick.AddListener(StartGame);
        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        callbacksRegistered = true;
    }

    void UnregisterCallbacks()
    {
        if (!callbacksRegistered)
            return;

        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);
        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(OnOptionsClicked);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);

        callbacksRegistered = false;
    }

    void SyncTitleOptionsFromState()
    {
        GameOptions.EnsureLoaded();
        SetSliderValue(sensitivitySlider, GameOptions.Sensitivity, sensitivityValueText, FormatDecimal);
        SetSliderValue(fieldOfViewSlider, GameOptions.FieldOfView, fieldOfViewValueText, FormatWholeNumber);
        SetSliderValue(masterVolumeSlider, GameOptions.MasterVolume, masterVolumeValueText, FormatPercent);
        SetSliderValue(musicVolumeSlider, GameOptions.MusicVolume, musicVolumeValueText, FormatPercent);
        SetSliderValue(soundEffectsVolumeSlider, GameOptions.SoundEffectsVolume, soundEffectsVolumeValueText, FormatPercent);
        SetSliderValue(brightnessSlider, GameOptions.Brightness, brightnessValueText, FormatMultiplier);
        UpdateUiScaleButtons(GameOptions.UiScaleLevel);

        if (autoAimToggle != null)
            autoAimToggle.SetIsOnWithoutNotify(GameOptions.AutoAim);

        GameOptions.ApplyRuntimeSettings();
        UpdateTitleEngineVolume();
    }

    void OnOptionsClicked()
    {
        ShowOptions(true);
    }

    void OnBackClicked()
    {
        ShowOptions(false);
    }

    void ShowOptions(bool visible)
    {
        SetOptionsVisible(visible);

        if (EventSystem.current != null)
        {
            GameObject selected = visible && backButton != null
                ? backButton.gameObject
                : startButton != null ? startButton.gameObject : null;
            EventSystem.current.SetSelectedGameObject(selected);
        }
    }

    void SetOptionsVisible(bool visible)
    {
        if (titleMenuRoot != null)
            titleMenuRoot.SetActive(!visible);
        if (worldCanvas != null)
            worldCanvas.enabled = true;
        if (optionsCanvas != null)
        {
            optionsCanvas.gameObject.SetActive(true);
            optionsCanvas.enabled = visible;
        }
        if (optionsMenuRoot != null)
            optionsMenuRoot.SetActive(visible);
    }

    bool IsOptionsOpen()
    {
        return optionsCanvas != null && optionsCanvas.enabled && optionsMenuRoot != null && optionsMenuRoot.activeSelf;
    }

    void OnSensitivityChanged(float value)
    {
        GameOptions.SetSensitivity(value);
        SetValueText(sensitivityValueText, FormatDecimal(value));
    }

    void OnFieldOfViewChanged(float value)
    {
        GameOptions.SetFieldOfView(value);
        SetValueText(fieldOfViewValueText, FormatWholeNumber(value));
    }

    void OnMasterVolumeChanged(float value)
    {
        GameOptions.SetMasterVolume(value);
        SetValueText(masterVolumeValueText, FormatPercent(value));
    }

    void OnMusicVolumeChanged(float value)
    {
        GameOptions.SetMusicVolume(value);
        SetValueText(musicVolumeValueText, FormatPercent(value));
    }

    void OnSoundEffectsVolumeChanged(float value)
    {
        GameOptions.SetSoundEffectsVolume(value);
        SetValueText(soundEffectsVolumeValueText, FormatPercent(value));
        UpdateTitleEngineVolume();
    }

    void OnBrightnessChanged(float value)
    {
        GameOptions.SetBrightness(value);
        SetValueText(brightnessValueText, FormatMultiplier(value));
    }

    void OnAutoAimChanged(bool value)
    {
        GameOptions.SetAutoAim(value);
    }

    static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    GameObject CreateRoot(string name, Transform parent, bool active)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.SetActive(active);
        return root;
    }

    GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Sprite sprite, Color color)
    {
        GameObject panel = CreateUIObject(name, parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    RectTransform CreateScrollContent(Transform parent, string name, Vector2 viewportSize, Vector2 contentSize)
    {
        GameObject viewportObject = CreateUIObject(name, parent);
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

        GameObject contentObject = CreateUIObject(name + " Content", viewportObject.transform);
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
        GameObject scrollbarObject = CreateUIObject(name, parent);
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

    Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Sprite sprite, Color color)
    {
        GameObject imageObject = CreateUIObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
        {
            rect.offsetMin = anchoredPosition;
            rect.offsetMax = size;
        }

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject labelObject = CreateUIObject(name, parent);
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

    Button CreateMenuButton(Transform parent, string name, string text, Vector2 position, Vector2 size, Color backgroundColor, Color textColor, bool primary)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = backgroundColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = primary ? HudOrangeBright : HudBlueBright;
        colors.pressedColor = primary ? HudOrangePressed : HudBluePressed;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.48f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CreateButtonFrame(buttonObject.transform, size, primary);
        CreateLabel(buttonObject.transform, "Label", text, Vector2.zero, size, Mathf.RoundToInt(size.y * 0.38f), FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        AddButtonSounds(button);
        return button;
    }

    Slider CreateSliderRow(Transform parent, string label, Vector2 position, float width, float min, float max, float value, Func<float, string> formatter, Action<float> onValueChanged, out Text valueText)
    {
        GameObject row = CreateOptionRow(parent, label, position, new Vector2(width, 44f));
        CreateLabel(row.transform, label + " Label", label, new Vector2(-width * 0.5f + 88f, 0f), new Vector2(170f, 34f), 16, FontStyle.Bold, HudTextDim, TextAnchor.MiddleLeft);

        Slider slider = CreateSlider(row.transform, label + " Slider", new Vector2(44f, 0f), new Vector2(width - 270f, 28f), min, max, value);
        slider.onValueChanged.AddListener(newValue => onValueChanged?.Invoke(newValue));
        AddSliderSounds(slider);

        valueText = CreateLabel(row.transform, label + " Value", formatter(value), new Vector2(width * 0.5f - 48f, 0f), new Vector2(82f, 34f), 15, FontStyle.Bold, HudTextBlue, TextAnchor.MiddleRight);
        return slider;
    }

    void CreateUiScaleButtonRow(Transform parent, string label, Vector2 position, float width)
    {
        GameObject row = CreateOptionRow(parent, label, position, new Vector2(width, 44f));
        CreateLabel(row.transform, label + " Label", label, new Vector2(-width * 0.5f + 88f, 0f), new Vector2(170f, 34f), 16, FontStyle.Bold, HudTextDim, TextAnchor.MiddleLeft);

        const float buttonWidth = 44f;
        const float buttonHeight = 34f;
        const float gap = 8f;
        float totalWidth = (buttonWidth * GameOptions.MaxUiScaleLevel) + (gap * (GameOptions.MaxUiScaleLevel - 1));
        float startX = width * 0.5f - 32f - totalWidth + buttonWidth * 0.5f;

        for (int i = 0; i < uiScaleButtons.Length; i++)
        {
            int level = i + 1;
            Vector2 buttonPosition = new Vector2(startX + i * (buttonWidth + gap), 0f);
            Button button = CreateMenuButton(row.transform, label + " " + level + " Button", level.ToString(), buttonPosition, new Vector2(buttonWidth, buttonHeight), HudBlue, HudText, false);
            button.onClick.AddListener(() => OnUiScaleButtonClicked(level));
            uiScaleButtons[i] = button;
        }

        UpdateUiScaleButtons(GameOptions.UiScaleLevel);
    }

    Toggle CreateToggleRow(Transform parent, string label, Vector2 position, float width, bool value, Action<bool> onValueChanged)
    {
        GameObject row = CreateOptionRow(parent, label, position, new Vector2(width, 44f));
        CreateLabel(row.transform, label + " Label", label, new Vector2(-width * 0.5f + 88f, 0f), new Vector2(170f, 34f), 16, FontStyle.Bold, HudTextDim, TextAnchor.MiddleLeft);
        Toggle toggle = CreateToggle(row.transform, label + " Toggle", new Vector2(width * 0.5f - 40f, 0f), value);
        toggle.onValueChanged.AddListener(newValue => onValueChanged?.Invoke(newValue));
        AddToggleSounds(toggle);
        return toggle;
    }

    GameObject CreateOptionRow(Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject row = CreatePanel(parent, label + " Row", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size, optionRowSprite, optionRowSprite != null ? Color.white : HudRowColor);
        CreateRowFrame(row.transform, size);
        return row;
    }

    Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size, float min, float max, float value)
    {
        GameObject sliderObject = CreateUIObject(name, parent);
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image hitTarget = sliderObject.AddComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform track = CreateRectChild(sliderObject.transform, "Track", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(size.x, 8f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.04f, 0.09f, 0.14f, 0.94f);
        trackImage.raycastTarget = false;

        RectTransform fillArea = CreateRectChild(sliderObject.transform, "Fill Area", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(size.x, 8f));
        RectTransform fill = CreateRectChild(fillArea, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = HudOrange;
        fillImage.raycastTarget = false;

        RectTransform handleArea = CreateRectChild(sliderObject.transform, "Handle Slide Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform handle = CreateRectChild(handleArea, "Handle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = HudOrangeBright;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        return slider;
    }

    Toggle CreateToggle(Transform parent, string name, Vector2 position, bool value)
    {
        GameObject toggleObject = CreateUIObject(name, parent);
        RectTransform rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(68f, 30f);

        Image background = toggleObject.AddComponent<Image>();
        background.sprite = toggleSprite;
        background.type = toggleSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = HudBlue;

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.isOn = value;
        toggle.targetGraphic = background;

        RectTransform checkRect = CreateRectChild(toggleObject.transform, "Checkmark", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(22f, 22f));
        Image checkImage = checkRect.gameObject.AddComponent<Image>();
        checkImage.color = HudOrangeBright;
        toggle.graphic = checkImage;
        CreateButtonFrame(toggleObject.transform, rect.sizeDelta, primary: false);
        return toggle;
    }

    void AddButtonSounds(Button button)
    {
        if (button == null)
            return;

        AddHoverSound(button.gameObject);
        button.onClick.AddListener(PlayUiClickSound);
    }

    void AddSliderSounds(Slider slider)
    {
        if (slider == null)
            return;

        AddHoverSound(slider.gameObject);
        slider.onValueChanged.AddListener(_ => UISoundPlayer.PlaySlider(sliderClip));
    }

    void AddToggleSounds(Toggle toggle)
    {
        if (toggle == null)
            return;

        AddHoverSound(toggle.gameObject);
        toggle.onValueChanged.AddListener(_ => UISoundPlayer.PlayRandomClick(clickClips));
    }

    void AddHoverSound(GameObject target)
    {
        if (target == null)
            return;

        CanvasUIHoverSound hoverSound = target.GetComponent<CanvasUIHoverSound>();
        if (hoverSound == null)
            hoverSound = target.AddComponent<CanvasUIHoverSound>();

        hoverSound.Configure(hoverClips);
    }

    void PlayUiClickSound()
    {
        UISoundPlayer.PlayRandomClick(clickClips);
    }

    void CreateHudFrame(Transform parent, Vector2 size, string prefix)
    {
        CreateDecorativeBlock(parent, prefix + " Top Border", new Vector2(0f, size.y * 0.5f - 0.5f), new Vector2(size.x, 1f), HudBorderTop);
        CreateDecorativeBlock(parent, prefix + " Right Border", new Vector2(size.x * 0.5f - 0.5f, 0f), new Vector2(1f, size.y), HudBorderRight);
        CreateDecorativeBlock(parent, prefix + " Bottom Border", new Vector2(0f, -size.y * 0.5f + 0.5f), new Vector2(size.x, 1f), HudBorderBlue);
        CreateDecorativeBlock(parent, prefix + " Left Accent", new Vector2(-size.x * 0.5f + 1.5f, 0f), new Vector2(3f, size.y), HudBorderOrange);
    }

    void CreateRowFrame(Transform parent, Vector2 size)
    {
        CreateDecorativeBlock(parent, "Row Left Accent", new Vector2(-size.x * 0.5f + 1.5f, 0f), new Vector2(3f, size.y), HudBorderBlueStrong);
        CreateDecorativeBlock(parent, "Row Top Border", new Vector2(0f, size.y * 0.5f - 0.5f), new Vector2(size.x, 1f), new Color(1f, 1f, 1f, 0.06f));
        CreateDecorativeBlock(parent, "Row Bottom Border", new Vector2(0f, -size.y * 0.5f + 0.5f), new Vector2(size.x, 1f), new Color(0.27f, 0.72f, 0.94f, 0.18f));
    }

    void CreateButtonFrame(Transform parent, Vector2 size, bool primary)
    {
        Color leftAccent = primary ? HudBorderOrange : HudBorderBlueStrong;
        Color bottomAccent = primary ? new Color(0.27f, 0.72f, 0.94f, 0.36f) : new Color(1f, 0.86f, 0.44f, 0.2f);
        CreateDecorativeBlock(parent, "Button Top Highlight", new Vector2(0f, size.y * 0.5f - 0.5f), new Vector2(size.x, 1f), new Color(1f, 1f, 1f, 0.18f));
        CreateDecorativeBlock(parent, "Button Left Accent", new Vector2(-size.x * 0.5f + 1.5f, 0f), new Vector2(3f, size.y), leftAccent);
        CreateDecorativeBlock(parent, "Button Bottom Accent", new Vector2(0f, -size.y * 0.5f + 1f), new Vector2(size.x - 6f, 2f), bottomAccent);
    }

    Image CreateDecorativeBlock(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRectChild(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    RectTransform CreateRectChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject child = CreateUIObject(name, parent);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    void ClearCanvasChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            DestroyUiObject(child);
        }
    }

    void DestroyUiObject(GameObject child)
    {
        if (Application.isPlaying)
            Destroy(child);
        else
            DestroyImmediate(child);
    }

    void ResetMainMenuReferences()
    {
        titleMenuRoot = null;
        startButton = null;
        optionsButton = null;
        quitButton = null;
    }

    void ResetOptionsReferences()
    {
        optionsMenuRoot = null;
        backButton = null;
        sensitivitySlider = null;
        fieldOfViewSlider = null;
        masterVolumeSlider = null;
        musicVolumeSlider = null;
        soundEffectsVolumeSlider = null;
        brightnessSlider = null;
        for (int i = 0; i < uiScaleButtons.Length; i++)
            uiScaleButtons[i] = null;
        autoAimToggle = null;
        sensitivityValueText = null;
        fieldOfViewValueText = null;
        masterVolumeValueText = null;
        musicVolumeValueText = null;
        soundEffectsVolumeValueText = null;
        brightnessValueText = null;
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

    void ResolveCanvasReferences()
    {
        if (worldCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("World Title Canvas");
            if (canvasObject != null)
                worldCanvas = canvasObject.GetComponent<Canvas>();
        }

        if (optionsCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("Screen Title Options Canvas");
            if (canvasObject != null)
                optionsCanvas = canvasObject.GetComponent<Canvas>();
        }
    }

    void RecreateOptionsCanvas()
    {
        if (optionsCanvas != null && optionsCanvas != worldCanvas)
            DestroyUiObject(optionsCanvas.gameObject);

        optionsCanvas = new GameObject("Screen Title Options Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
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

    void ResolveTitleCar()
    {
        if (titleCar != null)
            return;

        GameObject carObject = GameObject.Find("Title Car");
        if (carObject != null)
            titleCar = carObject.transform;
    }

    Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    static void SetSliderValue(Slider slider, float value, Text valueText, Func<float, string> formatter)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);
        SetValueText(valueText, formatter(value));
    }

    static void SetValueText(Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    static string FormatDecimal(float value)
    {
        return value.ToString("0.00");
    }

    static string FormatWholeNumber(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }

    static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    static string FormatMultiplier(float value)
    {
        return value.ToString("0.00") + "x";
    }

    void OnUiScaleButtonClicked(int level)
    {
        level = GameOptions.ClampUiScaleLevel(level);
        GameOptions.SetUiScaleLevel(level);
        UpdateUiScaleButtons(level);
    }

    void UpdateUiScaleButtons(int selectedLevel)
    {
        selectedLevel = GameOptions.ClampUiScaleLevel(selectedLevel);

        for (int i = 0; i < uiScaleButtons.Length; i++)
        {
            Button button = uiScaleButtons[i];
            if (button == null)
                continue;

            bool selected = i + 1 == selectedLevel;
            Color backgroundColor = selected ? HudOrange : HudBlue;
            Color textColor = selected ? HudTextDark : HudText;

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = backgroundColor;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = textColor;

            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = selected ? HudOrangeBright : HudBlueBright;
            colors.pressedColor = selected ? HudOrangePressed : HudBluePressed;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }
    }

    void EnsureCarRig()
    {
        ResolveTitleCar();
        if (titleCar == null || carRig != null)
            return;

        GameObject rigObject = new GameObject("Title Car Rig");
        carRig = rigObject.transform;
        carRig.SetPositionAndRotation(titleCar.position, GetCarFacingDriveRotation());
        titleCar.SetParent(carRig, true);

        engineSource = rigObject.AddComponent<AudioSource>();
        engineSource.playOnAwake = false;
        engineSource.loop = true;
        engineSource.spatialBlend = 0.72f;
        engineSource.minDistance = 2f;
        engineSource.maxDistance = 28f;

        CreateWheels();
    }

    Vector3 GetCarFacingDriveDirection()
    {
        Vector3 direction = Vector3.zero;

        if (titleCar != null)
            direction = GetBestPlanarFacingAxis(titleCar);
        if (direction.sqrMagnitude <= 0.001f && carRig != null)
            direction = Vector3.ProjectOnPlane(carRig.forward, Vector3.up);
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector3.ProjectOnPlane(-driveDirection, Vector3.up);
        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    Vector3 GetBestPlanarFacingAxis(Transform source)
    {
        Vector3 reference = Vector3.ProjectOnPlane(-driveDirection, Vector3.up);
        if (reference.sqrMagnitude <= 0.001f)
            reference = Vector3.forward;
        reference.Normalize();

        Vector3[] candidates =
        {
            source.forward,
            -source.forward,
            source.up,
            -source.up,
            source.right,
            -source.right
        };

        Vector3 best = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 planar = Vector3.ProjectOnPlane(candidates[i], Vector3.up);
            float planarLength = planar.magnitude;
            if (planarLength <= 0.001f)
                continue;

            Vector3 normalized = planar / planarLength;
            float score = planarLength * 2f + Vector3.Dot(normalized, reference);
            if (score > bestScore)
            {
                bestScore = score;
                best = normalized;
            }
        }

        return best;
    }

    Quaternion GetCarFacingDriveRotation()
    {
        return Quaternion.LookRotation(GetCarFacingDriveDirection(), Vector3.up);
    }

    void CreateWheels()
    {
        wheels.Clear();

        for (int i = 0; i < wheelLocalPositions.Length; i++)
        {
            Transform wheel = CreateWheel(i);
            if (wheel != null)
                wheels.Add(wheel);
        }
    }

    Transform CreateWheel(int index)
    {
        GameObject wheelObject = wheelPrefab != null
            ? Instantiate(wheelPrefab, carRig)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        wheelObject.name = "Title Wheel " + (index + 1);
        Transform wheel = wheelObject.transform;
        wheel.SetParent(carRig, false);
        wheel.localPosition = wheelLocalPositions[index];
        wheel.localRotation = index < 2
            ? Quaternion.Euler(-90f, 0f, 180f)
            : Quaternion.Euler(-90f, 0f, 0f);

        if (wheelPrefab == null)
            wheel.localScale = new Vector3(0.28f, 0.12f, 0.28f);

        return wheel;
    }

    void SpinWheels(float deltaTime)
    {
        float amount = wheelSpinDegreesPerSecond * deltaTime;
        for (int i = 0; i < wheels.Count; i++)
        {
            if (wheels[i] != null)
                wheels[i].Rotate(Vector3.right, amount, Space.Self);
        }
    }

    void PlayIdleEngine()
    {
        if (engineSource == null || idleEngineClip == null)
            return;

        engineSource.clip = idleEngineClip;
        engineSource.loop = true;
        engineBaseVolume = 0.45f;
        UpdateTitleEngineVolume();
        engineSource.pitch = 1f;
        engineSource.Play();
    }

    void UpdateTitleEngineVolume()
    {
        if (engineSource != null)
            engineSource.volume = Mathf.Clamp01(engineBaseVolume * GameOptions.SoundEffectsVolume);
    }
}
