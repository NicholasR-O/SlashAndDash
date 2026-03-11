using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UIDocument uiDocument;
    [SerializeField] CarController player;

    [Header("Circular Gauge")]
    [SerializeField] float healthVisualLerpSpeed = 4f;
    [SerializeField] Color healthColor = new Color32(107, 230, 132, 255);
    [SerializeField] Color speedColor = new Color32(81, 170, 244, 255);
    [SerializeField] Color boostColor = new Color32(255, 188, 78, 255);
    [SerializeField] Color boostFullColor = new Color32(255, 225, 132, 255);
    [SerializeField] Color trickColor = new Color32(120, 225, 255, 255);
    [SerializeField] Color gaugeTrackColor = new Color(1f, 1f, 1f, 0.14f);

    const string RootName = "hud-root";
    const string HealthGaugeName = "health-gauge";
    const string SpeedGaugeName = "speed-gauge";
    const string BoostGaugeName = "boost-gauge";
    const string TutorialContainerName = "tutorial-container";
    const string TutorialTextName = "tutorial-text";

    const float FullCircleRadians = Mathf.PI * 2f;
    const float StartAngleRadians = -Mathf.PI * 0.5f;
    const float GaugePadding = 8f;
    const float FullBoostThreshold = 0.995f;

    struct GaugeGeometry
    {
        public Vector2 center;
        public float healthRadius;
        public float speedInnerRadius;
        public float speedOuterRadius;
        public float speedThickness;
        public float boostInnerRadius;
        public float boostOuterRadius;
        public float boostBaseThickness;
    }

    VisualElement root;
    VisualElement healthGauge;
    VisualElement speedGauge;
    VisualElement boostGauge;
    VisualElement tutorialContainer;
    Label tutorialTextLabel;
    float displayedHealthRatio = 1f;
    bool healthVisualInitialized;
    float speedGaugeRatio;
    float boostGaugeRatio;
    bool showTrickOnOuterGauge;
    string tutorialMessage = string.Empty;
    bool tutorialMessageVisible;

    public bool IsVisible { get; private set; }

    void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        TryInitializeUi();
        SetVisible(GameState.IsPlaying);
    }

    void OnEnable()
    {
        TryInitializeUi();
        GameState.StateChanged += OnGameStateChanged;
        ResolvePlayerIfNeeded();
        RefreshBars();
    }

    void OnDisable()
    {
        GameState.StateChanged -= OnGameStateChanged;
    }

    void Update()
    {
        if (!IsVisible)
            return;

        ResolvePlayerIfNeeded();
        RefreshBars();
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

        root = uiDocument.rootVisualElement.Q<VisualElement>(RootName);
        healthGauge = uiDocument.rootVisualElement.Q<VisualElement>(HealthGaugeName);
        speedGauge = uiDocument.rootVisualElement.Q<VisualElement>(SpeedGaugeName);
        boostGauge = uiDocument.rootVisualElement.Q<VisualElement>(BoostGaugeName);
        tutorialContainer = uiDocument.rootVisualElement.Q<VisualElement>(TutorialContainerName);
        tutorialTextLabel = uiDocument.rootVisualElement.Q<Label>(TutorialTextName);

        if (healthGauge != null)
            healthGauge.generateVisualContent += DrawHealthGauge;
        if (speedGauge != null)
            speedGauge.generateVisualContent += DrawSpeedGauge;
        if (boostGauge != null)
            boostGauge.generateVisualContent += DrawBoostGauge;

        ApplyTutorialMessage();
    }

    void ResolvePlayerIfNeeded()
    {
        if (player == null)
            player = FindFirstObjectByType<CarController>();
    }

    void RefreshBars()
    {
        float speedRatio = player != null ? player.SpeedRatio : 0f;
        float hpRatio = player != null ? player.CurrentHealth / Mathf.Max(1f, player.MaxHealth) : 0f;
        float boostRatio = player != null ? player.RemainingBoostRatio : 0f;
        bool inAir = player != null && player.TrickInAir;
        float trickRatio = player != null ? player.TrickAirTimeRatio : 0f;

        UpdateHealthVisual(hpRatio);
        speedGaugeRatio = Mathf.Clamp01(speedRatio);
        boostGaugeRatio = Mathf.Clamp01(inAir ? trickRatio : boostRatio);
        showTrickOnOuterGauge = inAir;

        healthGauge?.MarkDirtyRepaint();
        speedGauge?.MarkDirtyRepaint();
        boostGauge?.MarkDirtyRepaint();
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

    void DrawHealthGauge(MeshGenerationContext context)
    {
        if (!TryGetGaugeGeometry(healthGauge, out GaugeGeometry geometry))
            return;

        DrawCircleSector(context, geometry.center, geometry.healthRadius, 1f, new Color(healthColor.r, healthColor.g, healthColor.b, 0.14f));
        DrawCircleSector(context, geometry.center, geometry.healthRadius * Mathf.Clamp01(displayedHealthRatio), 1f, healthColor);
    }

    void DrawSpeedGauge(MeshGenerationContext context)
    {
        if (!TryGetGaugeGeometry(speedGauge, out GaugeGeometry geometry))
            return;

        DrawCircleRing(context, geometry.center, geometry.speedInnerRadius, geometry.speedOuterRadius, 1f, gaugeTrackColor);
        DrawCircleRing(context, geometry.center, geometry.speedInnerRadius, geometry.speedOuterRadius, speedGaugeRatio, speedColor);
    }

    void DrawBoostGauge(MeshGenerationContext context)
    {
        if (!TryGetGaugeGeometry(boostGauge, out GaugeGeometry geometry))
            return;

        if (showTrickOnOuterGauge)
        {
            DrawCircleRing(context, geometry.center, geometry.boostInnerRadius, geometry.boostOuterRadius, 1f, gaugeTrackColor);
            DrawCircleRing(context, geometry.center, geometry.boostInnerRadius, geometry.boostOuterRadius, boostGaugeRatio, trickColor);
            return;
        }

        bool isFull = boostGaugeRatio >= FullBoostThreshold;
        float fillInnerRadius = isFull
            ? Mathf.Max(0f, geometry.speedInnerRadius - geometry.speedThickness * 0.65f)
            : geometry.boostInnerRadius;
        Color fillColor = isFull ? boostFullColor : boostColor;

        DrawCircleRing(context, geometry.center, geometry.boostInnerRadius, geometry.boostOuterRadius, 1f, gaugeTrackColor);
        DrawCircleRing(context, geometry.center, fillInnerRadius, geometry.boostOuterRadius, boostGaugeRatio, fillColor);
    }

    bool TryGetGaugeGeometry(VisualElement element, out GaugeGeometry geometry)
    {
        geometry = default;
        if (element == null)
            return false;

        Rect rect = element.contentRect;
        if (rect.width < 20f || rect.height < 20f)
            return false;

        Vector2 center = rect.center;
        float maxRadius = (Mathf.Min(rect.width, rect.height) * 0.5f) - GaugePadding;
        if (maxRadius <= 2f)
            return false;

        geometry.center = center;
        geometry.healthRadius = maxRadius * 0.56f;

        geometry.speedOuterRadius = maxRadius * 0.76f;
        geometry.speedThickness = maxRadius * 0.12f;
        geometry.speedInnerRadius = Mathf.Max(0f, geometry.speedOuterRadius - geometry.speedThickness);

        geometry.boostOuterRadius = maxRadius * 0.9f;
        geometry.boostBaseThickness = maxRadius * 0.04f;
        geometry.boostInnerRadius = Mathf.Max(0f, geometry.boostOuterRadius - geometry.boostBaseThickness);
        return true;
    }

    static void DrawCircleSector(MeshGenerationContext context, Vector2 center, float radius, float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);
        if (radius <= 0.01f || ratio <= 0.001f)
            return;

        int segments = Mathf.Max(8, Mathf.CeilToInt(80f * ratio));
        int vertexCount = segments + 2;
        int indexCount = segments * 3;
        MeshWriteData mesh = context.Allocate(vertexCount, indexCount);

        mesh.SetNextVertex(CreateVertex(center, color));

        for (int i = 0; i <= segments; i++)
        {
            float t = ratio * (i / (float)segments);
            Vector2 point = PointOnCircle(center, radius, t);
            mesh.SetNextVertex(CreateVertex(point, color));
        }

        for (int i = 0; i < segments; i++)
        {
            mesh.SetNextIndex(0);
            mesh.SetNextIndex((ushort)(i + 1));
            mesh.SetNextIndex((ushort)(i + 2));
        }
    }

    static void DrawCircleRing(MeshGenerationContext context, Vector2 center, float innerRadius, float outerRadius, float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);
        if (outerRadius <= innerRadius + 0.01f || ratio <= 0.001f)
            return;

        int segments = Mathf.Max(6, Mathf.CeilToInt(120f * ratio));
        int vertexCount = (segments + 1) * 2;
        int indexCount = segments * 6;
        MeshWriteData mesh = context.Allocate(vertexCount, indexCount);

        for (int i = 0; i <= segments; i++)
        {
            float t = ratio * (i / (float)segments);
            Vector2 outer = PointOnCircle(center, outerRadius, t);
            Vector2 inner = PointOnCircle(center, innerRadius, t);
            mesh.SetNextVertex(CreateVertex(outer, color));
            mesh.SetNextVertex(CreateVertex(inner, color));
        }

        for (int i = 0; i < segments; i++)
        {
            int vertex = i * 2;
            ushort outerA = (ushort)vertex;
            ushort innerA = (ushort)(vertex + 1);
            ushort outerB = (ushort)(vertex + 2);
            ushort innerB = (ushort)(vertex + 3);

            mesh.SetNextIndex(outerA);
            mesh.SetNextIndex(outerB);
            mesh.SetNextIndex(innerB);

            mesh.SetNextIndex(outerA);
            mesh.SetNextIndex(innerB);
            mesh.SetNextIndex(innerA);
        }
    }

    static Vertex CreateVertex(Vector2 point, Color color)
    {
        return new Vertex
        {
            position = new Vector3(point.x, point.y, Vertex.nearZ),
            tint = color,
            uv = Vector2.zero
        };
    }

    static Vector2 PointOnCircle(Vector2 center, float radius, float normalizedAngle)
    {
        float angle = StartAngleRadians + Mathf.Clamp01(normalizedAngle) * FullCircleRadians;
        return new Vector2(
            center.x + Mathf.Cos(angle) * radius,
            center.y + Mathf.Sin(angle) * radius);
    }

    void ApplyTutorialMessage()
    {
        TryInitializeUi();
        if (tutorialContainer == null || tutorialTextLabel == null)
            return;

        tutorialTextLabel.text = tutorialMessage;
        tutorialContainer.style.display = tutorialMessageVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

}
