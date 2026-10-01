using System;
using System.Collections;
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
    [SerializeField] private string worldActionMap = "Player";
    [SerializeField] private string uiActionMap = "UI";
    [SerializeField] private string endlessRunnerActionMap = "EndlessRunner";
    [SerializeField] private string memoryGameActionMap = "UI";
    [SerializeField] private string rhythmGameActionMap = "Rhythm";

    public bool IsTransitioning { get; private set; }
    public MinigameConfig ActiveMinigameConfig { get; private set; }

    private GameState _activeMinigame;
    private string _pendingMinigameFlag;

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
        StartCoroutine(Transition(GameState.Title, uiActionMap, () => SceneLoader.Instance.ReplaceAll(titleSceneName)));
    }

    public void StartNewGame()
    {
        if (IsTransitioning) return;
        StartCoroutine(Transition(GameState.CharacterCreation, uiActionMap, () => SceneLoader.Instance.ReplaceAll(characterCreationSceneName)));
    }

    public void LoadGame(string saveId)
    {
        if (IsTransitioning) return;

        if (!SaveManager.Instance.LoadSave(saveId))
        {
            Debug.LogError($"Failed to load save {saveId}");
            return;
        }
        TelemetryManager.Log("save_loaded", saveId);
        EnterWorld();
    }

    public void CompleteCharacterCreation(CharacterData character)
    {
        if (IsTransitioning) return;

        SaveManager.Instance.CreateNewSave(character, mainWorldSceneName);
        TelemetryManager.Log("new_game");
        EnterWorld();
    }

    public void ReturnToTitle()
    {
        if (IsTransitioning) return;
        StartCoroutine(Transition(GameState.Title, uiActionMap, () => SceneLoader.Instance.ReplaceAll(titleSceneName)));
    }

    private void EnterWorld()
    {
        StartCoroutine(Transition(GameState.World, worldActionMap, () => SceneLoader.Instance.ReplaceAll(mainWorldSceneName)));
    }

    public void StartMinigame(GameState minigame, string completionFlag = null, MinigameConfig config = null)
    {
        if (IsTransitioning) return;

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
        ActiveMinigameConfig = config;

        string sceneName = SceneNameFor(minigame);
        StartCoroutine(Transition(minigame, ActionMapFor(minigame), () => EnterMinigameScene(sceneName)));
    }

    private IEnumerator EnterMinigameScene(string sceneName)
    {
        yield return SceneLoader.Instance.LoadAdditive(sceneName);
        SceneLoader.Instance.HideScene(mainWorldSceneName);
    }

    private IEnumerator ExitMinigameScene(string sceneName)
    {
        SceneLoader.Instance.ShowScene(mainWorldSceneName);
        yield return SceneLoader.Instance.Unload(sceneName);
    }

    public void CompleteMinigame(bool success, int statPointsAwarded = 0)
    {
        if (IsTransitioning) return;

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

        string sceneName = SceneNameFor(_activeMinigame);
        _activeMinigame = default;
        _pendingMinigameFlag = null;
        ActiveMinigameConfig = null;

        StartCoroutine(Transition(GameState.World, worldActionMap, () => ExitMinigameScene(sceneName)));
    }

    private IEnumerator Transition(GameState state, string actionMap, Func<IEnumerator> sceneWork)
    {
        IsTransitioning = true;
        Time.timeScale = 1f;
        InputManager.Instance.DisableModeMaps();

        yield return SceneLoader.Instance.FadeOut();

        ChangeState(state);
        InputManager.Instance.SwitchMap(actionMap);

        yield return sceneWork();
        yield return SceneLoader.Instance.FadeIn();

        IsTransitioning = false;
    }

    public static bool IsMinigameState(GameState state) => IsMinigame(state);

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