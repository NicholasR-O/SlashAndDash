using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class OptionsMenuScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;
    [SerializeField] private CarController player;

    [Header("Ranges")]
    [SerializeField] private float minSensitivity = 0.1f;
    [SerializeField] private float maxSensitivity = 3f;
    [SerializeField] private float minFieldOfView = 55f;
    [SerializeField] private float maxFieldOfView = 95f;
    [SerializeField] private float minBrightness = 0.5f;
    [SerializeField] private float maxBrightness = 1.5f;

    [Header("Custom Visuals")]
    [SerializeField] private Texture2D pauseBackdropImage;
    [SerializeField] private Texture2D pausePanelImage;
    [SerializeField] private Texture2D optionRowImage;
    [SerializeField] private Texture2D menuButtonImage;
    [SerializeField] private Texture2D toggleRowImage;
    [SerializeField] private Texture2D debugPanelImage;
    [SerializeField] private Texture2D gameOverPanelImage;

    [Header("UI Audio")]
    [SerializeField] private AudioClip[] hoverClips;
    [SerializeField] private AudioClip[] clickClips;
    [SerializeField] private AudioClip sliderClip;

    const string OptionsRootName = "options-root";
    const string OptionsBackdropImageName = "options-backdrop-image";
    const string OptionsPanelName = "options-panel";
    const string DebugRootName = "debug-root";
    const string DebugPanelName = "debug-panel";
    const string GameOverRootName = "game-over-root";
    const string GameOverPanelName = "game-over-panel";
    const string SensitivitySliderName = "sensitivity-slider";
    const string SensitivityValueLabelName = "sensitivity-value";
    const string FieldOfViewSliderName = "fov-slider";
    const string FieldOfViewValueLabelName = "fov-value";
    const string MasterVolumeSliderName = "master-volume-slider";
    const string MasterVolumeValueLabelName = "master-volume-value";
    const string MusicVolumeSliderName = "music-volume-slider";
    const string MusicVolumeValueLabelName = "music-volume-value";
    const string SoundEffectsVolumeSliderName = "sfx-volume-slider";
    const string SoundEffectsVolumeValueLabelName = "sfx-volume-value";
    const string BrightnessSliderName = "brightness-slider";
    const string BrightnessValueLabelName = "brightness-value";
    const string UiScaleButtonPrefix = "ui-scale-button-";
    const string SelectedScaleButtonClass = "selected-scale-button";
    const string AutoAimToggleName = "auto-aim-toggle";
    const string ResumeButtonName = "resume-button";
    const string QuitButtonName = "quit-button";
    const string GodModeToggleName = "debug-god-mode-toggle";
    const string NoClipToggleName = "debug-no-clip-toggle";
    const string DisableAiToggleName = "debug-disable-ai-toggle";
    const string AlwaysBoostToggleName = "debug-always-boost-toggle";
    const string KillerRamToggleName = "debug-killer-ram-toggle";
    const string TeleportDestinationDropdownName = "debug-teleport-destination-dropdown";
    const string TeleportButtonName = "debug-teleport-button";
    const string TeleportStatusLabelName = "debug-teleport-status";

    VisualElement uiRoot;
    VisualElement optionsRoot;
    VisualElement optionsBackdropImageElement;
    VisualElement optionsPanel;
    VisualElement debugRoot;
    VisualElement debugPanel;
    VisualElement gameOverRoot;
    VisualElement gameOverPanel;
    Slider sensitivitySlider;
    Slider fieldOfViewSlider;
    Slider masterVolumeSlider;
    Slider musicVolumeSlider;
    Slider soundEffectsVolumeSlider;
    Slider brightnessSlider;
    Label sensitivityValueLabel;
    Label fieldOfViewValueLabel;
    Label masterVolumeValueLabel;
    Label musicVolumeValueLabel;
    Label soundEffectsVolumeValueLabel;
    Label brightnessValueLabel;
    Toggle autoAimToggle;
    Toggle godModeToggle;
    Toggle noClipToggle;
    Toggle disableAiToggle;
    Toggle alwaysBoostToggle;
    Toggle killerRamToggle;
    DropdownField teleportDestinationDropdown;
    Label teleportStatusLabel;
    Button resumeButton;
    Button quitButton;
    Button teleportButton;
    readonly Button[] uiScaleButtons = new Button[GameOptions.MaxUiScaleLevel];
    readonly List<CheatTeleportService.Destination> teleportDestinations = new List<CheatTeleportService.Destination>();

    bool callbacksRegistered;
    bool responsiveCallbackRegistered;

    public bool IsVisible { get; private set; }
    public bool IsDebugVisible { get; private set; }
    public bool IsGameOverVisible { get; private set; }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        GameOptions.EnsureLoaded();
        TryInitializeUi();
        SetVisible(false);
        SetDebugVisible(false);
        SetGameOverVisible(false);
    }

    void OnEnable()
    {
        TryInitializeUi();
        RegisterCallbacks();
        GameOptions.Changed += SyncOptionsFromState;
        ResolveCameraIfNeeded();
        ResolvePlayerIfNeeded();
        SyncOptionsFromState();
        SyncDebugValuesFromState();
    }

    void OnDisable()
    {
        GameOptions.Changed -= SyncOptionsFromState;
        UnregisterCallbacks();
    }

    void OnDestroy()
    {
        if (uiRoot != null && responsiveCallbackRegistered)
        {
            uiRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            responsiveCallbackRegistered = false;
        }
    }

    public void SetVisible(bool visible)
    {
        TryInitializeUi();
        IsVisible = visible;

        if (optionsRoot != null)
            optionsRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
            SyncOptionsFromState();
    }

    public void SetDebugVisible(bool visible)
    {
        TryInitializeUi();
        IsDebugVisible = visible;

        if (debugRoot != null)
            debugRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
        {
            RefreshTeleportDestinations();
            SyncDebugValuesFromState();
        }
    }

    public void SetGameOverVisible(bool visible)
    {
        TryInitializeUi();
        IsGameOverVisible = visible;

        if (gameOverRoot != null)
            gameOverRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void TryInitializeUi()
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
        UIScaleUtility.ApplyToDocument(uiDocument);

        optionsRoot = uiRoot.Q<VisualElement>(OptionsRootName);
        optionsBackdropImageElement = uiRoot.Q<VisualElement>(OptionsBackdropImageName);
        optionsPanel = uiRoot.Q<VisualElement>(OptionsPanelName);
        debugRoot = uiRoot.Q<VisualElement>(DebugRootName);
        debugPanel = uiRoot.Q<VisualElement>(DebugPanelName);
        gameOverRoot = uiRoot.Q<VisualElement>(GameOverRootName);
        gameOverPanel = uiRoot.Q<VisualElement>(GameOverPanelName);

        sensitivitySlider = uiRoot.Q<Slider>(SensitivitySliderName);
        fieldOfViewSlider = uiRoot.Q<Slider>(FieldOfViewSliderName);
        masterVolumeSlider = uiRoot.Q<Slider>(MasterVolumeSliderName);
        musicVolumeSlider = uiRoot.Q<Slider>(MusicVolumeSliderName);
        soundEffectsVolumeSlider = uiRoot.Q<Slider>(SoundEffectsVolumeSliderName);
        brightnessSlider = uiRoot.Q<Slider>(BrightnessSliderName);
        sensitivityValueLabel = uiRoot.Q<Label>(SensitivityValueLabelName);
        fieldOfViewValueLabel = uiRoot.Q<Label>(FieldOfViewValueLabelName);
        masterVolumeValueLabel = uiRoot.Q<Label>(MasterVolumeValueLabelName);
        musicVolumeValueLabel = uiRoot.Q<Label>(MusicVolumeValueLabelName);
        soundEffectsVolumeValueLabel = uiRoot.Q<Label>(SoundEffectsVolumeValueLabelName);
        brightnessValueLabel = uiRoot.Q<Label>(BrightnessValueLabelName);
        for (int i = 0; i < uiScaleButtons.Length; i++)
            uiScaleButtons[i] = uiRoot.Q<Button>(UiScaleButtonPrefix + (i + 1));
        autoAimToggle = uiRoot.Q<Toggle>(AutoAimToggleName);
        resumeButton = uiRoot.Q<Button>(ResumeButtonName);
        quitButton = uiRoot.Q<Button>(QuitButtonName);

        godModeToggle = uiRoot.Q<Toggle>(GodModeToggleName);
        noClipToggle = uiRoot.Q<Toggle>(NoClipToggleName);
        disableAiToggle = uiRoot.Q<Toggle>(DisableAiToggleName);
        alwaysBoostToggle = uiRoot.Q<Toggle>(AlwaysBoostToggleName);
        killerRamToggle = uiRoot.Q<Toggle>(KillerRamToggleName);
        teleportDestinationDropdown = uiRoot.Q<DropdownField>(TeleportDestinationDropdownName);
        teleportButton = uiRoot.Q<Button>(TeleportButtonName);
        teleportStatusLabel = uiRoot.Q<Label>(TeleportStatusLabelName);

        ConfigureSliders();
        ApplyCustomVisuals();
        RegisterResponsiveLayout();
        RefreshTeleportDestinations();

        if (optionsRoot == null || debugRoot == null || gameOverRoot == null)
            Debug.LogError("[OptionsMenuScript] Could not find options/debug/game-over root elements in the options UXML.");
    }

    void ConfigureSliders()
    {
        ConfigureSlider(sensitivitySlider, minSensitivity, maxSensitivity);
        ConfigureSlider(fieldOfViewSlider, minFieldOfView, maxFieldOfView);
        ConfigureSlider(masterVolumeSlider, 0f, 1f);
        ConfigureSlider(musicVolumeSlider, 0f, 1f);
        ConfigureSlider(soundEffectsVolumeSlider, 0f, 1f);
        ConfigureSlider(brightnessSlider, minBrightness, maxBrightness);
    }

    static void ConfigureSlider(Slider slider, float lowValue, float highValue)
    {
        if (slider == null)
            return;

        slider.lowValue = lowValue;
        slider.highValue = highValue;
    }

    void ApplyCustomVisuals()
    {
        SetBackground(optionsRoot, pauseBackdropImage);
        SetBackground(optionsBackdropImageElement, pauseBackdropImage);
        SetBackground(optionsPanel, pausePanelImage);
        SetBackground(debugPanel, debugPanelImage);
        SetBackground(gameOverPanel, gameOverPanelImage);

        if (uiRoot == null)
            return;

        uiRoot.Query<VisualElement>(className: "option-row").ForEach(row => SetBackground(row, optionRowImage));
        uiRoot.Query<Button>(className: "menu-button").ForEach(button => SetBackground(button, menuButtonImage));
        uiRoot.Query<Toggle>(className: "menu-toggle").ForEach(toggle => SetBackground(toggle, toggleRowImage));
        uiRoot.Query<Toggle>(className: "debug-toggle").ForEach(toggle => SetBackground(toggle, toggleRowImage));
    }

    static void SetBackground(VisualElement element, Texture2D texture)
    {
        if (element != null && texture != null)
            element.style.backgroundImage = new StyleBackground(texture);
    }

    void ResolveCameraIfNeeded()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindFirstObjectByType<ThirdPersonCamera>();
    }

    void ResolvePlayerIfNeeded()
    {
        if (player == null)
            player = FindFirstObjectByType<CarController>();
    }

    void RegisterCallbacks()
    {
        if (callbacksRegistered)
            return;

        RegisterSlider(sensitivitySlider, OnSensitivityChanged);
        RegisterSlider(fieldOfViewSlider, OnFieldOfViewChanged);
        RegisterSlider(masterVolumeSlider, OnMasterVolumeChanged);
        RegisterSlider(musicVolumeSlider, OnMusicVolumeChanged);
        RegisterSlider(soundEffectsVolumeSlider, OnSoundEffectsVolumeChanged);
        RegisterSlider(brightnessSlider, OnBrightnessChanged);
        RegisterToggle(autoAimToggle, OnAutoAimChanged);

        RegisterToggle(godModeToggle, OnGodModeChanged);
        RegisterToggle(noClipToggle, OnNoClipChanged);
        RegisterToggle(disableAiToggle, OnDisableAiChanged);
        RegisterToggle(alwaysBoostToggle, OnAlwaysBoostChanged);
        RegisterToggle(killerRamToggle, OnKillerRamChanged);

        RegisterButtonSound(resumeButton);
        RegisterButtonSound(quitButton);
        RegisterButtonSound(teleportButton);
        RegisterUiScaleButtonCallbacks();

        if (resumeButton != null)
            resumeButton.clicked += OnResumeClicked;
        if (quitButton != null)
            quitButton.clicked += OnQuitClicked;
        if (teleportButton != null)
            teleportButton.clicked += OnTeleportClicked;

        RegisterUiSounds();
        callbacksRegistered = true;
    }

    void UnregisterCallbacks()
    {
        if (!callbacksRegistered)
            return;

        UnregisterSlider(sensitivitySlider, OnSensitivityChanged);
        UnregisterSlider(fieldOfViewSlider, OnFieldOfViewChanged);
        UnregisterSlider(masterVolumeSlider, OnMasterVolumeChanged);
        UnregisterSlider(musicVolumeSlider, OnMusicVolumeChanged);
        UnregisterSlider(soundEffectsVolumeSlider, OnSoundEffectsVolumeChanged);
        UnregisterSlider(brightnessSlider, OnBrightnessChanged);
        UnregisterToggle(autoAimToggle, OnAutoAimChanged);

        UnregisterToggle(godModeToggle, OnGodModeChanged);
        UnregisterToggle(noClipToggle, OnNoClipChanged);
        UnregisterToggle(disableAiToggle, OnDisableAiChanged);
        UnregisterToggle(alwaysBoostToggle, OnAlwaysBoostChanged);
        UnregisterToggle(killerRamToggle, OnKillerRamChanged);

        if (resumeButton != null)
            resumeButton.clicked -= OnResumeClicked;
        if (quitButton != null)
            quitButton.clicked -= OnQuitClicked;
        if (teleportButton != null)
            teleportButton.clicked -= OnTeleportClicked;

        UnregisterButtonSound(resumeButton);
        UnregisterButtonSound(quitButton);
        UnregisterButtonSound(teleportButton);
        UnregisterUiScaleButtonCallbacks();
        UnregisterUiSounds();
        callbacksRegistered = false;
    }

    void SyncOptionsFromState()
    {
        GameOptions.EnsureLoaded();
        ResolveCameraIfNeeded();

        SetSliderValue(sensitivitySlider, GameOptions.Sensitivity);
        SetSliderValue(fieldOfViewSlider, GameOptions.FieldOfView);
        SetSliderValue(masterVolumeSlider, GameOptions.MasterVolume);
        SetSliderValue(musicVolumeSlider, GameOptions.MusicVolume);
        SetSliderValue(soundEffectsVolumeSlider, GameOptions.SoundEffectsVolume);
        SetSliderValue(brightnessSlider, GameOptions.Brightness);
        SetToggleValue(autoAimToggle, GameOptions.AutoAim);

        UpdateSensitivityLabel(GameOptions.Sensitivity);
        UpdateFieldOfViewLabel(GameOptions.FieldOfView);
        UpdateVolumeLabel(masterVolumeValueLabel, GameOptions.MasterVolume);
        UpdateVolumeLabel(musicVolumeValueLabel, GameOptions.MusicVolume);
        UpdateVolumeLabel(soundEffectsVolumeValueLabel, GameOptions.SoundEffectsVolume);
        UpdateBrightnessLabel(GameOptions.Brightness);
        UpdateUiScaleButtons(GameOptions.UiScaleLevel);

        ApplyCameraOptions();
        GameOptions.ApplyRuntimeSettings();
        UIScaleUtility.ApplyToDocument(uiDocument);
    }

    void SyncDebugValuesFromState()
    {
        ResolvePlayerIfNeeded();

        SetToggleValue(godModeToggle, GameState.GodMode);
        SetToggleValue(noClipToggle, GameState.NoClip);
        SetToggleValue(disableAiToggle, GameState.DisableAI);
        RefreshTeleportDestinations();

        if (player == null)
            return;

        SetToggleValue(alwaysBoostToggle, player.AlwaysBoostDebug);
        SetToggleValue(killerRamToggle, player.KillerRamDebug);
    }

    void ApplyCameraOptions()
    {
        ResolveCameraIfNeeded();
        if (thirdPersonCamera == null)
            return;

        thirdPersonCamera.ApplyUserOptions();
    }

    void OnSensitivityChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp(evt.newValue, minSensitivity, maxSensitivity);
        GameOptions.SetSensitivity(value);
        UpdateSensitivityLabel(value);
        ApplyCameraOptions();
    }

    void OnFieldOfViewChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp(evt.newValue, minFieldOfView, maxFieldOfView);
        GameOptions.SetFieldOfView(value);
        UpdateFieldOfViewLabel(value);
        ApplyCameraOptions();
    }

    void OnMasterVolumeChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp01(evt.newValue);
        GameOptions.SetMasterVolume(value);
        UpdateVolumeLabel(masterVolumeValueLabel, value);
    }

    void OnMusicVolumeChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp01(evt.newValue);
        GameOptions.SetMusicVolume(value);
        UpdateVolumeLabel(musicVolumeValueLabel, value);
    }

    void OnSoundEffectsVolumeChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp01(evt.newValue);
        GameOptions.SetSoundEffectsVolume(value);
        UpdateVolumeLabel(soundEffectsVolumeValueLabel, value);
    }

    void OnBrightnessChanged(ChangeEvent<float> evt)
    {
        float value = Mathf.Clamp(evt.newValue, minBrightness, maxBrightness);
        GameOptions.SetBrightness(value);
        UpdateBrightnessLabel(value);
    }

    void OnAutoAimChanged(ChangeEvent<bool> evt)
    {
        GameOptions.SetAutoAim(evt.newValue);
    }

    void OnUiScale1Clicked()
    {
        SetUiScaleLevel(1);
    }

    void OnUiScale2Clicked()
    {
        SetUiScaleLevel(2);
    }

    void OnUiScale3Clicked()
    {
        SetUiScaleLevel(3);
    }

    void OnUiScale4Clicked()
    {
        SetUiScaleLevel(4);
    }

    void SetUiScaleLevel(int level)
    {
        level = GameOptions.ClampUiScaleLevel(level);
        GameOptions.SetUiScaleLevel(level);
        UpdateUiScaleButtons(level);
        UIScaleUtility.ApplyToDocument(uiDocument);
    }

    void OnGodModeChanged(ChangeEvent<bool> evt)
    {
        GameState.SetGodMode(evt.newValue);
    }

    void OnNoClipChanged(ChangeEvent<bool> evt)
    {
        GameState.SetNoClip(evt.newValue);
    }

    void OnDisableAiChanged(ChangeEvent<bool> evt)
    {
        GameState.SetDisableAI(evt.newValue);
    }

    void OnAlwaysBoostChanged(ChangeEvent<bool> evt)
    {
        ResolvePlayerIfNeeded();
        if (player != null)
            player.AlwaysBoostDebug = evt.newValue;
    }

    void OnKillerRamChanged(ChangeEvent<bool> evt)
    {
        ResolvePlayerIfNeeded();
        if (player != null)
            player.KillerRamDebug = evt.newValue;
    }

    static void OnResumeClicked()
    {
        GameState.SetPaused(false);
    }

    static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnTeleportClicked()
    {
        RefreshTeleportDestinations();

        if (teleportDestinations.Count == 0)
        {
            SetTeleportStatus("No teleport targets found.");
            return;
        }

        int index = teleportDestinationDropdown != null ? teleportDestinationDropdown.index : 0;
        index = Mathf.Clamp(index, 0, teleportDestinations.Count - 1);
        CheatTeleportService.Destination destination = teleportDestinations[index];
        SetTeleportStatus("Teleporting...");
        CheatTeleportService.TeleportTo(destination);
    }

    void RefreshTeleportDestinations()
    {
        teleportDestinations.Clear();

        if (teleportDestinationDropdown == null)
            return;

        List<CheatTeleportService.Destination> destinations = CheatTeleportService.GetDestinations();
        for (int i = 0; i < destinations.Count; i++)
        {
            if (destinations[i] != null)
                teleportDestinations.Add(destinations[i]);
        }

        if (teleportDestinations.Count == 0)
        {
            teleportDestinationDropdown.choices = new List<string> { "No targets" };
            teleportDestinationDropdown.SetValueWithoutNotify("No targets");
            if (teleportButton != null)
                teleportButton.SetEnabled(false);
            SetTeleportStatus(string.Empty);
            return;
        }

        List<string> labels = BuildTeleportDestinationLabels(teleportDestinations);
        int selectedIndex = teleportDestinationDropdown.index;
        if (selectedIndex < 0 || selectedIndex >= labels.Count)
            selectedIndex = 0;

        teleportDestinationDropdown.choices = labels;
        teleportDestinationDropdown.index = selectedIndex;
        teleportDestinationDropdown.SetValueWithoutNotify(labels[selectedIndex]);

        if (teleportButton != null)
            teleportButton.SetEnabled(true);
        SetTeleportStatus(string.Empty);
    }

    static List<string> BuildTeleportDestinationLabels(List<CheatTeleportService.Destination> destinations)
    {
        Dictionary<string, int> duplicateCounts = new Dictionary<string, int>();
        for (int i = 0; i < destinations.Count; i++)
        {
            string key = GetDestinationDuplicateKey(destinations[i]);
            duplicateCounts.TryGetValue(key, out int count);
            duplicateCounts[key] = count + 1;
        }

        List<string> labels = new List<string>(destinations.Count);
        for (int i = 0; i < destinations.Count; i++)
        {
            CheatTeleportService.Destination destination = destinations[i];
            string label = $"{destination.SceneName} / {destination.KindLabel}: {destination.ObjectName}";
            if (duplicateCounts.TryGetValue(GetDestinationDuplicateKey(destination), out int count) && count > 1)
            {
                Vector3 pos = destination.FallbackPosition;
                label += $" ({pos.x:0}, {pos.z:0})";
            }

            labels.Add(label);
        }

        return labels;
    }

    static string GetDestinationDuplicateKey(CheatTeleportService.Destination destination)
    {
        if (destination == null)
            return string.Empty;

        return destination.SceneName + "|" + destination.Kind + "|" + destination.ObjectName;
    }

    void SetTeleportStatus(string message)
    {
        if (teleportStatusLabel != null)
            teleportStatusLabel.text = message;
    }

    void RegisterResponsiveLayout()
    {
        if (uiRoot == null || responsiveCallbackRegistered)
            return;

        uiRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        responsiveCallbackRegistered = true;
        ApplyResponsiveLayout(uiRoot.resolvedStyle.width, uiRoot.resolvedStyle.height);
    }

    void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        ApplyResponsiveLayout(evt.newRect.width, evt.newRect.height);
    }

    void ApplyResponsiveLayout(float width, float height)
    {
        if (uiRoot == null)
            return;

        bool compact = width > 0f && height > 0f && (width < 760f || height < 690f);
        uiRoot.EnableInClassList("compact-menu", compact);
    }

    void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = value.ToString("0.00");
    }

    void UpdateFieldOfViewLabel(float value)
    {
        if (fieldOfViewValueLabel != null)
            fieldOfViewValueLabel.text = Mathf.RoundToInt(value).ToString();
    }

    static void UpdateVolumeLabel(Label label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    void UpdateBrightnessLabel(float value)
    {
        if (brightnessValueLabel != null)
            brightnessValueLabel.text = value.ToString("0.00") + "x";
    }

    static void RegisterSlider(Slider slider, EventCallback<ChangeEvent<float>> callback)
    {
        if (slider != null)
            slider.RegisterValueChangedCallback(callback);
    }

    static void UnregisterSlider(Slider slider, EventCallback<ChangeEvent<float>> callback)
    {
        if (slider != null)
            slider.UnregisterValueChangedCallback(callback);
    }

    static void RegisterToggle(Toggle toggle, EventCallback<ChangeEvent<bool>> callback)
    {
        if (toggle != null)
            toggle.RegisterValueChangedCallback(callback);
    }

    static void UnregisterToggle(Toggle toggle, EventCallback<ChangeEvent<bool>> callback)
    {
        if (toggle != null)
            toggle.UnregisterValueChangedCallback(callback);
    }

    void RegisterUiSounds()
    {
        RegisterSliderSound(sensitivitySlider);
        RegisterSliderSound(fieldOfViewSlider);
        RegisterSliderSound(masterVolumeSlider);
        RegisterSliderSound(musicVolumeSlider);
        RegisterSliderSound(soundEffectsVolumeSlider);
        RegisterSliderSound(brightnessSlider);

        RegisterToggleSound(autoAimToggle);
        RegisterToggleSound(godModeToggle);
        RegisterToggleSound(noClipToggle);
        RegisterToggleSound(disableAiToggle);
        RegisterToggleSound(alwaysBoostToggle);
        RegisterToggleSound(killerRamToggle);
        RegisterDropdownSound(teleportDestinationDropdown);
    }

    void UnregisterUiSounds()
    {
        UnregisterSliderSound(sensitivitySlider);
        UnregisterSliderSound(fieldOfViewSlider);
        UnregisterSliderSound(masterVolumeSlider);
        UnregisterSliderSound(musicVolumeSlider);
        UnregisterSliderSound(soundEffectsVolumeSlider);
        UnregisterSliderSound(brightnessSlider);

        UnregisterToggleSound(autoAimToggle);
        UnregisterToggleSound(godModeToggle);
        UnregisterToggleSound(noClipToggle);
        UnregisterToggleSound(disableAiToggle);
        UnregisterToggleSound(alwaysBoostToggle);
        UnregisterToggleSound(killerRamToggle);
        UnregisterDropdownSound(teleportDestinationDropdown);
    }

    void RegisterSliderSound(Slider slider)
    {
        if (slider == null)
            return;

        slider.RegisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        slider.RegisterValueChangedCallback(OnUiSliderChanged);
    }

    void UnregisterSliderSound(Slider slider)
    {
        if (slider == null)
            return;

        slider.UnregisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        slider.UnregisterValueChangedCallback(OnUiSliderChanged);
    }

    void RegisterToggleSound(Toggle toggle)
    {
        if (toggle == null)
            return;

        toggle.RegisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        toggle.RegisterValueChangedCallback(OnUiToggleChanged);
    }

    void UnregisterToggleSound(Toggle toggle)
    {
        if (toggle == null)
            return;

        toggle.UnregisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        toggle.UnregisterValueChangedCallback(OnUiToggleChanged);
    }

    void RegisterDropdownSound(DropdownField dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.RegisterCallback<PointerEnterEvent>(OnUiPointerEnter);
    }

    void UnregisterDropdownSound(DropdownField dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.UnregisterCallback<PointerEnterEvent>(OnUiPointerEnter);
    }

    void RegisterButtonSound(Button button)
    {
        if (button == null)
            return;

        button.RegisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        button.clicked += PlayUiClickSound;
    }

    void UnregisterButtonSound(Button button)
    {
        if (button == null)
            return;

        button.UnregisterCallback<PointerEnterEvent>(OnUiPointerEnter);
        button.clicked -= PlayUiClickSound;
    }

    void OnUiPointerEnter(PointerEnterEvent evt)
    {
        UISoundPlayer.PlayRandomHover(hoverClips);
    }

    void OnUiSliderChanged(ChangeEvent<float> evt)
    {
        UISoundPlayer.PlaySlider(sliderClip);
    }

    void OnUiToggleChanged(ChangeEvent<bool> evt)
    {
        UISoundPlayer.PlayRandomClick(clickClips);
    }

    void PlayUiClickSound()
    {
        UISoundPlayer.PlayRandomClick(clickClips);
    }

    void RegisterUiScaleButtonCallbacks()
    {
        RegisterButtonSound(GetUiScaleButton(1));
        RegisterButtonSound(GetUiScaleButton(2));
        RegisterButtonSound(GetUiScaleButton(3));
        RegisterButtonSound(GetUiScaleButton(4));

        Button button = GetUiScaleButton(1);
        if (button != null)
            button.clicked += OnUiScale1Clicked;
        button = GetUiScaleButton(2);
        if (button != null)
            button.clicked += OnUiScale2Clicked;
        button = GetUiScaleButton(3);
        if (button != null)
            button.clicked += OnUiScale3Clicked;
        button = GetUiScaleButton(4);
        if (button != null)
            button.clicked += OnUiScale4Clicked;
    }

    void UnregisterUiScaleButtonCallbacks()
    {
        Button button = GetUiScaleButton(1);
        if (button != null)
            button.clicked -= OnUiScale1Clicked;
        button = GetUiScaleButton(2);
        if (button != null)
            button.clicked -= OnUiScale2Clicked;
        button = GetUiScaleButton(3);
        if (button != null)
            button.clicked -= OnUiScale3Clicked;
        button = GetUiScaleButton(4);
        if (button != null)
            button.clicked -= OnUiScale4Clicked;

        UnregisterButtonSound(GetUiScaleButton(1));
        UnregisterButtonSound(GetUiScaleButton(2));
        UnregisterButtonSound(GetUiScaleButton(3));
        UnregisterButtonSound(GetUiScaleButton(4));
    }

    Button GetUiScaleButton(int level)
    {
        int index = GameOptions.ClampUiScaleLevel(level) - 1;
        return index >= 0 && index < uiScaleButtons.Length ? uiScaleButtons[index] : null;
    }

    void UpdateUiScaleButtons(int selectedLevel)
    {
        selectedLevel = GameOptions.ClampUiScaleLevel(selectedLevel);
        for (int i = 0; i < uiScaleButtons.Length; i++)
        {
            Button button = uiScaleButtons[i];
            if (button != null)
                button.EnableInClassList(SelectedScaleButtonClass, i + 1 == selectedLevel);
        }
    }

    static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);
    }

    static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
            toggle.SetValueWithoutNotify(value);
    }
}
