using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class OptionsMenuScript : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    [Header("Sensitivity")]
    [SerializeField] private float minSensitivity = 0.1f;
    [SerializeField] private float maxSensitivity = 3f;

    private const string SensitivitySliderName = "sensitivity-slider";
    private const string SensitivityValueLabelName = "sensitivity-value";

    private VisualElement root;
    private Slider sensitivitySlider;
    private Label sensitivityValueLabel;
    private bool sliderCallbackRegistered;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        TryInitializeUi();
        SetVisible(false);
    }

    private void OnEnable()
    {
        TryInitializeUi();
        RegisterSliderCallbacks();
        ResolveCameraIfNeeded();
        SyncFromCamera();
    }

    private void OnDisable()
    {
        UnregisterSliderCallbacks();
    }

    public void SetVisible(bool visible)
    {
        TryInitializeUi();

        IsVisible = visible;

        if (root != null)
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
            SyncFromCamera();
    }

    private void TryInitializeUi()
    {
        if (root != null)
            return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[OptionsMenuScript] UIDocument and root visual element are required.");
            return;
        }

        root = uiDocument.rootVisualElement;
        sensitivitySlider = root.Q<Slider>(SensitivitySliderName);
        sensitivityValueLabel = root.Q<Label>(SensitivityValueLabelName);

        if (sensitivitySlider == null)
        {
            Debug.LogError($"[OptionsMenuScript] Could not find Slider '{SensitivitySliderName}' in the options UXML.");
            return;
        }

        sensitivitySlider.lowValue = minSensitivity;
        sensitivitySlider.highValue = maxSensitivity;
    }

    private void ResolveCameraIfNeeded()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindFirstObjectByType<ThirdPersonCamera>();
    }

    private void RegisterSliderCallbacks()
    {
        if (sensitivitySlider == null || sliderCallbackRegistered)
            return;

        sensitivitySlider.RegisterValueChangedCallback(OnSensitivityChanged);
        sliderCallbackRegistered = true;
    }

    private void UnregisterSliderCallbacks()
    {
        if (sensitivitySlider == null || !sliderCallbackRegistered)
            return;

        sensitivitySlider.UnregisterValueChangedCallback(OnSensitivityChanged);
        sliderCallbackRegistered = false;
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

    private void OnSensitivityChanged(ChangeEvent<float> evt)
    {
        float newValue = Mathf.Clamp(evt.newValue, minSensitivity, maxSensitivity);

        if (thirdPersonCamera != null)
            thirdPersonCamera.sensitivity = newValue;

        UpdateSensitivityLabel(newValue);
    }

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = value.ToString("0.00");
    }
}
