using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class OptionsMenuScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;
    [SerializeField] private CarController player;

    [Header("Sensitivity")]
    [SerializeField] private float minSensitivity = 0.1f;
    [SerializeField] private float maxSensitivity = 3f;

    private const string OptionsRootName = "options-root";
    private const string DebugRootName = "debug-root";
    private const string GameOverRootName = "game-over-root";
    private const string SensitivitySliderName = "sensitivity-slider";
    private const string SensitivityValueLabelName = "sensitivity-value";
    private const string GodModeToggleName = "debug-god-mode-toggle";
    private const string NoClipToggleName = "debug-no-clip-toggle";
    private const string DisableAiToggleName = "debug-disable-ai-toggle";
    private const string ShowSuspensionToggleName = "debug-show-suspension-toggle";
    private const string ShowNormalsToggleName = "debug-show-normals-toggle";
    private const string AlwaysBoostToggleName = "debug-always-boost-toggle";
    private const string QuitButtonName = "quit-button";

    private VisualElement uiRoot;
    private VisualElement optionsRoot;
    private VisualElement debugRoot;
    private VisualElement gameOverRoot;
    private Slider sensitivitySlider;
    private Label sensitivityValueLabel;
    private Toggle godModeToggle;
    private Toggle noClipToggle;
    private Toggle disableAiToggle;
    private Toggle showSuspensionToggle;
    private Toggle showNormalsToggle;
    private Toggle alwaysBoostToggle;
    private Button quitButton;

    private bool sliderCallbackRegistered;
    private bool debugCallbacksRegistered;
    private bool quitCallbackRegistered;

    public bool IsVisible { get; private set; }
    public bool IsDebugVisible { get; private set; }
    public bool IsGameOverVisible { get; private set; }

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        TryInitializeUi();
        SetVisible(false);
        SetDebugVisible(false);
        SetGameOverVisible(false);
    }

    private void OnEnable()
    {
        TryInitializeUi();
        RegisterCallbacks();
        ResolveCameraIfNeeded();
        ResolvePlayerIfNeeded();
        SyncFromCamera();
        SyncDebugValuesFromState();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    public void SetVisible(bool visible)
    {
        TryInitializeUi();

        IsVisible = visible;

        if (optionsRoot != null)
            optionsRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
            SyncFromCamera();
    }

    public void SetDebugVisible(bool visible)
    {
        TryInitializeUi();

        IsDebugVisible = visible;

        if (debugRoot != null)
            debugRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
            SyncDebugValuesFromState();
    }

    public void SetGameOverVisible(bool visible)
    {
        TryInitializeUi();
        IsGameOverVisible = visible;

        if (gameOverRoot != null)
            gameOverRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void TryInitializeUi()
    {
        if (uiRoot != null)
            return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[OptionsMenuScript] UIDocument and root visual element are required.");
            return;
        }

        uiRoot = uiDocument.rootVisualElement;
        optionsRoot = uiRoot.Q<VisualElement>(OptionsRootName);
        debugRoot = uiRoot.Q<VisualElement>(DebugRootName);
        gameOverRoot = uiRoot.Q<VisualElement>(GameOverRootName);

        sensitivitySlider = uiRoot.Q<Slider>(SensitivitySliderName);
        sensitivityValueLabel = uiRoot.Q<Label>(SensitivityValueLabelName);
        godModeToggle = uiRoot.Q<Toggle>(GodModeToggleName);
        noClipToggle = uiRoot.Q<Toggle>(NoClipToggleName);
        disableAiToggle = uiRoot.Q<Toggle>(DisableAiToggleName);
        showSuspensionToggle = uiRoot.Q<Toggle>(ShowSuspensionToggleName);
        showNormalsToggle = uiRoot.Q<Toggle>(ShowNormalsToggleName);
        alwaysBoostToggle = uiRoot.Q<Toggle>(AlwaysBoostToggleName);
        quitButton = uiRoot.Q<Button>(QuitButtonName);

        if (sensitivitySlider == null)
        {
            Debug.LogError($"[OptionsMenuScript] Could not find Slider '{SensitivitySliderName}' in the options UXML.");
            return;
        }

        if (optionsRoot == null || debugRoot == null || gameOverRoot == null)
            Debug.LogError("[OptionsMenuScript] Could not find options/debug root elements in the options UXML.");

        sensitivitySlider.lowValue = minSensitivity;
        sensitivitySlider.highValue = maxSensitivity;
    }

    private void ResolveCameraIfNeeded()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindFirstObjectByType<ThirdPersonCamera>();
    }

    private void ResolvePlayerIfNeeded()
    {
        if (player == null)
            player = FindFirstObjectByType<CarController>();
    }

    private void RegisterCallbacks()
    {
        if (sensitivitySlider != null && !sliderCallbackRegistered)
        {
            sensitivitySlider.RegisterValueChangedCallback(OnSensitivityChanged);
            sliderCallbackRegistered = true;
        }

        if (!debugCallbacksRegistered)
        {
            RegisterToggle(godModeToggle, OnGodModeChanged);
            RegisterToggle(noClipToggle, OnNoClipChanged);
            RegisterToggle(disableAiToggle, OnDisableAiChanged);
            RegisterToggle(showSuspensionToggle, OnShowSuspensionChanged);
            RegisterToggle(showNormalsToggle, OnShowNormalsChanged);
            RegisterToggle(alwaysBoostToggle, OnAlwaysBoostChanged);
            debugCallbacksRegistered = true;
        }

        if (quitButton != null && !quitCallbackRegistered)
        {
            quitButton.clicked += OnQuitClicked;
            quitCallbackRegistered = true;
        }
    }

    private void UnregisterCallbacks()
    {
        if (sensitivitySlider != null && sliderCallbackRegistered)
        {
            sensitivitySlider.UnregisterValueChangedCallback(OnSensitivityChanged);
            sliderCallbackRegistered = false;
        }

        if (debugCallbacksRegistered)
        {
            UnregisterToggle(godModeToggle, OnGodModeChanged);
            UnregisterToggle(noClipToggle, OnNoClipChanged);
            UnregisterToggle(disableAiToggle, OnDisableAiChanged);
            UnregisterToggle(showSuspensionToggle, OnShowSuspensionChanged);
            UnregisterToggle(showNormalsToggle, OnShowNormalsChanged);
            UnregisterToggle(alwaysBoostToggle, OnAlwaysBoostChanged);
            debugCallbacksRegistered = false;
        }

        if (quitButton != null && quitCallbackRegistered)
        {
            quitButton.clicked -= OnQuitClicked;
            quitCallbackRegistered = false;
        }
    }

    private void SyncFromCamera()
    {
        if (sensitivitySlider == null)
            return;

        ResolveCameraIfNeeded();

        float value = thirdPersonCamera != null
            ? Mathf.Clamp(thirdPersonCamera.sensitivity, minSensitivity, maxSensitivity)
            : minSensitivity;

        sensitivitySlider.SetValueWithoutNotify(value);
        UpdateSensitivityLabel(value);

        if (thirdPersonCamera != null)
            thirdPersonCamera.sensitivity = value;
    }

    private void SyncDebugValuesFromState()
    {
        ResolvePlayerIfNeeded();

        SetToggleValue(godModeToggle, GameState.GodMode);
        SetToggleValue(noClipToggle, GameState.NoClip);
        SetToggleValue(disableAiToggle, GameState.DisableAI);

        if (player == null)
            return;

        SetToggleValue(showSuspensionToggle, player.ShowSuspensionRays);
        SetToggleValue(showNormalsToggle, player.ShowSurfaceNormals);
        SetToggleValue(alwaysBoostToggle, player.AlwaysBoostDebug);
    }

    private void OnSensitivityChanged(ChangeEvent<float> evt)
    {
        float newValue = Mathf.Clamp(evt.newValue, minSensitivity, maxSensitivity);

        if (thirdPersonCamera != null)
            thirdPersonCamera.sensitivity = newValue;

        UpdateSensitivityLabel(newValue);
    }

    private void OnGodModeChanged(ChangeEvent<bool> evt)
    {
        GameState.SetGodMode(evt.newValue);
    }

    private void OnNoClipChanged(ChangeEvent<bool> evt)
    {
        GameState.SetNoClip(evt.newValue);
    }

    private void OnDisableAiChanged(ChangeEvent<bool> evt)
    {
        GameState.SetDisableAI(evt.newValue);
    }

    private void OnShowSuspensionChanged(ChangeEvent<bool> evt)
    {
        ResolvePlayerIfNeeded();
        if (player != null)
            player.ShowSuspensionRays = evt.newValue;
    }

    private void OnShowNormalsChanged(ChangeEvent<bool> evt)
    {
        ResolvePlayerIfNeeded();
        if (player != null)
            player.ShowSurfaceNormals = evt.newValue;
    }

    private void OnAlwaysBoostChanged(ChangeEvent<bool> evt)
    {
        ResolvePlayerIfNeeded();
        if (player != null)
            player.AlwaysBoostDebug = evt.newValue;
    }

    private static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = value.ToString("0.00");
    }

    private static void RegisterToggle(Toggle toggle, EventCallback<ChangeEvent<bool>> callback)
    {
        if (toggle != null)
            toggle.RegisterValueChangedCallback(callback);
    }

    private static void UnregisterToggle(Toggle toggle, EventCallback<ChangeEvent<bool>> callback)
    {
        if (toggle != null)
            toggle.UnregisterValueChangedCallback(callback);
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
            toggle.SetValueWithoutNotify(value);
    }
}
