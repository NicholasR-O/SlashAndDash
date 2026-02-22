using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OptionsMenuScript optionsMenu;
    [SerializeField] private UIDocument hudDocument;

    [Header("Pause Behavior")]
    [SerializeField] private bool lockCursorWhenPlaying = true;
    [SerializeField] private bool pauseTimeScale = true;

    private MenuInputActions menuInputActions;

    private void Awake()
    {
        menuInputActions = new MenuInputActions();
    }

    private void Start()
    {
        if (optionsMenu == null)
            optionsMenu = FindFirstObjectByType<OptionsMenuScript>();
        ResolveHudDocumentIfNeeded();

        ApplyGameState(GameState.Current);

        if (optionsMenu == null)
            Debug.LogWarning("[GameManager] OptionsMenuScript reference is missing.");
    }

    private void OnEnable()
    {
        GameState.StateChanged += OnGameStateChanged;

        if (menuInputActions == null)
            menuInputActions = new MenuInputActions();

        menuInputActions.PauseMenu.Pause.performed += OnPausePerformed;
        menuInputActions.PauseMenu.Enable();
    }

    private void OnDisable()
    {
        GameState.StateChanged -= OnGameStateChanged;

        if (menuInputActions != null)
        {
            menuInputActions.PauseMenu.Pause.performed -= OnPausePerformed;
            menuInputActions.PauseMenu.Disable();
        }
    }

    private void OnDestroy()
    {
        if (pauseTimeScale)
            Time.timeScale = 1f;

        menuInputActions?.Dispose();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (GameState.IsGameOver)
            return;

        GameState.TogglePause();
    }

    private void OnGameStateChanged(GameState.State state)
    {
        ApplyGameState(state);
    }

    private void ApplyGameState(GameState.State state)
    {
        bool paused = state == GameState.State.Paused;
        bool gameOver = state == GameState.State.GameOver;
        bool blockGameplay = paused || gameOver;

        if (pauseTimeScale)
            Time.timeScale = blockGameplay ? 0f : 1f;

        if (optionsMenu != null)
            optionsMenu.SetVisible(paused);
        SetHudVisible(state == GameState.State.Playing);

        UnityEngine.Cursor.visible = blockGameplay;
        UnityEngine.Cursor.lockState = blockGameplay
            ? CursorLockMode.None
            : (lockCursorWhenPlaying ? CursorLockMode.Locked : CursorLockMode.None);
    }

    private void SetHudVisible(bool visible)
    {
        ResolveHudDocumentIfNeeded();

        if (hudDocument == null || hudDocument.rootVisualElement == null)
            return;

        VisualElement hudRoot = hudDocument.rootVisualElement.Q<VisualElement>("hud-root");
        if (hudRoot == null)
            return;

        hudRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ResolveHudDocumentIfNeeded()
    {
        if (hudDocument != null)
            return;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        for (int i = 0; i < documents.Length; i++)
        {
            UIDocument doc = documents[i];
            if (doc == null || doc.rootVisualElement == null)
                continue;

            if (doc.rootVisualElement.Q<VisualElement>("hud-root") != null)
            {
                hudDocument = doc;
                return;
            }
        }
    }
}
