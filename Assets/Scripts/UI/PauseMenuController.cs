using UnityEngine;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button leaveMinigameButton;
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private string returnToTitleMessage = "Return to the title screen?";

    public bool IsPaused { get; private set; }

    private float _previousTimeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel.SetActive(false);

        resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null)
            optionsButton.onClick.AddListener(OpenOptions);
        if (leaveMinigameButton != null)
            leaveMinigameButton.onClick.AddListener(LeaveMinigame);
        if (returnToTitleButton != null)
            returnToTitleButton.onClick.AddListener(ReturnToTitle);

        if (uiPanel != null)
            uiPanel.OnBack += Resume;
    }

    private void Start()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnPauseRequested += TryPause;
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnPauseRequested -= TryPause;

        if (IsPaused)
            Time.timeScale = _previousTimeScale;
    }

    private void TryPause()
    {
        if (IsPaused) return;

        var game = GameManager.Instance;
        if (game == null || game.IsTransitioning) return;

        var state = game.CurrentState;
        bool inMinigame = GameManager.IsMinigameState(state);
        if (state != GameState.World && !inMinigame) return;

        IsPaused = true;
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (leaveMinigameButton != null)
            leaveMinigameButton.gameObject.SetActive(inMinigame && MinigameBase.Current != null);

        UIPanelAnimator.SetVisible(panel, true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = _previousTimeScale;
        UIPanelAnimator.SetVisible(panel, false);
    }

    private void OpenOptions()
    {
        if (OptionsMenuController.Instance != null)
            OptionsMenuController.Instance.Open();
        else
            Debug.LogWarning("No OptionsMenuController in Bootstrap", this);
    }

    private void LeaveMinigame()
    {
        var minigame = MinigameBase.Current;
        Resume();

        if (minigame != null)
            minigame.ReturnToWorld();
    }

    private void ReturnToTitle()
    {
        if (ConfirmationDialogueController.Instance == null)
        {
            ConfirmReturnToTitle();
            return;
        }

        ConfirmationDialogueController.Instance.Show(returnToTitleMessage, ConfirmReturnToTitle);
    }

    private void ConfirmReturnToTitle()
    {
        Resume();

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCurrent();

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToTitle();
    }
}