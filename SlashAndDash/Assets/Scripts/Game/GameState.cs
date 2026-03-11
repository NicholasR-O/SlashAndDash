using System;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public enum State
    {
        Playing,
        Paused,
        DebugMenu,
        GameOver
    }

    public static State Current { get; private set; } = State.Playing;
    public static bool IsPaused => Current == State.Paused || Current == State.DebugMenu;
    public static bool IsGameOver => Current == State.GameOver;
    public static bool IsPlaying => Current == State.Playing;
    public static bool IsDebugMenuOpen => Current == State.DebugMenu;

    public static bool GodMode { get; private set; }
    public static bool NoClip { get; private set; }
    public static bool DisableAI { get; private set; }

    public static event Action<State> StateChanged;
    public static event Action DebugSettingsChanged;

    static State stateBeforeDebugMenu = State.Playing;

    public static void SetPaused(bool paused)
    {
        if (IsGameOver)
            return;

        SetState(paused ? State.Paused : State.Playing);
    }

    public static void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public static void SetDebugMenu(bool visible)
    {
        if (IsGameOver)
            return;

        if (visible)
        {
            if (Current == State.DebugMenu)
                return;

            stateBeforeDebugMenu = Current;
            SetState(State.DebugMenu);
            return;
        }

        if (Current != State.DebugMenu)
            return;

        SetState(stateBeforeDebugMenu == State.Paused ? State.Paused : State.Playing);
    }

    public static void ToggleDebugMenu()
    {
        SetDebugMenu(!IsDebugMenuOpen);
    }

    public static void SetGameOver()
    {
        SetState(State.GameOver);
    }

    public static void SetPlaying()
    {
        SetState(State.Playing);
    }

    public static void SetState(State newState)
    {
        if (Current == newState)
            return;

        Current = newState;
        StateChanged?.Invoke(Current);
    }

    public static void SetGodMode(bool enabled)
    {
        if (GodMode == enabled)
            return;

        GodMode = enabled;
        DebugSettingsChanged?.Invoke();
    }

    public static void SetNoClip(bool enabled)
    {
        if (NoClip == enabled)
            return;

        NoClip = enabled;
        DebugSettingsChanged?.Invoke();
    }

    public static void SetDisableAI(bool enabled)
    {
        if (DisableAI == enabled)
            return;

        DisableAI = enabled;
        DebugSettingsChanged?.Invoke();
    }

    private void Awake()
    {
        GodMode = false;
        NoClip = false;
        DisableAI = false;
        stateBeforeDebugMenu = State.Playing;
        SetPlaying();
    }
}
