using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UIDocument uiDocument;
    [SerializeField] CarController player;

    const string RootName = "hud-root";
    const string SpeedBarName = "speed-bar";
    const string HpBarName = "hp-bar";
    const string BoostBarName = "boost-bar";
    const string SpeedValueName = "speed-value";
    const string HpValueName = "hp-value";
    const string BoostValueName = "boost-value";

    VisualElement root;
    ProgressBar speedBar;
    ProgressBar hpBar;
    ProgressBar boostBar;
    Label speedValue;
    Label hpValue;
    Label boostValue;

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
        speedBar = uiDocument.rootVisualElement.Q<ProgressBar>(SpeedBarName);
        hpBar = uiDocument.rootVisualElement.Q<ProgressBar>(HpBarName);
        boostBar = uiDocument.rootVisualElement.Q<ProgressBar>(BoostBarName);
        speedValue = uiDocument.rootVisualElement.Q<Label>(SpeedValueName);
        hpValue = uiDocument.rootVisualElement.Q<Label>(HpValueName);
        boostValue = uiDocument.rootVisualElement.Q<Label>(BoostValueName);

        ConfigureBar(speedBar);
        ConfigureBar(hpBar);
        ConfigureBar(boostBar);
    }

    static void ConfigureBar(ProgressBar bar)
    {
        if (bar == null)
            return;

        bar.lowValue = 0f;
        bar.highValue = 100f;
        bar.value = 0f;
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

        SetBar(speedBar, speedRatio);
        SetBar(hpBar, hpRatio);
        SetBar(boostBar, boostRatio);

        if (speedValue != null)
            speedValue.text = player != null ? player.CurrentSpeed.ToString("0.0") : "0.0";
        if (hpValue != null)
            hpValue.text = (hpRatio * 100f).ToString("0") + "%";
        if (boostValue != null)
            boostValue.text = (boostRatio * 100f).ToString("0") + "%";
    }

    static void SetBar(ProgressBar bar, float ratio)
    {
        if (bar == null)
            return;

        bar.value = Mathf.Clamp01(ratio) * 100f;
    }
}
