using System;
using UnityEngine;

public enum GameState
{
    Title,
    CharacterCreation,
    World,
    Minigame_EndlessRunner,
    Minigame_Memory,
    Minigame_Rhythm
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; } = GameState.Title;
    public event Action<GameState, GameState> OnStateChanged;

    [Header("Scene names")]
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField] private string characterCreationSceneName = "CharacterCreation";
    [SerializeField] private string mainWorldSceneName = "MainLevel";
    [SerializeField] private string endlessRunnerSceneName = "EndlessRunner";
    [SerializeField] private string memoryGameSceneName = "MemoryGame";
    [SerializeField] private string rhythmGameSceneName = "Rhythm";

    [Header("Input action map names")]
    [SerializeField] private string worldActionMap = "Gameplay";
    [SerializeField] private string uiActionMap = "UI";
    [SerializeField] private string endlessRunnerActionMap = "EndlessRunner";
    [SerializeField] private string memoryGameActionMap = "MemoryGame";
    [SerializeField] private string rhythmGameActionMap = "Rhythm";

    private GameState _activeMinigame;
    private string _pendingMinigameFlag;

    private bool IsBusy => SceneLoader.Instance.IsTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GoTo(GameState.Title, titleSceneName, uiActionMap);
    }

    public void StartNewGame()
    {
        if (IsBusy) return;
        GoTo(GameState.CharacterCreation, characterCreationSceneName, uiActionMap);
    }

    public void LoadGame(string saveId)
    {
        if (IsBusy) return;

        if (!SaveManager.Instance.LoadSave(saveId))
        {
            Debug.LogError($"Failed to load save {saveId}");
            return;
        }

        GoTo(GameState.World, mainWorldSceneName, worldActionMap);
    }

    public void CompleteCharacterCreation(CharacterData character)
    {
        if (IsBusy) return;

        SaveManager.Instance.CreateNewSave(character, mainWorldSceneName);
        GoTo(GameState.World, mainWorldSceneName, worldActionMap);
    }

    public void ReturnToTitle()
    {
        if (IsBusy) return;
        GoTo(GameState.Title, titleSceneName, uiActionMap);
    }

    private void GoTo(GameState state, string sceneName, string actionMap)
    {
        ChangeState(state);
        SceneLoader.Instance.TransitionTo(sceneName);
        InputManager.Instance.SwitchMap(actionMap);
    }

    public void StartMinigame(GameState minigame, string completionFlag = null)
    {
        if (!IsMinigame(minigame))
        {
            Debug.LogError($"{minigame} is not a minigame state");
            return;
        }

        if (IsMinigame(CurrentState))
        {
            Debug.LogWarning($"Cannot start {minigame} while {CurrentState} is running");
            return;
        }

        _activeMinigame = minigame;
        _pendingMinigameFlag = completionFlag;

        ChangeState(minigame);
        SceneLoader.Instance.LoadAdditive(SceneNameFor(minigame));
        InputManager.Instance.SwitchMap(ActionMapFor(minigame));
    }

    public void CompleteMinigame(bool success, int statPointsAwarded = 0)
    {
        if (!IsMinigame(CurrentState))
        {
            Debug.LogWarning("CompleteMinigame called but no minigame is active");
            return;
        }

        var save = SaveManager.Instance.CurrentSave;
        if (success && save != null)
        {
            if (!string.IsNullOrEmpty(_pendingMinigameFlag))
                save.SetFlag(_pendingMinigameFlag, true);

            if (statPointsAwarded > 0)
                save.stats.GrantPoints(statPointsAwarded);

            SaveManager.Instance.SaveCurrent();
        }

        SceneLoader.Instance.UnloadScene(SceneNameFor(_activeMinigame), () =>
        {
            ChangeState(GameState.World);
            InputManager.Instance.SwitchMap(worldActionMap);
        });

        _activeMinigame = default;
        _pendingMinigameFlag = null;
    }

    private static bool IsMinigame(GameState state) =>
        state == GameState.Minigame_EndlessRunner ||
        state == GameState.Minigame_Memory ||
        state == GameState.Minigame_Rhythm;

    private string SceneNameFor(GameState minigame) => minigame switch
    {
        GameState.Minigame_EndlessRunner => endlessRunnerSceneName,
        GameState.Minigame_Memory => memoryGameSceneName,
        GameState.Minigame_Rhythm => rhythmGameSceneName,
        _ => throw new ArgumentOutOfRangeException(nameof(minigame))
    };

    private string ActionMapFor(GameState minigame) => minigame switch
    {
        GameState.Minigame_EndlessRunner => endlessRunnerActionMap,
        GameState.Minigame_Memory => memoryGameActionMap,
        GameState.Minigame_Rhythm => rhythmGameActionMap,
        _ => throw new ArgumentOutOfRangeException(nameof(minigame))
    };

    private void ChangeState(GameState next)
    {
        var previous = CurrentState;
        CurrentState = next;
        OnStateChanged?.Invoke(previous, next);
        Debug.Log($"GameState: {previous} -> {next}");
    }
}