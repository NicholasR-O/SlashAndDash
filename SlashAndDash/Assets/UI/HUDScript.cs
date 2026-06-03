using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UIDocument uiDocument;
    [SerializeField] CarController player;

    [Header("Speedometer")]
    [SerializeField] float healthVisualLerpSpeed = 4f;
    [SerializeField, Range(-180f, 180f)] float speedometerMinAngle = -125f;
    [SerializeField, Range(-180f, 180f)] float speedometerMaxAngle = 125f;

    [Header("Bar Colors")]
    [SerializeField] Color healthColor = new Color32(107, 230, 132, 255);
    [SerializeField] Color boostLayerOneColor = new Color32(207, 244, 255, 255);
    [SerializeField] Color boostLayerTwoColor = new Color32(108, 211, 255, 255);
    [SerializeField] Color boostLayerThreeColor = new Color32(32, 143, 255, 255);

    [Header("Custom Visuals")]
    [SerializeField] Texture2D hudPanelImage;
    [FormerlySerializedAs("gaugeBackgroundImage")]
    [SerializeField] Texture2D speedometerBackgroundImage;
    [FormerlySerializedAs("speedIconImage")]
    [SerializeField] Texture2D speedometerNeedleImage;
    [SerializeField] Texture2D healthBarOverlayImage;
    [FormerlySerializedAs("boostIconImage")]
    [SerializeField] Texture2D boostBarOverlayImage;
    [SerializeField] Texture2D tutorialPanelImage;
    [SerializeField] Texture2D arenaCounterPanelImage;

    const string RootName = "hud-root";
    const string HudPanelName = "hud-panel";
    const string HudPanelImageName = "hud-panel-image";
    const string SpeedometerBackgroundName = "speedometer-background-image";
    const string SpeedometerNeedleName = "speedometer-needle";
    const string SpeedometerNeedleImageName = "speedometer-needle-image";
    const string SpeedometerNeedleFallbackName = "speedometer-needle-fallback";
    const string HealthFillName = "health-fill";
    const string HealthBarOverlayName = "health-bar-overlay-image";
    const string BoostLayerOneName = "boost-layer-one";
    const string BoostLayerTwoName = "boost-layer-two";
    const string BoostLayerThreeName = "boost-layer-three";
    const string BoostBarOverlayName = "boost-bar-overlay-image";
    const string BoostMarkerOneName = "boost-marker-one";
    const string BoostMarkerTwoName = "boost-marker-two";
    const string BoostMarkerThreeName = "boost-marker-three";
    const string HealthValueName = "health-value";
    const string SpeedValueName = "speed-value";
    const string BoostValueName = "boost-value";
    const string TutorialContainerName = "tutorial-container";
    const string TutorialTextName = "tutorial-text";
    const string ArenaCounterContainerName = "arena-counter-container";
    const string ArenaCounterTextName = "arena-counter-text";

    const float ArenaCounterHiddenTop = -78f;
    const float ArenaCounterShownTop = 18f;
    const float ArenaCounterSlideDuration = 0.28f;

    VisualElement root;
    VisualElement hudPanel;
    VisualElement hudPanelImageElement;
    VisualElement speedometerBackgroundElement;
    VisualElement speedometerNeedleElement;
    VisualElement speedometerNeedleImageElement;
    VisualElement speedometerNeedleFallbackElement;
    VisualElement healthFillElement;
    VisualElement healthBarOverlayElement;
    VisualElement boostLayerOneElement;
    VisualElement boostLayerTwoElement;
    VisualElement boostLayerThreeElement;
    VisualElement boostBarOverlayElement;
    VisualElement boostMarkerOneElement;
    VisualElement boostMarkerTwoElement;
    VisualElement boostMarkerThreeElement;
    VisualElement tutorialContainer;
    VisualElement arenaCounterContainer;
    Label healthValueLabel;
    Label speedValueLabel;
    Label boostValueLabel;
    Label tutorialTextLabel;
    Label arenaCounterTextLabel;
    float displayedHealthRatio = 1f;
    bool healthVisualInitialized;
    string tutorialMessage = string.Empty;
    bool tutorialMessageVisible;
    Coroutine arenaCounterAnimation;
    float arenaCounterCurrentTop = ArenaCounterHiddenTop;
    float arenaCounterCurrentOpacity;
    bool responsiveCallbackRegistered;

    public bool IsVisible { get; private set; }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        GameOptions.EnsureLoaded();
        TryInitializeUi();
        SetVisible(GameState.IsPlaying);
    }

    void OnEnable()
    {
        TryInitializeUi();
        GameOptions.Changed += OnGameOptionsChanged;
        GameState.StateChanged += OnGameStateChanged;
        ArenaTrigger.ArenaStarted += OnArenaStarted;
        ArenaTrigger.ArenaEnemyCountChanged += OnArenaEnemyCountChanged;
        ArenaTrigger.ArenaEnded += OnArenaEnded;
        ResolvePlayerIfNeeded();
        RefreshHud();
    }

    void OnDisable()
    {
        GameOptions.Changed -= OnGameOptionsChanged;
        GameState.StateChanged -= OnGameStateChanged;
        ArenaTrigger.ArenaStarted -= OnArenaStarted;
        ArenaTrigger.ArenaEnemyCountChanged -= OnArenaEnemyCountChanged;
        ArenaTrigger.ArenaEnded -= OnArenaEnded;

        if (arenaCounterAnimation != null)
        {
            StopCoroutine(arenaCounterAnimation);
            arenaCounterAnimation = null;
        }
    }

    void OnDestroy()
    {
        if (root != null && responsiveCallbackRegistered)
        {
            root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            responsiveCallbackRegistered = false;
        }
    }

    void Update()
    {
        if (!IsVisible)
            return;

        ResolvePlayerIfNeeded();
        RefreshHud();
    }

    public void SetVisible(bool visible)
    {
        TryInitializeUi();
        IsVisible = visible;

        if (root != null)
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void ShowTutorialMessage(string message)
    {
        tutorialMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        tutorialMessageVisible = !string.IsNullOrEmpty(tutorialMessage);
        ApplyTutorialMessage();
    }

    public void HideTutorialMessage()
    {
        tutorialMessage = string.Empty;
        tutorialMessageVisible = false;
        ApplyTutorialMessage();
    }

    void OnGameStateChanged(GameState.State state)
    {
        SetVisible(state == GameState.State.Playing);
    }

    void OnGameOptionsChanged()
    {
        UIScaleUtility.ApplyToDocument(uiDocument);

        if (root != null)
            ApplyResponsiveLayout(root.resolvedStyle.width, root.resolvedStyle.height);
    }

    void TryInitializeUi()
    {
        if (root != null)
            return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[HUDScript] UIDocument and root visual element are required.");
            return;
        }

        VisualElement documentRoot = uiDocument.rootVisualElement;
        UIScaleUtility.ApplyToDocument(uiDocument);

        root = documentRoot.Q<VisualElement>(RootName);
        hudPanel = documentRoot.Q<VisualElement>(HudPanelName);
        hudPanelImageElement = documentRoot.Q<VisualElement>(HudPanelImageName);
        speedometerBackgroundElement = documentRoot.Q<VisualElement>(SpeedometerBackgroundName);
        speedometerNeedleElement = documentRoot.Q<VisualElement>(SpeedometerNeedleName);
        speedometerNeedleImageElement = documentRoot.Q<VisualElement>(SpeedometerNeedleImageName);
        speedometerNeedleFallbackElement = documentRoot.Q<VisualElement>(SpeedometerNeedleFallbackName);
        healthFillElement = documentRoot.Q<VisualElement>(HealthFillName);
        healthBarOverlayElement = documentRoot.Q<VisualElement>(HealthBarOverlayName);
        boostLayerOneElement = documentRoot.Q<VisualElement>(BoostLayerOneName);
        boostLayerTwoElement = documentRoot.Q<VisualElement>(BoostLayerTwoName);
        boostLayerThreeElement = documentRoot.Q<VisualElement>(BoostLayerThreeName);
        boostBarOverlayElement = documentRoot.Q<VisualElement>(BoostBarOverlayName);
        boostMarkerOneElement = documentRoot.Q<VisualElement>(BoostMarkerOneName);
        boostMarkerTwoElement = documentRoot.Q<VisualElement>(BoostMarkerTwoName);
        boostMarkerThreeElement = documentRoot.Q<VisualElement>(BoostMarkerThreeName);
        tutorialContainer = documentRoot.Q<VisualElement>(TutorialContainerName);
        arenaCounterContainer = documentRoot.Q<VisualElement>(ArenaCounterContainerName);
        healthValueLabel = documentRoot.Q<Label>(HealthValueName);
        speedValueLabel = documentRoot.Q<Label>(SpeedValueName);
        boostValueLabel = documentRoot.Q<Label>(BoostValueName);
        tutorialTextLabel = documentRoot.Q<Label>(TutorialTextName);
        arenaCounterTextLabel = documentRoot.Q<Label>(ArenaCounterTextName);

        ApplyTutorialMessage();
        ApplyCustomVisuals();
        ApplyArenaCounterVisuals(DisplayStyle.None);
        RegisterResponsiveLayout();
    }

    void ApplyCustomVisuals()
    {
        SetBackground(hudPanel, hudPanelImage);
        SetBackground(hudPanelImageElement, hudPanelImage);
        SetBackground(speedometerBackgroundElement, speedometerBackgroundImage);
        SetBackground(speedometerNeedleImageElement, speedometerNeedleImage);
        SetBackground(healthBarOverlayElement, healthBarOverlayImage);
        SetBackground(boostBarOverlayElement, boostBarOverlayImage);
        SetBackground(tutorialContainer, tutorialPanelImage);
        SetBackground(arenaCounterContainer, arenaCounterPanelImage);
        SetBackground(tutorialTextLabel, tutorialPanelImage);
        SetBackground(arenaCounterTextLabel, arenaCounterPanelImage);

        if (speedometerNeedleImageElement != null)
            speedometerNeedleImageElement.style.display = speedometerNeedleImage != null ? DisplayStyle.Flex : DisplayStyle.None;

        if (speedometerNeedleFallbackElement != null)
            speedometerNeedleFallbackElement.style.display = speedometerNeedleImage != null ? DisplayStyle.None : DisplayStyle.Flex;
    }

    static void SetBackground(VisualElement element, Texture2D texture)
    {
        if (element != null && texture != null)
            element.style.backgroundImage = new StyleBackground(texture);
    }

    void RegisterResponsiveLayout()
    {
        if (root == null || responsiveCallbackRegistered)
            return;

        root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        responsiveCallbackRegistered = true;
        ApplyResponsiveLayout(root.resolvedStyle.width, root.resolvedStyle.height);
    }

    void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        ApplyResponsiveLayout(evt.newRect.width, evt.newRect.height);
    }

    void ApplyResponsiveLayout(float width, float height)
    {
        if (root == null)
            return;

        bool compact = width > 0f && (width < 760f || height < 520f);
        root.EnableInClassList("hud-compact", compact);
    }

    void ResolvePlayerIfNeeded()
    {
        if (player == null)
            player = FindFirstObjectByType<CarController>();
    }

    void RefreshHud()
    {
        float speedRatio = player != null ? player.SpeedRatio : 0f;
        float hpRatio = player != null ? player.CurrentHealth / Mathf.Max(1f, player.MaxHealth) : 0f;

        UpdateHealthVisual(hpRatio);
        UpdateSpeedometer(speedRatio);
        UpdateHealthBar(hpRatio);
        UpdateBoostBar();
        UpdateReadoutLabels(hpRatio);
    }

    void UpdateSpeedometer(float speedRatio)
    {
        if (speedometerNeedleElement == null)
            return;

        float angle = Mathf.Lerp(speedometerMinAngle, speedometerMaxAngle, Mathf.Clamp01(speedRatio));
        speedometerNeedleElement.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void UpdateHealthBar(float hpRatio)
    {
        if (healthFillElement == null)
            return;

        healthFillElement.style.backgroundColor = healthColor;
        SetFillWidth(healthFillElement, displayedHealthRatio);
    }

    void UpdateBoostBar()
    {
        int stackCap = player != null ? Mathf.Max(1, player.BoostStackCap) : 3;
        float boostUnits = GetDisplayBoostUnits(stackCap);

        SetBoostLayer(boostLayerOneElement, boostUnits, stackCap, 1, boostLayerOneColor);
        SetBoostLayer(boostLayerTwoElement, boostUnits, stackCap, 2, boostLayerTwoColor);
        SetBoostLayer(boostLayerThreeElement, boostUnits, stackCap, 3, boostLayerThreeColor);
        UpdateBoostMarkers(stackCap);
    }

    float GetDisplayBoostUnits(int stackCap)
    {
        if (player == null)
            return 0f;

        if (player.CurrentBoostStacks > 0)
            return Mathf.Clamp(player.CurrentBoostStacks * player.RemainingBoostRatio, 0f, stackCap);

        return Mathf.Clamp01(player.StoredBoostRatio) * stackCap;
    }

    static void SetBoostLayer(VisualElement element, float boostUnits, int stackCap, int layerIndex, Color color)
    {
        if (element == null)
            return;

        if (stackCap < layerIndex || boostUnits <= layerIndex - 1)
        {
            element.style.display = DisplayStyle.None;
            return;
        }

        float visibleUnits = Mathf.Min(boostUnits, layerIndex);
        element.style.display = DisplayStyle.Flex;
        element.style.backgroundColor = color;
        SetFillWidth(element, visibleUnits / Mathf.Max(1, stackCap));
    }

    void UpdateBoostMarkers(int stackCap)
    {
        SetBoostMarker(boostMarkerOneElement, stackCap, 1);
        SetBoostMarker(boostMarkerTwoElement, stackCap, 2);
        SetBoostMarker(boostMarkerThreeElement, stackCap, 3);
    }

    static void SetBoostMarker(VisualElement element, int stackCap, int markerIndex)
    {
        if (element == null)
            return;

        if (stackCap < markerIndex)
        {
            element.style.display = DisplayStyle.None;
            return;
        }

        float markerRatio = markerIndex / (float)Mathf.Max(1, stackCap);
        element.style.display = DisplayStyle.Flex;
        element.style.left = Length.Percent(Mathf.Min(markerRatio, 0.985f) * 100f);
    }

    void UpdateReadoutLabels(float hpRatio)
    {
        if (healthValueLabel != null)
            healthValueLabel.text = Mathf.RoundToInt(Mathf.Clamp01(hpRatio) * 100f) + "%";

        if (speedValueLabel != null)
        {
            float speed = player != null ? player.CurrentSpeed : 0f;
            speedValueLabel.text = Mathf.RoundToInt(speed).ToString();
        }

        if (boostValueLabel != null)
        {
            int stackCap = player != null ? Mathf.Max(1, player.BoostStackCap) : 3;
            int fullBars = player != null ? player.StoredBoostFullBars : 0;
            int activeStacks = player != null ? player.CurrentBoostStacks : 0;
            boostValueLabel.text = activeStacks > 0 ? $"{activeStacks}x" : $"{fullBars}/{stackCap}";
        }
    }

    void UpdateHealthVisual(float targetRatio)
    {
        targetRatio = Mathf.Clamp01(targetRatio);

        if (!healthVisualInitialized)
        {
            displayedHealthRatio = targetRatio;
            healthVisualInitialized = true;
            return;
        }

        float smooth = Mathf.Max(0.01f, healthVisualLerpSpeed);
        float blend = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, targetRatio, blend);
    }

    static void SetFillWidth(VisualElement element, float ratio)
    {
        if (element != null)
            element.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
    }

    void ApplyTutorialMessage()
    {
        TryInitializeUi();
        if (tutorialContainer == null || tutorialTextLabel == null)
            return;

        tutorialTextLabel.text = tutorialMessage;
        tutorialContainer.style.display = tutorialMessageVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void OnArenaStarted(int remainingEnemies)
    {
        SetArenaCounterText(remainingEnemies);
        SlideArenaCounter(show: true);
    }

    void OnArenaEnemyCountChanged(int remainingEnemies)
    {
        SetArenaCounterText(remainingEnemies);
    }

    void OnArenaEnded()
    {
        SlideArenaCounter(show: false);
    }

    void SetArenaCounterText(int remainingEnemies)
    {
        TryInitializeUi();
        if (arenaCounterTextLabel == null)
            return;

        int count = Mathf.Max(0, remainingEnemies);
        arenaCounterTextLabel.text = count == 1 ? "1 ENEMY REMAINS" : $"{count} ENEMIES REMAIN";
    }

    void SlideArenaCounter(bool show)
    {
        TryInitializeUi();
        if (arenaCounterContainer == null)
            return;

        if (arenaCounterAnimation != null)
            StopCoroutine(arenaCounterAnimation);

        arenaCounterAnimation = StartCoroutine(ArenaCounterSlideRoutine(show));
    }

    IEnumerator ArenaCounterSlideRoutine(bool show)
    {
        float startTop = arenaCounterCurrentTop;
        float targetTop = show ? ArenaCounterShownTop : ArenaCounterHiddenTop;
        float startOpacity = arenaCounterCurrentOpacity;
        float targetOpacity = show ? 1f : 0f;
        float elapsed = 0f;

        if (show)
            ApplyArenaCounterVisuals(DisplayStyle.Flex);

        while (elapsed < ArenaCounterSlideDuration)
        {
            float t = Mathf.Clamp01(elapsed / ArenaCounterSlideDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            arenaCounterCurrentTop = Mathf.Lerp(startTop, targetTop, eased);
            arenaCounterCurrentOpacity = Mathf.Lerp(startOpacity, targetOpacity, eased);
            ApplyArenaCounterVisuals(DisplayStyle.Flex);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        arenaCounterCurrentTop = targetTop;
        arenaCounterCurrentOpacity = targetOpacity;
        ApplyArenaCounterVisuals(show ? DisplayStyle.Flex : DisplayStyle.None);
        arenaCounterAnimation = null;
    }

    void ApplyArenaCounterVisuals(DisplayStyle display)
    {
        if (arenaCounterContainer == null)
            return;

        arenaCounterContainer.style.display = display;
        arenaCounterContainer.style.top = arenaCounterCurrentTop;
        arenaCounterContainer.style.opacity = arenaCounterCurrentOpacity;
    }
}
