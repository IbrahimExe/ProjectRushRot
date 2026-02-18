using UnityEngine;

public static class GameState
{
    public static bool IsStarted { get; private set; } = false;

    public static void StartGame()
    {
        IsStarted = true;
    }
}
