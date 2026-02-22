using System;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public enum State
    {
        Playing,
        Paused,
        GameOver
    }

    public static State Current { get; private set; } = State.Playing;
    public static bool IsPaused => Current == State.Paused;
    public static bool IsGameOver => Current == State.GameOver;
    public static bool IsPlaying => Current == State.Playing;

    public static event Action<State> StateChanged;

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

    private void Awake()
    {
        SetPlaying();
    }
}
