using System;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public enum State
    {
        Playing,
        Paused
    }

    public static State Current { get; private set; } = State.Playing;
    public static bool IsPaused => Current == State.Paused;

    public static event Action<State> StateChanged;

    public static void SetPaused(bool paused)
    {
        SetState(paused ? State.Paused : State.Playing);
    }

    public static void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public static void SetState(State newState)
    {
        if (Current == newState)
            return;

        Current = newState;
        StateChanged?.Invoke(Current);
    }

    private void Awake()
    {
        SetPaused(false);
    }
}
