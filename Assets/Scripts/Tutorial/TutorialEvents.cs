using System;

public static class TutorialEvents
{
    public const string PlayerMoved = "PlayerMoved";
    public const string PlayerJumped = "PlayerJumped";
    public const string PlayerDropped = "PlayerDropped";
    public const string Interacted = "Interacted";
    public const string DialogueStarted = "DialogueStarted";
    public const string DialogueFinished = "DialogueFinished";
    public const string PauseOpened = "PauseOpened";
    public const string StatsMenuOpened = "StatsMenuOpened";
    public const string StatPointsSpent = "StatPointsSpent";
    public const string StatPointsEarned = "StatPointsEarned";
    public const string StateEntered = "StateEntered";
    public const string MinigameBegan = "MinigameBegan";
    public const string MinigameSucceeded = "MinigameSucceeded";
    public const string MinigameFailed = "MinigameFailed";

    public static event Action<string> OnRaised;

    public static void Raise(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        OnRaised?.Invoke(key);
    }

    public static void Raise(string key, string detail)
    {
        Raise(key);
        if (!string.IsNullOrEmpty(detail))
            Raise($"{key}:{detail}");
    }
}
