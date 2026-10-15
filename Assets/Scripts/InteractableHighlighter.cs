using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractableHighlighter : MonoBehaviour
{
    public static InteractableHighlighter Instance { get; private set; }

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private bool addToAllInteractables = true;

    [Header("Interactable Outline")]
    [SerializeField] private HighlightSettings settings = new HighlightSettings();

    [Header("Prompt Outline")]
    [SerializeField] private bool outlinePrompt = true;
    [SerializeField] private bool promptMatchesInteractable = true;
    [SerializeField] private HighlightSettings promptSettings = new HighlightSettings { side = OutlineSide.Outside, style = HighlightStyle.Steady };

    public Material OutlineMaterial => outlineMaterial;
    public HighlightSettings Settings => settings;
    public HighlightSettings PromptSettings
    {
        get
        {
            if (!outlinePrompt) return null;
            if (!promptMatchesInteractable) return promptSettings;

            _matchedPrompt.side = promptSettings.side;
            _matchedPrompt.width = promptSettings.width;
            _matchedPrompt.style = settings.style;
            _matchedPrompt.color = settings.color;
            _matchedPrompt.minAmount = settings.minAmount;
            _matchedPrompt.maxAmount = settings.maxAmount;
            _matchedPrompt.speed = settings.speed;
            return _matchedPrompt;
        }
    }

    private readonly HighlightSettings _matchedPrompt = new HighlightSettings();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        AddToAll();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => AddToAll();

    public void AddToAll()
    {
        if (!addToAllInteractables) return;

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour is IInteractable)
                Add(behaviour.gameObject);
        }
    }

    public static InteractableHighlight Add(GameObject target)
    {
        if (target == null) return null;
        return target.TryGetComponent<InteractableHighlight>(out var existing) ? existing : target.AddComponent<InteractableHighlight>();
    }
}