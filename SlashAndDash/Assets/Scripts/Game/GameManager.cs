using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OptionsMenuScript optionsMenu;

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

        ApplyPauseState(GameState.IsPaused);

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

        GameState.TogglePause();
    }

    private void OnGameStateChanged(GameState.State state)
    {
        ApplyPauseState(state == GameState.State.Paused);
    }

    private void ApplyPauseState(bool paused)
    {
        if (pauseTimeScale)
            Time.timeScale = paused ? 0f : 1f;

        if (optionsMenu != null)
            optionsMenu.SetVisible(paused);

        Cursor.visible = paused;
        Cursor.lockState = paused
            ? CursorLockMode.None
            : (lockCursorWhenPlaying ? CursorLockMode.Locked : CursorLockMode.None);
    }
}
